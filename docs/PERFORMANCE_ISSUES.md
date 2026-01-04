# ChatLake Performance Issues

This document details performance bottlenecks identified through unit testing that prevent successful parsing of large conversation exports (e.g., 200MB ChatGPT exports).

## Overview

The import pipeline had several architectural issues that caused memory exhaustion and excessive database round trips when processing large files. These issues were identified through diagnostic unit tests in the `ChatLake.Tests.Core` project.

---

## Issue 1: Parser Creates Massive DOM ✅ FIXED

**Severity:** Critical
**Status:** ✅ Fixed
**Location:** `src/ChatLake.Infrastructure/Parsing/ChatGpt/ChatGptConversationsJsonParser.cs`

### Previous Implementation

```csharp
using var doc = await JsonDocument.ParseAsync(jsonStream, ...);  // Loads entire file as DOM
```

### Problem

The parser loaded the **entire JSON file** into memory as a `JsonDocument` DOM before yielding any results.

For a 200MB file:
- ~200MB for the JSON bytes
- ~400-800MB for the JsonDocument DOM tree
- Total: **600MB-1GB+ memory**

### Fix Applied

Now uses `Utf8JsonReader` with `JsonDocument.ParseValue` for element-by-element parsing:

```csharp
var reader = new Utf8JsonReader(bytes, readerOptions);
while (reader.Read())
{
    if (reader.TokenType == JsonTokenType.StartObject)
    {
        using var doc = JsonDocument.ParseValue(ref reader);  // Parse ONE conversation
        var parsed = ParseSingleConversation(doc.RootElement);
        if (parsed != null) results.Add(parsed);
    }
}
```

### Memory Improvement

- Previous: ~200MB bytes + ~600MB DOM = **800MB+**
- Now: ~200MB bytes + ~100KB (single conversation DOM) = **~200MB**

### Acceptance Criteria

- [x] Avoids massive DOM allocation for entire file
- [x] Memory usage proportional to file size, not DOM size
- [x] Existing parser contract tests continue to pass

---

## Issue 2: Memory Duplication in Pipeline ✅ FIXED

**Severity:** High
**Status:** ✅ Fixed
**Location:** `src/ChatLake.Infrastructure/Importing/Services/IngestionPipeline.cs`

### Previous Implementation

```csharp
await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(artifact.RawJson));
```

### Fix Applied

Pipeline now supports file-based streaming via `StoredPath`:

```csharp
private static async Task<Stream> OpenArtifactStreamAsync(RawArtifact artifact)
{
    // Prefer filesystem storage for large artifacts
    if (!string.IsNullOrEmpty(artifact.StoredPath) && File.Exists(artifact.StoredPath))
    {
        return new FileStream(artifact.StoredPath, FileMode.Open, FileAccess.Read, ...);
    }
    // Fall back to RawJson for backwards compatibility
    ...
}
```

### Acceptance Criteria

- [x] Pipeline streams from file when `StoredPath` is set
- [x] Falls back to `RawJson` for backwards compatibility
- [ ] **TODO:** Upload service should store large files to filesystem

---

## Issue 3: N+1 Query Pattern in Pipeline ⏳ PENDING

**Severity:** High
**Status:** ⏳ Pending
**Location:** `src/ChatLake.Infrastructure/Importing/Services/IngestionPipeline.cs:46-61`

### Current Implementation

```csharp
await foreach (var convo in parser.ParseAsync(stream))
{
    await _conversationIngestion.IngestAsync(..., new[] { convo });  // N calls
    var conversationId = await _db.Conversations...SingleAsync();    // N queries
    await _summaryBuilder.RebuildAsync(conversationId);              // N rebuilds
}
```

### Problem

For N conversations: 3N+ database operations.

### Recommended Fix

Batch operations (100 conversations per batch).

### Acceptance Criteria

- [ ] Conversations are batched (configurable batch size)
- [ ] `IConversationIngestionService.IngestAsync` returns created IDs
- [ ] `IConversationSummaryBuilder` supports batch rebuilding

---

## Issue 4: Multiple SaveChanges Per Conversation ⏳ PENDING

**Severity:** Medium
**Status:** ⏳ Pending
**Location:** `src/ChatLake.Infrastructure/Conversations/Services/ConversationIngestionService.cs:50,77`

### Problem

Each conversation triggers **2 database round trips** (SaveChanges at lines 50 and 77).

### Recommended Fix

Single `SaveChangesAsync` at end of batch.

---

## Issue 5: Interface Doesn't Return Created IDs ⏳ PENDING

**Severity:** Low
**Status:** ⏳ Pending

### Recommended Fix

Change `IngestAsync` to return `Task<IReadOnlyCollection<long>>`.

---

## Implementation Status

| Priority | Issue | Status | Impact |
|----------|-------|--------|--------|
| 1 | Parser DOM avoidance | ✅ Fixed | Critical - enables 200MB files |
| 2 | File-based streaming | ✅ Fixed | High - reduces memory duplication |
| 3 | Pipeline batching | ⏳ Pending | High - 15000 DB ops → 150 |
| 4 | Single SaveChanges | ⏳ Pending | Medium - 2x round trips |
| 5 | Return IDs | ⏳ Pending | Low - extra queries |

## Test Coverage

All 30 tests pass:

```bash
dotnet test tests/ChatLake.Tests.Core
# Total tests: 30, Passed: 30
```

Test classes:
- `ChatLake.Tests.Core.Parsing.ChatGptParserPerformanceTests` - 6 tests
- `ChatLake.Tests.Core.Pipeline.IngestionPipelineTests` - 6 tests
- `ChatLake.Tests.Core.Services.ConversationIngestionServiceTests` - 10 tests
- Parser contract tests - 8 tests

---

## Next Steps

To complete large file support:

1. **Upload Service:** Store large files (>10MB) to filesystem with `StoredPath`
2. **Pipeline Batching:** Batch conversations for bulk database operations
3. **Ingestion Optimization:** Single SaveChanges per batch

---

## Related Files

- `src/ChatLake.Infrastructure/Parsing/ChatGpt/ChatGptConversationsJsonParser.cs` ✅ Updated
- `src/ChatLake.Infrastructure/Importing/Services/IngestionPipeline.cs` ✅ Updated
- `src/ChatLake.Infrastructure/Conversations/Services/ConversationIngestionService.cs`
- `src/ChatLake.Core/Services/IConversationIngestionService.cs`
- `src/ChatLake.Core/Services/IConversationSummaryBuilder.cs`
