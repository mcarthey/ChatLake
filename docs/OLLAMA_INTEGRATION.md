# ChatLake + Ollama Integration

> Draft for blog post at https://learnedgeek.com/Blog

## Overview

ChatLake uses local LLMs via [Ollama](https://ollama.com) for two key tasks:
1. **Semantic Embeddings** - Converting conversation text into meaning-vectors
2. **Cluster Naming** - Generating human-readable names for conversation groups

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        ChatLake                                  │
├─────────────────────────────────────────────────────────────────┤
│  ClusteringService                                               │
│    │                                                             │
│    ├── 1. Load conversations from DB                            │
│    │                                                             │
│    ├── 2. Generate embeddings (ILlmService)                     │
│    │      └── OllamaService.GenerateEmbeddingAsync()            │
│    │          └── POST http://localhost:11434/api/embed         │
│    │              Model: nomic-embed-text (768 dimensions)      │
│    │                                                             │
│    ├── 3. Cluster embeddings (ML.NET KMeans)                    │
│    │      └── Groups similar vectors together                   │
│    │                                                             │
│    └── 4. Name clusters (ILlmService)                           │
│           └── OllamaService.GenerateClusterNameAsync()          │
│               └── POST http://localhost:11434/api/generate      │
│                   Model: mistral:7b                             │
└─────────────────────────────────────────────────────────────────┘
```

## Ollama API Endpoints Used

### 1. List Models
```http
GET http://localhost:11434/api/tags
```
Used to check if required models are installed.

### 2. Generate Embeddings
```http
POST http://localhost:11434/api/embed
Content-Type: application/json

{
  "model": "nomic-embed-text",
  "input": ["Your text to embed here..."]
}
```

**Response:**
```json
{
  "embeddings": [[0.123, -0.456, 0.789, ...]]  // 768 floats
}
```

**Key Limitations:**
- `nomic-embed-text` has ~2048 token context (~6000 characters)
- Longer texts must be truncated
- Currently truncating to 5000 characters

### 3. Generate Text (Streaming)
```http
POST http://localhost:11434/api/generate
Content-Type: application/json

{
  "model": "mistral:7b",
  "prompt": "Your prompt here...",
  "options": {
    "temperature": 0.3,
    "num_predict": 30
  }
}
```

**Response (streamed):**
```json
{"response": "Word"}
{"response": " by"}
{"response": " word"}
{"done": true}
```

## Models Used

| Model | Purpose | Size | Dimensions |
|-------|---------|------|------------|
| `nomic-embed-text` | Semantic embeddings | 274 MB | 768 |
| `mistral:7b` | Cluster naming | 4.1 GB | N/A |

## Current Implementation

### OllamaService.cs

```csharp
public sealed class OllamaService : ILlmService
{
    private readonly OllamaApiClient _client;
    private readonly string _model = "mistral:7b";
    private readonly string _embeddingModel = "nomic-embed-text";

    // Generate embedding for a conversation
    public async Task<float[]?> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        var truncated = TruncateText(text, 5000);
        var response = await _client.EmbedAsync(new EmbedRequest
        {
            Model = _embeddingModel,
            Input = [truncated]
        }, ct);

        return response?.Embeddings?[0]?.Select(d => (float)d).ToArray();
    }

    // Generate a name for a cluster of conversations
    public async Task<string> GenerateClusterNameAsync(
        IReadOnlyList<string> samples,
        int count,
        CancellationToken ct)
    {
        var prompt = BuildNamingPrompt(samples, count);
        var response = new StringBuilder();

        await foreach (var chunk in _client.GenerateAsync(...))
        {
            response.Append(chunk.Response);
        }

        return CleanupResponse(response.ToString());
    }
}
```

## Known Issues & Future Improvements

### Issue 1: Context Length Errors
Some conversations exceed the embedding model's context window.

**Current Mitigation:** Truncate to 5000 characters
**Future Fix:** Chunk long conversations and embed segments separately

### Issue 2: Generic Cluster Names ("User Profile")
The LLM sometimes returns generic names, possibly because:
- Conversation text includes role markers ("user:", "assistant:")
- Similar patterns across different topic clusters

**Future Fix:**
- Strip role markers before embedding/naming
- Use more specific prompting
- Consider few-shot examples

### Issue 3: Topic Drift Within Conversations
Long conversations often cover multiple topics, but current approach treats each conversation as one unit.

**Future Architecture:**
```
Current:  Conversation → Single Embedding → Cluster
Future:   Conversation → Split into Segments → Multiple Embeddings → Cluster Segments
```

This would allow:
- Finding topics that span conversations
- Better handling of long, multi-topic chats
- More granular project suggestions

## Performance Characteristics

| Operation | Time (1293 conversations) |
|-----------|---------------------------|
| Load from DB | ~2 seconds |
| Generate embeddings | ~5-10 minutes |
| KMeans clustering | ~1 second |
| Generate 12 cluster names | ~30 seconds |

**Bottleneck:** Embedding generation (sequential API calls)

**Optimization Ideas:**
- Batch embedding requests (Ollama supports arrays)
- Parallel embedding with multiple model instances
- Cache embeddings in database

## Setup Instructions

### 1. Install Ollama
```bash
# Windows: Download from https://ollama.com/download
# Or via winget:
winget install Ollama.Ollama
```

### 2. Pull Required Models
```bash
ollama pull nomic-embed-text
ollama pull mistral:7b
```

### 3. Verify Ollama is Running
```bash
curl http://localhost:11434/api/tags
```

### 4. Run ChatLake
```bash
cd src/ChatLake.Web
dotnet run
```

## Future: Topic Segmentation (Topic Drift)

### The Problem
Long conversations often cover multiple topics. Currently we treat each conversation as one unit:

```
Conversation 1: "3D printing → Recipe for dinner → Unity game bug"
                     ↓
              Single embedding (mixed topics)
                     ↓
              Confused clustering
```

### The Solution: Segment-Level Analysis

Instead of embedding whole conversations, split them into topic segments:

```
Conversation 1: "3D printing → Recipe for dinner → Unity game bug"
                     ↓
        Split into 3 segments (by topic shift detection)
                     ↓
Segment 1: "3D printing"     → Embedding → Cluster A (3D Printing)
Segment 2: "Recipe"          → Embedding → Cluster B (Cooking)
Segment 3: "Unity game bug"  → Embedding → Cluster C (Game Dev)
```

### Implementation Approach

1. **Topic Shift Detection**
   - Use embedding similarity between adjacent message groups
   - When similarity drops below threshold, mark as new segment
   - Or use LLM to identify topic boundaries

2. **New Data Model**
   ```
   Conversation (1) ──→ (many) ConversationSegment
                              ├── StartMessageIndex
                              ├── EndMessageIndex
                              ├── EmbeddingVector
                              └── TopicLabel
   ```

3. **Benefits**
   - Find all segments about "3D printing" across ALL conversations
   - Better blog topic suggestions (pull related segments together)
   - Answer "Have I solved this before?" more accurately
   - Handle topic drift naturally

### Blog Topics from Segments

With segment-level analysis, we can:
1. Find all segments about a topic (e.g., "Raspberry Pi GPIO")
2. Rank by depth/quality of discussion
3. Suggest: "You have 15 segments discussing GPIO - write a blog post?"

## References

- [Ollama API Documentation](https://github.com/ollama/ollama/blob/main/docs/api.md)
- [OllamaSharp NuGet Package](https://www.nuget.org/packages/OllamaSharp)
- [nomic-embed-text Model](https://ollama.com/library/nomic-embed-text)
- [Mistral 7B Model](https://ollama.com/library/mistral)
