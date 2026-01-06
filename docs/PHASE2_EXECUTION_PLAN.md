# Phase 2 Execution Plan: ML Analysis + UI Improvements

**Status:** IN PROGRESS (with architectural changes)
**Prerequisite:** Phase 1 Complete (200MB import successful, streaming pipeline, progress tracking)
**Goal:** Implement Gold tier ML features with concurrent UI improvements

> **Note:** Implementation diverged from original plan. See `IMPLEMENTATION_STATUS.md` for details.
> Key change: Replaced ML.NET TF-IDF + KMeans with Ollama embeddings + UMAP + HDBSCAN.

---

## Current State Summary

**Completed (Bronze + Silver):**
- Streaming file upload (64KB buffer)
- Element-by-element JSON parsing
- Batched commits with progress tracking
- Import cleanup service
- Conversation list with meaningful summaries
- 1,293 conversations / 41,879 messages imported

**Not Yet Implemented:**
- Message fingerprint idempotency (IMP-04)
- Import observation logging (IMP-06)
- All Gold tier tables and ML features
- Enhanced UI views

---

## Execution Strategy

### Parallel Workstreams

```
Week N     Week N+1   Week N+2   Week N+3   Week N+4
─────────────────────────────────────────────────────
FOUNDATION (sequential, enables everything)
├─ DB-GOLD: Gold tier migrations
└─ ML-01: InferenceRun framework

ML TRACK (after foundation)
├─ ML-02: Clustering + ProjectSuggestion
├─ ML-03: Topic extraction
├─ ML-05: Similarity detection
├─ ML-04: Drift metrics (needs projects)
└─ ML-06: Blog suggestions (needs topics)

UI TRACK (parallel with ML)
├─ UI-05: Conversation detail view
├─ UI-03: Project dashboard
├─ UI-04: Suggested projects inbox
├─ UI-06: Timeline visualizations
└─ UI-07: Blog suggestions view
```

---

## Phase 2A: Foundation (Must Complete First)

### DB-GOLD — Gold Tier Schema Migration ✅ COMPLETE

**Objective:** Create all Gold tier tables from MSSQL_SCHEMA.md

**Tables created:**
1. `InferenceRun` - ML execution tracking ✅
2. `Project` - User/system projects ✅
3. `ProjectConversation` - Conversation ↔ Project mapping ✅
4. `ProjectSuggestion` - Clustering inbox ✅
5. `Topic` - Extracted topics ✅
6. `ConversationTopic` - Topic assignments ✅
7. `ProjectDriftMetric` - Topic creep scores ✅
8. `ConversationSimilarity` - Related conversations ✅
9. `BlogTopicSuggestion` - Publishing candidates ✅

**Additional tables (not in original plan):**
10. `ConversationSegment` - Topic-coherent chunks ✅
11. `SegmentEmbedding` - Cached 768-dim vectors ✅

**Tasks:**
- [x] Create EF Core entities in `ChatLake.Infrastructure/Gold/Entities/`
- [x] Add DbSet properties to ChatLakeDbContext
- [x] Configure relationships and indexes in OnModelCreating
- [x] Generate and apply migration
- [x] Verify schema matches MSSQL_SCHEMA.md exactly

**DoD:** ✅ COMPLETE
- All 11 Gold tables exist with proper FKs, indexes, constraints
- Migration is reversible
- No breaking changes to existing Bronze/Silver tables

---

### ML-01 — InferenceRun Framework ✅ COMPLETE

**Objective:** Standardize ML execution tracking so all derived data is traceable

**Components:**
```
ChatLake.Core/
  Services/
    IInferenceRunService.cs

ChatLake.Infrastructure/
  Gold/
    Services/
      InferenceRunService.cs
```

**Interface:** ✅ Implemented as planned

