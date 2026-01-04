# ChatLake Performance Issues

This document details performance bottlenecks identified through unit testing that prevent successful parsing of large conversation exports (e.g., 200MB ChatGPT exports).

## Overview

The current import pipeline has several architectural issues that cause memory exhaustion and excessive database round trips when processing large files. These issues were identified through diagnostic unit tests in the `ChatLake.Tests.Core` project.

---

## Issue 1: Parser Does Not Stream (Critical)

**Severity:** Critical
**Location:** `src/ChatLake.Infrastructure/Parsing/ChatGpt/ChatGptConversationsJsonParser.cs:21-28`

### Current Implementation

```csharp
public async IAsyncEnumerable<ParsedConversation> ParseAsync(Stream jsonStream, ...)
{
    using var doc = await JsonDocument.ParseAsync(jsonStream, ...);  // Loads entire file

    foreach (var convEl in doc.RootElement.EnumerateArray())
    {
        yield return ParseSingleConversation(convEl);
    }
}
```

### Problem

Despite returning `IAsyncEnumerable<ParsedConversation>`, the parser loads the **entire JSON file** into memory as a `JsonDocument` DOM before yielding any results.

For a 200MB file:
- ~200MB for the JSON string
- ~400-800MB for the JsonDocument DOM tree
- Total: **600MB-1GB+ memory** before first conversation is available

### Test Evidence

`ChatGptParserPerformanceTests.TimeToFirstYield_ShouldNotScaleWithFileSize`:
- 5000 conversations: **243ms** to first yield
- A true streaming parser would yield in <10ms regardless of file size

### Recommended Fix

Use `System.Text.Json.Utf8JsonReader` for true streaming, or restructure to parse conversations incrementally:

```csharp
public async IAsyncEnumerable<ParsedConversation> ParseAsync(Stream jsonStream, ...)
{
    // Option 1: Use JsonSerializer.DeserializeAsyncEnumerable (requires wrapper type)
    // Option 2: Use Utf8JsonReader with manual state management
    // Option 3: Use System.Text.Json source generators for streaming
}
```

### Acceptance Criteria

- [ ] First conversation yields within 100ms regardless of file size
- [ ] Memory usage remains constant (~50MB) regardless of file size
- [ ] Existing parser contract tests continue to pass

---

## Issue 2: Memory Duplication in Pipeline

**Severity:** High
**Location:** `src/ChatLake.Infrastructure/Importing/Services/IngestionPipeline.cs:44-45`

### Current Implementation

```csharp
await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(artifact.RawJson));
```

### Problem

The `RawJson` property contains the entire 200MB file as a string. This line:
1. Converts the string to a byte array (another 200MB allocation)
2. Wraps it in a MemoryStream

Combined with Issue 1, memory usage becomes:
- Original `RawJson` string: ~200MB
- UTF8 byte array: ~200MB
- JsonDocument DOM: ~400-800MB
- **Total: 800MB-1.2GB**

### Recommended Fix

Store large artifacts on the filesystem and stream directly:

```csharp
// Use StoredPath for large files
if (!string.IsNullOrEmpty(artifact.StoredPath))
{
    await using var stream = File.OpenRead(artifact.StoredPath);
    await foreach (var convo in parser.ParseAsync(stream)) { ... }
}
```

### Acceptance Criteria

- [ ] Artifacts over a threshold (e.g., 10MB) are stored on filesystem
- [ ] Pipeline streams from file instead of loading into memory
- [ ] RawJson property is null for file-stored artifacts

---

## Issue 3: N+1 Query Pattern in Pipeline

**Severity:** High
**Location:** `src/ChatLake.Infrastructure/Importing/Services/IngestionPipeline.cs:47-61`

### Current Implementation

```csharp
await foreach (var convo in parser.ParseAsync(stream))
{
    // Called once per conversation
    await _conversationIngestion.IngestAsync(batchId, artifactId, new[] { convo });

    // Query to get back the ID we just created
    var conversationId = await _db.Conversations
        .Where(c => c.ExternalConversationId == convo.ExternalConversationId)
        .Select(c => c.ConversationId)
        .SingleAsync();

    // Rebuild summary for each conversation
    await _summaryBuilder.RebuildAsync(conversationId);
}
```

### Problem

For N conversations, this performs:
- N calls to `IngestAsync`
- N queries to retrieve `ConversationId`
- N calls to `RebuildAsync`
- **Total: 3N+ database operations**

A 200MB file with 5000 conversations = **15,000+ database operations**.

### Test Evidence

`IngestionPipelineTests.IngestRawArtifact_CallsIngestionOncePerConversation`:
- Confirms IngestAsync is called N times for N conversations

### Recommended Fix

Batch operations:

```csharp
const int BatchSize = 100;
var batch = new List<ParsedConversation>(BatchSize);

await foreach (var convo in parser.ParseAsync(stream))
{
    batch.Add(convo);

    if (batch.Count >= BatchSize)
    {
        var ids = await _conversationIngestion.IngestAsync(batchId, artifactId, batch);
        await _summaryBuilder.RebuildAsync(ids);
        batch.Clear();
    }
}

// Process remaining
if (batch.Count > 0)
{
    var ids = await _conversationIngestion.IngestAsync(batchId, artifactId, batch);
    await _summaryBuilder.RebuildAsync(ids);
}
```

### Acceptance Criteria

- [ ] Conversations are batched (configurable batch size, default 100)
- [ ] `IConversationIngestionService.IngestAsync` returns created IDs
- [ ] `IConversationSummaryBuilder` supports batch rebuilding
- [ ] Total database operations reduced from 3N to ~3*(N/BatchSize)

---

## Issue 4: Multiple SaveChanges Per Conversation

**Severity:** Medium
**Location:** `src/ChatLake.Infrastructure/Conversations/Services/ConversationIngestionService.cs:50,77`

### Current Implementation

```csharp
foreach (var parsed in parsedConversations)
{
    // ... create conversation entity ...
    _db.Conversations.Add(conversation);
    await _db.SaveChangesAsync();  // Save #1

    // ... add messages ...
    foreach (var msg in parsed.Messages)
    {
        _db.Messages.Add(new Message { ... });
    }

    await _db.SaveChangesAsync();  // Save #2
}
```

### Problem

Each conversation triggers **2 database round trips**, even when ingesting multiple conversations in a batch.

### Test Evidence

`ConversationIngestionServiceTests.IngestAsync_LargeBatch_CompletesSuccessfully`:
- 200 conversations with 20 messages each: **753ms**
- With batched SaveChanges, this could be <100ms

### Recommended Fix

Accumulate all changes and save once:

```csharp
foreach (var parsed in parsedConversations)
{
    var conversation = new Conversation { ... };
    _db.Conversations.Add(conversation);

    foreach (var msg in parsed.Messages)
    {
        _db.Messages.Add(new Message { Conversation = conversation, ... });
    }
}

await _db.SaveChangesAsync();  // Single save for all entities
```

### Acceptance Criteria

- [ ] Single `SaveChangesAsync` call per batch
- [ ] Use navigation properties to link entities before save
- [ ] Handle constraint violations at batch level

---

## Issue 5: Interface Doesn't Return Created IDs

**Severity:** Low
**Location:** `src/ChatLake.Core/Services/IConversationIngestionService.cs`

### Current Implementation

```csharp
public interface IConversationIngestionService
{
    Task IngestAsync(
        long importBatchId,
        long rawArtifactId,
        IReadOnlyCollection<ParsedConversation> parsedConversations);
}
```

### Problem

The pipeline must query the database to retrieve IDs after ingestion:

```csharp
var conversationId = await _db.Conversations
    .Where(c => c.ExternalConversationId == convo.ExternalConversationId)
    .SingleAsync();
```

### Recommended Fix

Return the created/existing conversation IDs:

```csharp
public interface IConversationIngestionService
{
    Task<IReadOnlyCollection<long>> IngestAsync(
        long importBatchId,
        long rawArtifactId,
        IReadOnlyCollection<ParsedConversation> parsedConversations);
}
```

### Acceptance Criteria

- [ ] `IngestAsync` returns list of conversation IDs
- [ ] Pipeline uses returned IDs instead of querying
- [ ] Order of returned IDs matches input order

---

## Implementation Priority

| Priority | Issue | Impact | Effort |
|----------|-------|--------|--------|
| 1 | Parser streaming | Critical - blocks 200MB files | High |
| 2 | Pipeline batching | High - 15000 DB ops → 150 | Medium |
| 3 | Single SaveChanges | Medium - 2x round trips | Low |
| 4 | File-based storage | High - memory duplication | Medium |
| 5 | Return IDs | Low - extra queries | Low |

## Test Coverage

The following test classes verify these issues and will validate fixes:

- `ChatLake.Tests.Core.Parsing.ChatGptParserPerformanceTests`
- `ChatLake.Tests.Core.Pipeline.IngestionPipelineTests`
- `ChatLake.Tests.Core.Services.ConversationIngestionServiceTests`

Run tests with:
```bash
dotnet test tests/ChatLake.Tests.Core
```

---

## Related Files

- `src/ChatLake.Infrastructure/Parsing/ChatGpt/ChatGptConversationsJsonParser.cs`
- `src/ChatLake.Infrastructure/Importing/Services/IngestionPipeline.cs`
- `src/ChatLake.Infrastructure/Conversations/Services/ConversationIngestionService.cs`
- `src/ChatLake.Core/Services/IConversationIngestionService.cs`
- `src/ChatLake.Core/Services/IConversationSummaryBuilder.cs`
