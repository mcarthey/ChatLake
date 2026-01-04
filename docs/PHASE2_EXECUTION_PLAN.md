# Phase 2 Execution Plan: ML Analysis + UI Improvements

**Status:** Planning
**Prerequisite:** Phase 1 Complete (200MB import successful, streaming pipeline, progress tracking)
**Goal:** Implement Gold tier ML features with concurrent UI improvements

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

### DB-GOLD — Gold Tier Schema Migration

**Objective:** Create all Gold tier tables from MSSQL_SCHEMA.md

**Tables to create:**
1. `InferenceRun` - ML execution tracking
2. `Project` - User/system projects
3. `ProjectConversation` - Conversation ↔ Project mapping
4. `ProjectSuggestion` - Clustering inbox
5. `Topic` - Extracted topics
6. `ConversationTopic` - Topic assignments
7. `ProjectDriftMetric` - Topic creep scores
8. `ConversationSimilarity` - Related conversations
9. `BlogTopicSuggestion` - Publishing candidates

**Tasks:**
- [ ] Create EF Core entities in `ChatLake.Infrastructure/Gold/Entities/`
- [ ] Add DbSet properties to ChatLakeDbContext
- [ ] Configure relationships and indexes in OnModelCreating
- [ ] Generate and apply migration
- [ ] Verify schema matches MSSQL_SCHEMA.md exactly

**DoD:**
- All 9 Gold tables exist with proper FKs, indexes, constraints
- Migration is reversible
- No breaking changes to existing Bronze/Silver tables

---

### ML-01 — InferenceRun Framework

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

**Interface:**
```csharp
public interface IInferenceRunService
{
    Task<long> StartRunAsync(string runType, string modelName, string modelVersion,
        string inputScope, string? inputDescription = null);
    Task CompleteRunAsync(long runId, string? metricsJson = null);
    Task FailRunAsync(long runId, string errorMessage);
    Task<InferenceRun?> GetRunAsync(long runId);
    Task<IReadOnlyList<InferenceRun>> GetRecentRunsAsync(string? runType = null, int limit = 20);
}
```

**RunTypes to support:**
- `Clustering` - KMeans project grouping
- `Topics` - Topic extraction
- `Similarity` - Conversation similarity edges
- `Drift` - Topic drift calculation
- `BlogTopics` - Blog suggestion generation

**DoD:**
- InferenceRun rows created for every ML operation
- Status transitions: Running → Completed | Failed
- Metrics stored as JSON for analysis

---

## Phase 2B: Core ML Features

### ML-02 — Baseline Conversation Clustering

**Objective:** Auto-group conversations into suggested projects

**Approach (ML.NET TF-IDF + KMeans):**
1. Extract text from all conversations (concatenate messages)
2. TF-IDF featurization
3. KMeans clustering (k = sqrt(n/2) as starting heuristic)
4. Generate ProjectSuggestion records
5. Optionally auto-create Project + ProjectConversation if confidence > threshold

**Components:**
```
ChatLake.Inference/
  Clustering/
    ConversationClusteringPipeline.cs
    ClusteringOptions.cs

ChatLake.Core/
  Services/
    IClusteringService.cs
```

**Tasks:**
- [ ] Add ML.NET packages (Microsoft.ML, Microsoft.ML.Transforms.Text)
- [ ] Implement TF-IDF + KMeans pipeline
- [ ] Create ProjectSuggestion records with confidence scores
- [ ] Unit tests for clustering determinism
- [ ] Integration test: cluster sample conversations

**DoD:**
- ProjectSuggestion records created for each cluster
- Confidence scores populated
- Results reproducible (same input → same clusters)

---

### ML-03 — Topic Extraction

**Objective:** Label conversations with topic keywords

**Approach:**
1. Use TF-IDF to extract top keywords per conversation
2. Optionally use LDA (Latent Dirichlet Allocation) for topic modeling
3. Create Topic records for discovered topics
4. Create ConversationTopic assignments with scores

**Tasks:**
- [ ] Implement keyword extraction (top N TF-IDF terms)
- [ ] Create Topic + ConversationTopic records
- [ ] Expose topics in conversation API response

**DoD:**
- Each conversation has 1-5 topic assignments
- Topics visible in conversation detail view

---

### ML-05 — Similarity Detection

**Objective:** Enable "Have I solved this before?" via conversation similarity