**RunTypes implemented:**
- `Segmentation` - Topic-coherent segment creation ✅
- `Embedding` - Vector generation via Ollama ✅
- `SegmentClustering` - UMAP + HDBSCAN clustering ✅
- `Topics` - Topic extraction (stub)
- `Similarity` - Conversation similarity edges (stub)
- `Drift` - Topic drift calculation (stub)

**DoD:** ✅ COMPLETE
- InferenceRun rows created for every ML operation
- Status transitions: Running → Completed | Failed
- Metrics stored as JSON for analysis

---

## Phase 2B: Core ML Features

### ML-02 — Baseline Conversation Clustering ✅ COMPLETE (Architecture Changed)

**Objective:** Auto-group conversations into suggested projects

**Original Plan:** ML.NET TF-IDF + KMeans
**Actual Implementation:** Ollama Embeddings + UMAP + HDBSCAN (better results)

**Actual Approach (Segment-Level UMAP + HDBSCAN):**
1. Segment conversations into topic-coherent chunks (SegmentationService)
2. Generate 768-dim embeddings via Ollama nomic-embed-text (EmbeddingCacheService)
3. UMAP dimensionality reduction (768D → 15D)
4. HDBSCAN density clustering (MinClusterSize=8)
5. LLM-generated cluster names via mistral:7b
6. Generate ProjectSuggestion records with segment/conversation counts

**Components:**
```
ChatLake.Inference/
  Clustering/
    UmapHdbscanPipeline.cs        ← Main pipeline
    HdbscanClusteringPipeline.cs  ← Direct HDBSCAN (deprecated)
    EmbeddingClusteringPipeline.cs ← KMeans fallback

ChatLake.Core/
  Services/
    IClusteringService.cs
    ISegmentationService.cs       ← NEW
    IEmbeddingCacheService.cs     ← NEW
    ILlmService.cs                ← NEW

ChatLake.Infrastructure/
  Gold/Services/
    ClusteringService.cs
    OllamaService.cs              ← NEW
  Conversations/Services/
    SegmentationService.cs        ← NEW
```

**Tasks:**
- [x] Add ML.NET packages (Microsoft.ML)
- [x] Add UMAP/HdbscanSharp packages
- [x] Add OllamaSharp package
- [x] Implement segment-level analysis
- [x] Implement UMAP + HDBSCAN pipeline
- [x] Create ProjectSuggestion records with confidence scores
- [x] Interactive cluster visualization (Plotly.js)
- [x] Suggestions grouped by InferenceRun

**DoD:** ✅ COMPLETE
- ProjectSuggestion records created for each cluster
- Confidence scores populated
- Results reproducible (seeded UMAP + HDBSCAN)
- 56 clusters from 1,624 segments (MinClusterSize=8)

---

### ML-03 — Topic Extraction ⏸️ DEFERRED (Replaced by Segment Analysis)

**Objective:** Label conversations with topic keywords

**Status:** The segment-level clustering approach (ML-02) provides better topic discovery
than traditional LDA/TF-IDF keyword extraction. Each cluster IS a topic.

**Original Approach:** TF-IDF keywords + LDA
**Alternative:** Cluster names from LLM serve as topic labels

**Future consideration:**
- Could extract keywords from segment text for search
- Could use cluster names as de-facto topics

**Tasks:**
- [ ] ~Implement keyword extraction~ (deferred)
- [ ] ~Create Topic + ConversationTopic records~ (deferred)
- [ ] Consider: use cluster names as topic labels

**DoD:** ⏸️ DEFERRED
- Segment clustering provides equivalent functionality

---

### ML-05 — Similarity Detection ⚠️ STUB EXISTS (Next Priority)

**Objective:** Enable "Have I solved this before?" via conversation similarity

**Status:** Service stub exists but not functional. Now that we have segment embeddings,
this becomes much easier to implement.

**Updated Approach (using segment embeddings):**
1. Use cached segment embeddings (already have 1,624 × 768-dim vectors)
2. Calculate cosine similarity between segments
3. Store edges where similarity > threshold (e.g., 0.7)
4. Show "Related Segments" from different conversations

