Below is a **clean review + concrete next steps**, based strictly on the three attached documents and the current state of the codebase. No speculation.

---

## 1. What the three documents establish (and where you are aligned)

### DELIVERY_PLAN.md

You are explicitly following a **Bronze → Silver → Gold** data-lake model with:

* immutable raw imports
* deterministic rebuildability
* idempotent normalization
* discardable derived layers

Your recent changes (streaming parser, async ingestion, raw artifact first) are **correctly aligned** with this plan. The decision to stop the previous in-memory parse was the right call. 

---

### MSSQL_SCHEMA.md

This document is the *authoritative truth* for:

* **idempotency strategy**
* **message fingerprinting**
* **import provenance**
* **what must be enforced by constraints vs code**

Important alignment points:

* RawArtifact is Bronze and immutable
* Conversation/Message are Silver
* Fingerprints (hashes) are the real dedupe mechanism
* ImportBatch + observation tables provide auditability

Your current pipeline is **missing only one thing conceptually**:

> a **streamed normalization loop that commits incrementally instead of buffering conversations**

That’s a performance/architecture issue, not a design flaw. 

---

### PROJECT_REFERENCE.md

This confirms:

* the system is single-user, private-first
* imports can be very large
* long-running background operations are expected
* UI must be *honest* about work in progress

This directly explains why the current “Import” UX felt broken: the backend was doing work, but the UI had no execution model for it. 

---

## 2. Precise diagnosis of the current state

### What is now **correct**

* Razor Pages binding issue is resolved
* RawArtifact insert works
* Parser has moved to `IAsyncEnumerable`
* Memory blow-up from full JSON materialization is avoided
* Tests were updated to async contract
* NullRawArtifactParser is now a true no-op

### What is **still missing**

1. **IngestionPipeline is not streaming**

   * It still *conceptually* assumes “parse all → ingest all”
   * Even if the parser streams, the pipeline must also stream

2. **No unit of progress**

   * No batching
   * No per-conversation commits
   * No progress reporting
   * No cancellation boundary surfaced to UI

3. **UI executes a long-running request synchronously**

   * Browser waits
   * No status feedback
   * No resilience

None of these are bugs. They are **expected gaps at this milestone**.

---

## 3. Required next steps (in strict priority order)

### Step 1 — Make ingestion truly streaming (mandatory)

Refactor `IngestionPipeline` so it:

* consumes `await foreach (var convo in parser.ParseAsync(...))`
* ingests **one conversation at a time**
* commits per conversation or per small batch
* does *not* hold the full parsed set in memory

This directly satisfies the performance requirements in **MSSQL_SCHEMA §7**. 

---

### Step 2 — Add an ImportExecution model (mandatory)

You need a lightweight execution tracker:

* ImportBatch already exists → extend its usage
* Add:

  * `ProcessedConversationCount`
  * `LastHeartbeatUtc`
  * `Status = Running | Completed | Failed`

This is required by both DELIVERY_PLAN and PROJECT_REFERENCE.

---

### Step 3 — Move import off the request thread (mandatory)

The current `OnPostAsync` model is **not viable long-term**.

Options (choose one, in this order of recommendation):

1. BackgroundService / Channel-based queue
2. Hosted service triggered by DB row
3. Minimal Hangfire-style worker (later)

UI should:

* enqueue import
* redirect immediately
* poll status

This is explicitly implied by the roadmap. 

---

### Step 4 — Enforce fingerprint-based idempotency (next milestone)

Once streaming is stable:

* implement `MessageFingerprintSha256` exactly as defined
* add the unique constraint
* add idempotency tests

Do **not** do this before streaming is correct.

---

## 4. What not to touch yet (important)

* Gold / ML tables → intentionally future
* ProjectSuggestion / Drift → future
* Attachments → metadata only later
* Embeddings → Phase 2

The documents are very clear on this sequencing.

---

## 5. Bottom line

You are **on the correct architectural path**.

The failure mode you hit (huge file, CPU/memory usage, no visible progress) is *exactly* what happens when:

* parsing becomes async
* but ingestion and UI are still synchronous

The fix is architectural, not corrective.

If you want, next we should:

* refactor `IngestionPipeline` to a fully streaming version
* then design the minimal background import runner
* then wire progress into the UI

Those are the next three commits.
