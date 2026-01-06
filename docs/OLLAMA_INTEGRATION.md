# ChatLake + Ollama Integration

> Draft for blog post at https://learnedgeek.com/Blog

**Last Updated:** 2026-01-07

## Overview

ChatLake uses local LLMs via [Ollama](https://ollama.com) for three key tasks:
1. **Semantic Embeddings** - Converting text into 768-dimensional meaning-vectors
2. **Topic Segmentation** - Splitting conversations into coherent chunks using embedding similarity
3. **Cluster Naming** - Generating human-readable names for segment groups

## Architecture (UMAP + HDBSCAN Pipeline)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           ChatLake ML Pipeline                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  1. SEGMENTATION PHASE (SegmentationService)                                │
│     └── Load conversations without segments                                  │
│     └── Generate sliding window embeddings (Ollama nomic-embed-text)        │
│     └── Detect topic boundaries (cosine similarity < 0.55)                  │
│     └── Create ConversationSegment records                                  │
│                                                                              │
│  2. EMBEDDING PHASE (EmbeddingCacheService)                                 │
│     └── Load segments without embeddings                                    │
│     └── Generate 768-dim vectors (Ollama nomic-embed-text)                  │
│     └── Cache in SegmentEmbedding table                                     │
│     └── Hash-based invalidation for changes                                 │
│                                                                              │
│  3. CLUSTERING PHASE (UmapHdbscanPipeline)                                  │
│     └── Load all segment embeddings                                          │
│     └── UMAP dimensionality reduction (768D → 15D)                          │
│     └── HDBSCAN density-based clustering (MinClusterSize=8)                 │
│     └── Identify noise points (don't force-fit into clusters)              │
│                                                                              │
│  4. NAMING PHASE (OllamaService)                                            │
│     └── Sample 12 segments per cluster                                      │
│     └── LLM generates common theme name (mistral:7b)                        │
│     └── Create ProjectSuggestion records                                    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Why UMAP + HDBSCAN?

This is the **BERTopic** approach - industry standard for topic modeling:

| Approach | Pros | Cons |
|----------|------|------|
| TF-IDF + KMeans | Fast, simple | Poor clusters, no semantic understanding |
| UMAP + HDBSCAN | Semantic clusters, handles noise, no k needed | Slower, requires embeddings |

**Key advantages:**
- **UMAP** preserves semantic neighborhoods during dimensionality reduction
- **HDBSCAN** finds natural cluster boundaries without forcing a cluster count
- **Noise handling** - segments that don't fit are marked as noise, not forced into bad clusters

## Ollama API Endpoints Used

### 1. List Models
```http
GET http://localhost:11434/api/tags
```
Used to verify required models are installed.

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

**Context Limits:**
- `nomic-embed-text` has ~2048 token context (~6000 characters)
- Segments are truncated to 4000 characters
- Sliding windows truncated to 3000 characters

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

## Models Used

| Model | Purpose | Size | Dimensions |
|-------|---------|------|------------|
| `nomic-embed-text` | Semantic embeddings | 274 MB | 768 |
| `mistral:7b` | Cluster naming | 4.1 GB | N/A |

## Data Model

### ConversationSegment (Silver tier)
Topic-coherent chunks within conversations:
```
- ConversationSegmentId (PK)
- ConversationId (FK → Conversation)
- SegmentIndex (position within conversation)
- StartMessageIndex, EndMessageIndex
- MessageCount
- ContentHash (SHA-256 for change detection)
- CreatedAtUtc
```

### SegmentEmbedding (Gold tier)
Cached 768-dimensional vectors:
```
- SegmentEmbeddingId (PK)
- ConversationSegmentId (FK)
- EmbeddingModel ("nomic-embed-text")
- Dimensions (768)
- EmbeddingVector (binary, 3072 bytes)
- SourceContentHash (for cache invalidation)
```

## Current Implementation

### Key Services

| Service | Purpose |
|---------|---------|
| `OllamaService` | Low-level Ollama API wrapper |
| `SegmentationService` | Splits conversations using embedding similarity |
| `EmbeddingCacheService` | Manages segment embedding storage |
| `ClusteringService` | Orchestrates full pipeline |
| `UmapHdbscanPipeline` | UMAP reduction + HDBSCAN clustering |

### Segmentation Algorithm (Sliding Window)

```csharp
// For each conversation:
1. Load messages (filter profile context, system messages)
2. Create sliding windows of N messages (default: 4)
3. Generate embedding for each window
4. Calculate cosine similarity between consecutive windows
5. When similarity < 0.55, mark as segment boundary
6. Minimum segment: 3 messages
7. Maximum segment: 50 messages (force split)
```

### UMAP Configuration

```csharp
var umap = new Umap(
    distance: DistanceFunction.CosineDistance,
    dimensions: 15,        // Reduce from 768 to 15
    numberOfNeighbors: 15,
    minimumDistance: 0.1f,
    random: new Random(42) // Deterministic
);
```

### HDBSCAN Configuration

```csharp
var hdbscan = new HdbscanRunner(new HdbscanParameters<double[]>
{
    MinPoints = 5,
    MinClusterSize = 8,  // Tuned from 5 to reduce duplicates
    DistanceFunction = new CosineDistance()
});
```

## Performance Characteristics

| Operation | Duration | Notes |
|-----------|----------|-------|
| Full Reset (all phases) | 30-60 min | Regenerates segments + embeddings |
| Re-cluster (Fast) | ~30 sec | Uses cached embeddings |
| Visualization | ~4 sec | UMAP to 2D + Plotly render |

**Current Dataset:**
- 1,293 conversations
- ~1,624 segments
- 56 clusters (MinClusterSize=8)
- ~400 noise segments (24.6%)

**Bottleneck:** Embedding generation during Full Reset (sequential Ollama calls)

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

## Cluster Naming Prompt

The LLM names clusters by finding common themes:

```
You are an expert at identifying themes. Below are 12 text snippets
from a cluster of 45 conversation segments. Find ONE common theme
that connects them. Respond with ONLY a 2-5 word label, no explanation.

Sample 1: "..."
Sample 2: "..."
...
```

## Known Issues & Mitigations

### Issue 1: Duplicate Cluster Names
Some clusters get similar names (e.g., multiple "Black Ember" clusters).

**Mitigation:** Increased MinClusterSize from 5 to 8 (reduced 85 → 56 clusters)

### Issue 2: Noise Segments (~25%)
Some segments don't cluster well.

**Why it's okay:** HDBSCAN is designed to identify noise rather than force-fit.
These are often one-off topics or short conversations.

### Issue 3: Context Length Errors
Some segments exceed embedding model's context window.

**Mitigation:** Truncate segment content to 4000 characters

## Future Improvements

1. **Similarity Detection** - Use segment embeddings to find "Have I solved this before?"
2. **Blog Topic Suggestions** - Identify clusters suitable for blog posts
3. **Batch Embedding** - Parallel Ollama calls for faster Full Reset
4. **Drift Detection** - Track topic changes over time within projects

## References

- [Ollama API Documentation](https://github.com/ollama/ollama/blob/main/docs/api.md)
- [OllamaSharp NuGet Package](https://www.nuget.org/packages/OllamaSharp)
- [nomic-embed-text Model](https://ollama.com/library/nomic-embed-text)
- [Mistral 7B Model](https://ollama.com/library/mistral)
- [UMAP Algorithm](https://umap-learn.readthedocs.io/)
- [HDBSCAN Algorithm](https://hdbscan.readthedocs.io/)
- [BERTopic](https://maartengr.github.io/BERTopic/) (inspiration for this approach)