**Performance consideration:**
- 1,624 segments = ~1.3M comparisons
- But embeddings already exist (no generation cost)
- Can use approximate nearest neighbors for speed

**Tasks:**
- [ ] Implement pairwise cosine similarity using segment embeddings
- [ ] Store ConversationSimilarity edges
- [ ] Add "Related Conversations" panel to conversation viewer
- [ ] Performance test with full dataset

**DoD:**
- Similar conversations/segments discoverable via API
- UI shows related conversations panel

---

### ML-04 — Drift Metric Calculation ⚠️ STUB EXISTS (Lower Priority)

**Objective:** Detect topic creep within projects over time

**Prerequisite:** ML-02 (clustering) ✅ + Projects with conversations assigned

**Status:** Service stub exists. Requires projects to have conversations assigned first.
Less urgent since segment clustering already groups by topic.

**Updated Approach (using segment embeddings):**
1. For each project, collect segment embeddings by time window
2. Compute centroid embedding per window
3. Calculate drift as cosine distance between window centroids
4. Store ProjectDriftMetric records

**Tasks:**
- [ ] Implement rolling window analysis on segments
- [ ] Calculate drift scores using embedding centroids
- [ ] Store ProjectDriftMetric records
- [ ] Add drift indicator to project detail view

**DoD:**
- Drift scores computed for each project
- Visible in project detail view

---

### ML-06 — Blog Topic Suggestion Engine ❌ NOT STARTED

**Objective:** Identify publishable research arcs

**Prerequisite:** ML-02 (clustering) ✅ + Some project suggestions accepted

**Status:** Not started. Now that clustering works well, this becomes feasible.

**Updated Approach (using clusters as research arcs):**
1. Identify clusters with >N segments spanning >M conversations
2. Use LLM to analyze depth/quality of discussion
3. Generate candidate blog titles/outlines via LLM
4. Store BlogTopicSuggestion records linked to cluster segments

**Tasks:**
- [ ] Implement research arc detection (large coherent clusters)
- [ ] Generate title + outline suggestions via LLM
- [ ] Link to source segments/conversations
- [ ] Create Blog Suggestions UI page

**DoD:**
- Blog suggestions visible in UI
- Traceable to source conversations/segments

---

## Phase 2C: UI Improvements (Parallel Track)

### UI-05 — Enhanced Conversation Viewer ✅ COMPLETE

**Objective:** Full threaded message display with metadata

**Status:** ✅ COMPLETE - Chat-style threaded UI implemented

**Implemented:**
- Threaded message display (user/assistant alternating) ✅
- Message timestamps and roles ✅
- Chat-bubble styling ✅
- Code block formatting ✅

**Remaining (when ML features complete):**
- [ ] Topics sidebar (deferred with ML-03)
- [ ] Related conversations panel (needs ML-05)
- [ ] Project assignment display

**Tasks:**
- [x] Create `/Conversations/Detail/{id}` page
- [x] Render messages in threaded format
- [x] Styling for readability
- [ ] Add related conversations panel (when ML-05 complete)

**DoD:** ✅ CORE COMPLETE
- Conversations fully readable
- Metadata visible
- Related content: pending ML-05

---

### UI-03 — Project Dashboard ✅ BASIC COMPLETE

**Objective:** Primary navigation surface for projects

**Status:** ✅ Basic implementation complete

**Implemented:**
- Project list page ✅
- Project detail view with conversation list ✅
- Conversation count display ✅

**Remaining (when ML features complete):**
- [ ] Timeline (volume over time) - needs ML-04
- [ ] Topic breakdown - deferred with ML-03
- [ ] Drift indicator - needs ML-04

**Tasks:**
- [x] Create `/Projects` list page
- [x] Create `/Projects/Detail/{id}` page
- [x] Add conversation count
- [ ] Add timeline visualization (when ML-04 complete)

