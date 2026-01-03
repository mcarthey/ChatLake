# ChatLake: MSSQL Schema + Import Idempotency Strategy
Version: 0.1  
Applies to: ASP.NET MVC + Razor, MSSQL, ML.NET  
Scope: Bronze/Silver/Gold + Overrides + Auditability + Reimports

---

## 1. Design Goals

1. **Immutable raw storage (Bronze)**: every import is preserved, never overwritten.
2. **Deterministic normalization (Silver)**: rebuildable from Bronze.
3. **Derived/ML outputs (Gold)**: versioned, discardable, reproducible.
4. **Reimport-safe**: repeated imports do not create uncontrolled duplicates.
5. **User overrides persist** across reruns and reimports.

---

## 2. Naming and Conventions

- Schema: `dbo` (initially). Optionally split later into `lake` / `curated` / `derived`.
- PKs: `bigint IDENTITY(1,1)` unless explicitly otherwise.
- Timestamps: `datetime2(3)` (UTC).
- Text: `nvarchar(max)` for message content; `nvarchar(4000)` for titles/labels.
- Hashes: `binary(32)` for SHA-256; store as `varbinary(32)` or `binary(32)` (prefer `binary(32)` fixed).
- JSON: store raw payload as `nvarchar(max)` (JSON). If SQL Server 2022+ JSON functions are available, still store raw as text and parse in app layer.

---

## 3. Bronze (Raw / Immutable)

### 3.1 ImportBatch
Tracks each import execution (zip/file drop or export set).

**Table: `ImportBatch`**
- `ImportBatchId` (bigint, PK, identity)
- `SourceSystem` (nvarchar(50)) — e.g., `ChatGPTExport`
- `SourceVersion` (nvarchar(50), null) — export format version if detected
- `ImportedAtUtc` (datetime2(3), not null, default sysutcdatetime())
- `ImportedBy` (nvarchar(200), null) — username/identity
- `ImportLabel` (nvarchar(200), null) — user label (e.g., “Jan 2026 export”)
- `ArtifactCount` (int, not null, default 0)
- `Notes` (nvarchar(2000), null)
- `Status` (nvarchar(20), not null) — `Staged|Committed|Failed`

**Indexes**
- IX_ImportBatch_ImportedAtUtc (`ImportedAtUtc`)

---

### 3.2 RawArtifact
Stores each raw file/blob as an immutable record with hash-based identity.

**Table: `RawArtifact`**
- `RawArtifactId` (bigint, PK, identity)
- `ImportBatchId` (bigint, FK → ImportBatch)
- `ArtifactType` (nvarchar(50)) — `ConversationsJson|AttachmentsManifest|Other`
- `ArtifactName` (nvarchar(260)) — original filename
- `ContentType` (nvarchar(100), null)
- `ByteLength` (bigint, not null)
- `Sha256` (binary(32), not null)
- `RawJson` (nvarchar(max), null) — for JSON artifacts
- `StoredPath` (nvarchar(500), null) — if storing file on disk instead of DB (optional)
- `CreatedAtUtc` (datetime2(3), not null, default sysutcdatetime())

**Constraints**
- UQ_RawArtifact_Sha256 unique (`Sha256`) **optional**:
  - If enabled, it enforces global uniqueness of artifacts by hash (prevents storing identical artifacts twice).
  - If you prefer to keep duplicates for provenance, omit this and enforce uniqueness at (ImportBatchId, Sha256).

**Indexes**
- IX_RawArtifact_ImportBatchId (`ImportBatchId`)
- IX_RawArtifact_Sha256 (`Sha256`)

---

## 4. Silver (Normalized / Curated)

### 4.1 Conversation
Represents a single chat thread.

**Table: `Conversation`**
- `ConversationId` (bigint, PK, identity)
- `SourceConversationKey` (nvarchar(200), not null) — stable key from export if present; else derived
- `Title` (nvarchar(500), null)
- `CreatedAtUtc` (datetime2(3), null)
- `UpdatedAtUtc` (datetime2(3), null)
- `FirstMessageAtUtc` (datetime2(3), null)
- `LastMessageAtUtc` (datetime2(3), null)
- `ImportedFirstBatchId` (bigint, FK → ImportBatch)
- `ImportedLastBatchId` (bigint, FK → ImportBatch)
- `SourceArtifactId` (bigint, FK → RawArtifact) — where it came from (first seen)
- `IsDeletedInSource` (bit, not null, default 0) — if future exports indicate deletion (optional)
- `ConversationSha256` (binary(32), null) — optional canonical hash after normalization

**Constraints**
- UQ_Conversation_SourceConversationKey unique (`SourceConversationKey`)

**Indexes**
- IX_Conversation_LastMessageAtUtc (`LastMessageAtUtc`)
- IX_Conversation_ImportedLastBatchId (`ImportedLastBatchId`)