**Approach:**
1. Compute TF-IDF vectors for all conversations
2. Calculate cosine similarity between all pairs
3. Store edges where similarity > threshold (e.g., 0.3)
4. Optionally: add ONNX embedding support later

**Performance consideration:**
- N conversations = N² comparisons
- For 1,293 conversations: ~835k comparisons
- Batch and threshold aggressively

**Tasks:**
- [ ] Implement pairwise cosine similarity
- [ ] Store ConversationSimilarity edges (A < B ordering)
- [ ] Add "Related Conversations" to API
- [ ] Performance test with full dataset

**DoD:**
- Similar conversations discoverable via API
- UI shows related conversations panel

---

### ML-04 — Drift Metric Calculation

**Objective:** Detect topic creep within projects over time

**Prerequisite:** ML-02 (clustering) + ML-03 (topics)

**Approach:**
1. For each project, collect conversations by time window
2. Compute topic distribution per window
3. Calculate drift as KL divergence or cosine distance between windows
4. Store ProjectDriftMetric records

**Tasks:**
- [ ] Implement rolling window analysis
- [ ] Calculate drift scores
- [ ] Store ProjectDriftMetric records

**DoD:**
- Drift scores computed for each project
- Visible in project detail view

---

### ML-06 — Blog Topic Suggestion Engine

**Objective:** Identify publishable research arcs

**Prerequisite:** ML-02 (projects) + ML-03 (topics)

**Approach:**
1. Identify projects with >N conversations
2. Analyze topic coherence and evolution
3. Generate candidate blog titles/outlines
4. Store BlogTopicSuggestion records

**Tasks:**
- [ ] Implement research arc detection heuristics
- [ ] Generate title + outline suggestions
- [ ] Link to source conversations

**DoD:**
- Blog suggestions visible in UI
- Traceable to source conversations

---

## Phase 2C: UI Improvements (Parallel Track)

### UI-05 — Enhanced Conversation Viewer

**Objective:** Full threaded message display with metadata

**Current state:** Basic list with preview text

**Target state:**
- Threaded message display (user/assistant alternating)
- Message timestamps and roles
- Topics sidebar
- Related conversations panel
- Project assignment display

**Tasks:**
- [ ] Create `/Conversations/Detail/{id}` page
- [ ] Render messages in threaded format
- [ ] Add topics sidebar (when ML-03 complete)
- [ ] Add related conversations panel (when ML-05 complete)
- [ ] Styling for readability

**DoD:**
- Conversations fully readable
- Metadata visible
- Related content discoverable

---

### UI-03 — Project Dashboard

**Objective:** Primary navigation surface for projects

**Prerequisite:** ML-02 (creates projects)

**Components:**
- Project list with conversation counts
- Project detail view with:
  - Conversation list
  - Timeline (volume over time)
  - Topic breakdown
  - Drift indicator (when ML-04 complete)

**Tasks:**
- [ ] Create `/Projects` list page
- [ ] Create `/Projects/Detail/{id}` page
- [ ] Add conversation count and date range
- [ ] Add topic summary (when ML-03 complete)

**DoD:**
- Projects navigable
- Key metrics visible at a glance

---

### UI-04 — Suggested Projects Inbox

**Objective:** Human-in-the-loop project approval

**Prerequisite:** ML-02 (creates suggestions)

**Actions to support:**
- Accept → creates Project + assigns conversations
- Reject → marks suggestion dismissed
- Merge → combines with existing project

**Tasks:**
- [ ] Create `/Projects/Suggestions` inbox page
- [ ] Display cluster preview (sample conversations)
- [ ] Accept/Reject/Merge buttons with API calls
- [ ] Persist decisions in UserOverride table

**DoD:**
- User can accept/reject all suggestions
- Decisions persist across inference reruns

---

### UI-06 — Timeline Visualizations

**Objective:** Visual analytics for cognitive telemetry

**Prerequisite:** ML-03 (topics) + ML-04 (drift)

**Charts to implement:**
- Volume over time (conversation count by month)
- Topic bands (stacked area chart)
- Drift overlays (line chart)

**Library options:**
- Chart.js (client-side, lightweight)
- Server-rendered SVG (no JS dependency)

**Tasks:**
- [ ] Choose charting library
- [ ] Implement volume timeline
- [ ] Implement topic distribution chart
- [ ] Add drift overlay

**DoD:**
- Charts render accurately
- Interactive (hover for details)

---

### UI-07 — Blog Topic Suggestions View

**Objective:** Support publishing workflow

**Prerequisite:** ML-06

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