**DoD:** ✅ CORE COMPLETE
- Projects navigable
- Key metrics visible at a glance

---

### UI-04 — Suggested Projects Inbox ✅ ENHANCED

**Objective:** Human-in-the-loop project approval

**Prerequisite:** ML-02 (creates suggestions) ✅

**Status:** ✅ COMPLETE with enhancements beyond original plan

**Actions supported:**
- Accept → creates Project + assigns conversations ✅
- Reject → marks suggestion dismissed ✅
- Merge → combines with existing project ✅
- Clear All → removes all pending suggestions ✅

**Enhancements beyond original plan:**
- Grouped by InferenceRun with collapsible `<details>` sections
- "Latest" badge on most recent run
- Run metadata (timestamp, segment count, noise count)
- Re-cluster (Fast) vs Full Reset (Slow) buttons
- Segment count + conversation count display

**Tasks:**
- [x] Create `/Projects/Suggestions` inbox page
- [x] Display cluster preview (sample conversations)
- [x] Accept/Reject/Merge buttons with API calls
- [x] Group suggestions by inference run
- [ ] Persist decisions in UserOverride table (deferred)

**DoD:** ✅ COMPLETE
- User can accept/reject all suggestions
- Suggestions grouped by run for clarity

---

### UI-06 — Visualizations ✅ CHANGED (Cluster Viz Instead of Timeline)

**Objective:** Visual analytics for cognitive telemetry

**Original Plan:** Timeline + topic bands + drift overlays
**Actual Implementation:** Interactive cluster visualization (Plotly.js)

**Status:** ✅ COMPLETE (different approach than planned)

**What was built instead:**
- 2D UMAP projection of all segment embeddings
- Each cluster displayed as distinct color
- Noise points shown in gray background
- Hover tooltips with segment previews
- Interactive legend with cluster names and counts
- Statistics panel: total segments, clusters, noise %, UMAP time
- localStorage caching for instant re-navigation

**Why the change:**
- Cluster visualization provides immediate insight into topic groupings
- Timeline requires ML-04 (drift) which is deferred
- UMAP projection shows semantic relationships directly

**Tasks:**
- [x] Choose charting library (Plotly.js)
- [x] Implement cluster visualization
- [x] Add localStorage caching
- [ ] Volume timeline (deferred until ML-04)
- [ ] Topic bands (deferred - segments replace topics)
- [ ] Drift overlay (deferred until ML-04)

**DoD:** ✅ COMPLETE (cluster viz)
- Charts render accurately
- Interactive (hover for details)
- Cached for fast navigation

---

### UI-07 — Blog Topic Suggestions View ❌ NOT STARTED

**Objective:** Support publishing workflow

**Prerequisite:** ML-06 (Blog Suggestions) ❌

**Status:** ❌ NOT STARTED - Blocked on ML-06

**Components:**
- List of suggestions with confidence
- Outline preview
- Source conversation links
- Status management (Pending → Approved/Dismissed)

**Tasks:**
- [ ] Create `/Blog/Suggestions` page
- [ ] Display outline + sources
- [ ] Status toggle buttons

**DoD:**
- Blog suggestions browsable without SQL access

---

## Phase 2D: Deferred Items (Post-BAT)

### IMP-04 — Message Fingerprint Idempotency

**Status:** Deferred until reimport scenarios are common

**Why deferred:**
- Current use case is single import, not repeated imports
- Fingerprint logic is complex and needs careful testing
- Can be added without schema changes (MessageFingerprintSha256 column exists)

### IMP-06 — Import Observation Logging

**Status:** Deferred, low priority

**Why deferred:**
- Adds storage overhead
- Useful for auditing but not blocking

### UI-01 — Authentication

**Status:** Deferred for personal instance

**Why deferred:**
- Single-user on localhost
- Add when deploying to server with network access