---

### 4.2 Message
Represents each message turn (user/assistant/system/tool). This is the core table.

**Table: `Message`**
- `MessageId` (bigint, PK, identity)
- `ConversationId` (bigint, FK → Conversation, not null)
- `SourceMessageKey` (nvarchar(200), null) — stable if provided by export; else null
- `Role` (nvarchar(20), not null) — `user|assistant|system|tool`
- `SequenceIndex` (int, not null) — deterministic ordering within conversation
- `CreatedAtUtc` (datetime2(3), null) — message timestamp if present
- `Text` (nvarchar(max), null)
- `ContentJson` (nvarchar(max), null) — raw structured content for message (optional)
- `TextSha256` (binary(32), null) — hash of normalized text (see idempotency)
- `MessageFingerprintSha256` (binary(32), not null) — canonical fingerprint (see below)
- `ImportedFirstBatchId` (bigint, FK → ImportBatch, not null)
- `ImportedLastBatchId` (bigint, FK → ImportBatch, not null)

**Constraints**
- UQ_Message_Conversation_Sequence unique (`ConversationId`, `SequenceIndex`)
- UQ_Message_Fingerprint unique (`MessageFingerprintSha256`) **recommended**
  - This is the key idempotency control when no stable message IDs exist.

**Indexes**
- IX_Message_ConversationId (`ConversationId`)
- IX_Message_CreatedAtUtc (`CreatedAtUtc`)
- IX_Message_Role (`Role`)
- Full-text index on `Text` (optional but strongly recommended for search use cases)

---

### 4.3 Attachment (metadata only initially)
**Table: `Attachment`**
- `AttachmentId` (bigint, PK, identity)
- `MessageId` (bigint, FK → Message)
- `SourceAttachmentKey` (nvarchar(200), null)
- `FileName` (nvarchar(260), null)
- `ContentType` (nvarchar(100), null)
- `ByteLength` (bigint, null)
- `Sha256` (binary(32), null)
- `StoredPath` (nvarchar(500), null)
- `CreatedAtUtc` (datetime2(3), not null, default sysutcdatetime())

**Indexes**
- IX_Attachment_MessageId (`MessageId`)
- IX_Attachment_Sha256 (`Sha256`)

---

### 4.4 ImportMap (Provenance mapping)
Tracks which conversations/messages were observed in which import batch, for auditing and incremental updates.

**Table: `ImportObservation`**
- `ImportObservationId` (bigint, PK, identity)
- `ImportBatchId` (bigint, FK → ImportBatch, not null)
- `ConversationId` (bigint, FK → Conversation, not null)
- `MessageId` (bigint, FK → Message, null) — optional; keep conversation-level if too large
- `ObservedAtUtc` (datetime2(3), not null, default sysutcdatetime())

**Indexes**
- IX_ImportObservation_Batch (`ImportBatchId`)
- IX_ImportObservation_Conversation (`ConversationId`)
- IX_ImportObservation_Message (`MessageId`)

---

## 5. Gold (Derived / ML Augmentation)

### 5.1 InferenceRun
Every ML/derived computation writes into a run.

**Table: `InferenceRun`**
- `InferenceRunId` (bigint, PK, identity)
- `RunType` (nvarchar(50), not null) — `Clustering|Topics|Embeddings|Similarity|Drift|BlogTopics`
- `ModelName` (nvarchar(200), not null) — e.g., `ChatLake.KMeans.v1`
- `ModelVersion` (nvarchar(50), not null)
- `FeatureConfigHashSha256` (binary(32), not null)
- `InputScope` (nvarchar(50), not null) — `All|ImportBatchRange|Project|ConversationSet`
- `InputDescription` (nvarchar(2000), null)
- `StartedAtUtc` (datetime2(3), not null, default sysutcdatetime())
- `CompletedAtUtc` (datetime2(3), null)
- `Status` (nvarchar(20), not null) — `Running|Completed|Failed`
- `MetricsJson` (nvarchar(max), null) — silhouette score, etc.

**Indexes**
- IX_InferenceRun_StartedAtUtc (`StartedAtUtc`)
- IX_InferenceRun_RunType (`RunType`)

---

### 5.2 Project (curated concept)
Projects are entities that can be user-created or system-suggested.

**Table: `Project`**
- `ProjectId` (bigint, PK, identity)
- `ProjectKey` (nvarchar(200), not null) — slug
- `Name` (nvarchar(500), not null)
- `Description` (nvarchar(2000), null)
- `CreatedAtUtc` (datetime2(3), not null, default sysutcdatetime())
- `CreatedBy` (nvarchar(200), null) — user/system
- `IsSystemGenerated` (bit, not null, default 0)
- `IsActive` (bit, not null, default 1)

**Constraints**
- UQ_Project_ProjectKey unique (`ProjectKey`)

---

### 5.3 ProjectMembership (conversation ↔ project)
**Table: `ProjectConversation`**
- `ProjectConversationId` (bigint, PK, identity)
- `ProjectId` (bigint, FK → Project, not null)
- `ConversationId` (bigint, FK → Conversation, not null)
- `AssignedBy` (nvarchar(50), not null) — `System|User`
- `InferenceRunId` (bigint, FK → InferenceRun, null)
- `Confidence` (decimal(5,4), null) — 0.0000–1.0000
- `AssignedAtUtc` (datetime2(3), not null, default sysutcdatetime())
- `IsCurrent` (bit, not null, default 1) — supports history when reruns occur

**Constraints**
- UQ_ProjectConversation_Current unique (`ProjectId`, `ConversationId`, `IsCurrent`) with filtered unique index where `IsCurrent=1` (SQL Server supports filtered indexes)

**Indexes**
- IX_ProjectConversation_Project (`ProjectId`)
- IX_ProjectConversation_Conversation (`ConversationId`)

---

### 5.4 ProjectSuggestion (inbox)
**Table: `ProjectSuggestion`**
- `ProjectSuggestionId` (bigint, PK, identity)
- `InferenceRunId` (bigint, FK → InferenceRun, not null)
- `SuggestedProjectKey` (nvarchar(200), not null)
- `SuggestedName` (nvarchar(500), not null)
- `Summary` (nvarchar(2000), null)
- `Confidence` (decimal(5,4), not null)
- `Status` (nvarchar(20), not null) — `Pending|Accepted|Rejected|Merged`
- `ResolvedProjectId` (bigint, FK → Project, null)
- `ResolvedAtUtc` (datetime2(3), null)

**Indexes**
- IX_ProjectSuggestion_Status (`Status`)
- IX_ProjectSuggestion_Confidence (`Confidence`)

---

### 5.5 Topic + Assignments
**Table: `Topic`**
- `TopicId` (bigint, PK, identity)
- `InferenceRunId` (bigint, FK → InferenceRun, not null)
- `Label` (nvarchar(200), not null) — e.g., “Klipper”, “EF Core”, “Act II Draft”
- `KeywordsJson` (nvarchar(max), null)

**Table: `ConversationTopic`**
- `ConversationTopicId` (bigint, PK, identity)
- `InferenceRunId` (bigint, FK → InferenceRun, not null)
- `ConversationId` (bigint, FK → Conversation, not null)
- `TopicId` (bigint, FK → Topic, not null)
- `Score` (decimal(7,6), not null)

**Indexes**
- IX_ConversationTopic_Conversation (`ConversationId`)
- IX_ConversationTopic_Topic (`TopicId`)

---

### 5.6 Drift Metrics (topic creep)
**Table: `ProjectDriftMetric`**
- `ProjectDriftMetricId` (bigint, PK, identity)
- `InferenceRunId` (bigint, FK → InferenceRun, not null)
- `ProjectId` (bigint, FK → Project, not null)
- `WindowStartUtc` (datetime2(3), not null)
- `WindowEndUtc` (datetime2(3), not null)
- `DriftScore` (decimal(7,6), not null)
- `DetailsJson` (nvarchar(max), null)

**Indexes**
- IX_ProjectDrift_Project_Window (`ProjectId`, `WindowStartUtc`)

---

### 5.7 Similarity Edges (“solved before”)
**Table: `ConversationSimilarity`**
- `ConversationSimilarityId` (bigint, PK, identity)
- `InferenceRunId` (bigint, FK → InferenceRun, not null)
- `ConversationIdA` (bigint, FK → Conversation, not null)
- `ConversationIdB` (bigint, FK → Conversation, not null)
- `Similarity` (decimal(7,6), not null)
- `Method` (nvarchar(50), not null) — `TfidfCosine|EmbeddingCosine`

**Constraints**
- Enforce ordering (A < B) in app layer to prevent duplicates.
- UQ_Similarity_Pair unique (`InferenceRunId`, `ConversationIdA`, `ConversationIdB`)

**Indexes**
- IX_Similarity_A (`ConversationIdA`)
- IX_Similarity_B (`ConversationIdB`)
- IX_Similarity_Value (`Similarity`)

---

### 5.8 Blog Topic Suggestions (topic discovery)
**Table: `BlogTopicSuggestion`**
- `BlogTopicSuggestionId` (bigint, PK, identity)
- `InferenceRunId` (bigint, FK → InferenceRun, not null)
- `ProjectId` (bigint, FK → Project, null)
- `Title` (nvarchar(500), not null)
- `OutlineJson` (nvarchar(max), null)
- `Confidence` (decimal(5,4), not null)
- `SourceConversationIdsJson` (nvarchar(max), not null) — list of ConversationIds (initial)
- `Status` (nvarchar(20), not null) — `Pending|Approved|Dismissed`