---

## Recommended Execution Order

### Sprint 1: Foundation
1. **DB-GOLD** — Create all Gold tier tables
2. **ML-01** — InferenceRun framework
3. **UI-05** — Enhanced conversation viewer (can start without ML)

### Sprint 2: Core ML
4. **ML-02** — Clustering + ProjectSuggestion
5. **UI-03** — Project dashboard (basic, pre-clustering)
6. **UI-04** — Suggested projects inbox

### Sprint 3: Topics + Similarity
7. **ML-03** — Topic extraction
8. **ML-05** — Similarity detection
9. Update **UI-05** with topics + related conversations

### Sprint 4: Advanced Analytics
10. **ML-04** — Drift metrics
11. **ML-06** — Blog suggestions
12. **UI-06** — Timeline visualizations
13. **UI-07** — Blog suggestions view

---

## Success Criteria (BAT Exit)

Per DELIVERY_PLAN.md §9:

1. ✅ Clean clone + documented setup produces running system
2. ⏳ ChatGPT exports importable without duplication (needs fingerprint)
3. ⏳ Projects, suggestions, timelines visible
4. ⏳ All ML outputs traceable via InferenceRun
5. ✅ No personal data in repository
6. ⏳ UI requires authentication (deferred for localhost)
7. ⏳ Documentation enables independent onboarding

---

## Open Questions

1. **Charting library:** Chart.js vs server-rendered SVG?
   - Chart.js: interactive, widely used
   - SVG: no JS dependency, simpler

2. **Clustering k value:** How many projects to suggest?
   - Start with sqrt(n/2) ≈ 25 for 1,293 conversations
   - Allow manual tuning

3. **Similarity threshold:** What cosine value counts as "related"?
   - Start with 0.3, tune based on results

4. **Topic count:** How many topics per conversation?
   - Start with top 3-5 by TF-IDF score

---

## Dependencies Map

```
DB-GOLD ─────────┬──────────────────────────────────────────┐
                 │                                          │
ML-01 ───────────┼──────────────────────────────────────────┤
                 │                                          │
                 ▼                                          │
ML-02 ──────────────────────────────────────────┐          │
(Clustering)     │                              │          │
                 │                              ▼          │
                 │                         UI-04           │
                 │                    (Suggestions Inbox)  │
                 ▼                                          │
ML-03 ──────────────────────────────────────────┐          │
(Topics)         │                              │          │
                 │                              ▼          │
                 │                         UI-06           │
                 │                    (Timelines)          │
                 ▼                                          │
ML-04 ──────────────────────────────────────────┐          │
(Drift)          │                              │          │
                 │                              │          │
                 ▼                              │          │
ML-06 ──────────────────────────────────────────┤          │
(Blog Topics)    │                              │          │
                 │                              ▼          │
                 │                         UI-07           │
                 │                    (Blog Suggestions)   │
                 │                                          │
ML-05 ──────────────────────────────────────────┘          │
(Similarity)     │                                          │
                 │                                          │
                 ▼                                          │
            UI-05 ◄─────────────────────────────────────────┘
       (Conversation Viewer)
```

---

## Estimated Effort

| Task | Complexity | Dependencies |
|------|------------|--------------|
| DB-GOLD | Medium | None |
| ML-01 | Low | DB-GOLD |
| ML-02 | High | ML-01 |
| ML-03 | Medium | ML-01 |
| ML-04 | Medium | ML-02, ML-03 |
| ML-05 | Medium | ML-01 |
| ML-06 | Medium | ML-02, ML-03 |
| UI-03 | Medium | ML-02 |
| UI-04 | Medium | ML-02 |
| UI-05 | Medium | None (enhanced with ML-03, ML-05) |
| UI-06 | High | ML-03, ML-04 |
| UI-07 | Low | ML-06 |

---

*Document created: 2026-01-04*
*Ready for execution when capacity allows*