**Indexes**
- IX_BlogTopicSuggestion_Status (`Status`)
- IX_BlogTopicSuggestion_Confidence (`Confidence`)

---

## 6. Overrides (Human-in-the-Loop Persistence)

### 6.1 UserOverride (generic event log)
Captures decisions that must survive reruns.

**Table: `UserOverride`**
- `UserOverrideId` (bigint, PK, identity)
- `OverrideType` (nvarchar(50), not null) — `AcceptProjectSuggestion|RejectProjectSuggestion|ManualProjectAssignment|MergeProjects|SplitProject|RenameProject|SuppressSuggestion`
- `TargetType` (nvarchar(50), not null) — `Project|ProjectSuggestion|Conversation|Topic|BlogTopicSuggestion`
- `TargetId` (bigint, not null)
- `PayloadJson` (nvarchar(max), null)
- `CreatedAtUtc` (datetime2(3), not null, default sysutcdatetime())
- `CreatedBy` (nvarchar(200), null)

**Indexes**
- IX_UserOverride_Target (`TargetType`, `TargetId`)
- IX_UserOverride_CreatedAtUtc (`CreatedAtUtc`)

---

## 7. Import Idempotency Strategy (Critical)

### 7.1 Problem Statement
ChatGPT exports may not provide stable message IDs across exports, and a conversation can appear in multiple export batches. We need to:
- avoid uncontrolled duplicates
- preserve provenance (seen in multiple imports)
- allow safe reimports of the same export

### 7.2 Strategy Overview
Use a **canonical message fingerprint** and enforce uniqueness on that fingerprint.

**MessageFingerprintSha256** is computed from normalized fields:

1. `SourceConversationKey` (or derived stable conversation key)
2. `Role`
3. `CreatedAtUtc` (if present; else blank)
4. `SequenceIndex` (preferred if stable; else derived from ordering)
5. Normalized text (whitespace-collapsed, normalized newlines)
6. Optional: include a stable content-json hash if text is absent

**Fingerprint input example (string to hash):**
```

convKey=<key>|role=user|ts=2025-12-31T12:34:56.789Z|seq=12|textHash=<sha256(text)>

```

Then `MessageFingerprintSha256 = SHA256(UTF8(fingerprintString))`.

**Enforcement**
- Unique index on `MessageFingerprintSha256`
- Unique index on (`ConversationId`, `SequenceIndex`) to keep ordering stable inside the normalized conversation view

### 7.3 When Source IDs Exist
If export provides stable `SourceMessageKey`:
- Prefer uniqueness on (`SourceConversationKey`, `SourceMessageKey`)
- Still compute fingerprint as a fallback and for consistency checks

### 7.4 Deduplication Rules (Deterministic)
On import parse:
1. Upsert `Conversation` by `SourceConversationKey`.
2. For each message:
   - compute fingerprint
   - attempt insert into `Message`
   - if conflict:
     - update `ImportedLastBatchId`
     - do not overwrite text unless the source explicitly differs and you decide to support “source corrections” (Phase 2)
3. Insert `ImportObservation` rows (conversation-level always; message-level optionally)

### 7.5 Handling “Edit-like” Differences Across Exports
If a message appears with same seq/ts/role but text differs:
- Treat as separate message only if fingerprint differs (it will).
- Optionally record a linkage:
  - `MessageRevision` table later (Phase 2) if you want to model edits.

### 7.6 Idempotency Tests (must exist early)
Automated tests should validate:
- Importing the same export twice produces identical row counts (except ImportBatch + ImportObservation).
- Importing export A then export B (overlapping data) only adds genuinely new messages.
- Import order does not change the final normalized dataset (commutative up to timestamps/ImportedLastBatchId).

---

## 8. Schema Evolution and Migrations

- Use EF Core migrations (recommended) with:
  - explicit indices
  - filtered unique indices where needed (e.g., ProjectConversation current membership)
- Treat schema changes as forward-only migrations with rollback scripts if required (enterprise posture).

---

## 9. Performance Notes (pragmatic)

- Full-text index on `Message.Text` is the biggest win for search.
- Keep `MessageFingerprintSha256` indexed and fixed-length.
- Partitioning is optional; revisit after >1M messages.
- Store embeddings later only if needed; they enlarge storage substantially.

---

## 10. Immediate Implementation Tasks (Issue Seeds)

1. Create EF Core models + migrations for Bronze + Silver core tables.
2. Implement fingerprint computation library + unit tests (gold standard for idempotency).
3. Implement ImportBatch workflow with staging → commit.
4. Implement conversation viewer and message ordering using (`ConversationId`, `SequenceIndex`).
5. Add ImportObservation at least at conversation-level for auditability.
