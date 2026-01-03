# ChatLake – Technical Delivery Task Breakdown (BAT-Ready Scope)
Version: 1.0  
Audience: Engineering Team (Solo or Distributed)  
Completion Standard: Enterprise-quality delivery, ready for Business Acceptance Testing (BAT)

---

## 0. How to Read This Document

- Tasks are **discrete, assignable, and independently executable**
- Each task has:
  - Objective
  - Inputs
  - Outputs
  - Definition of Done (DoD)
- Tasks are grouped into **workstreams**
- Assignees should be able to work **in isolation**, using documentation and prior artifacts
- When *all tasks* are complete, the system is considered **feature-complete for BAT**

---

## 1. Foundation & Repository Setup (Workstream FND)

### FND-01 — Repository Scaffolding
**Objective**  
Create the initial GitHub repository structure with no data leakage risk.

**Inputs**
- Project reference document
- Security posture guidelines

**Outputs**
- GitHub repo with standard structure

**Tasks**
- Create `/src`, `/docs`, `/examples`, `/tests`
- Add solution file placeholder
- Ensure repo builds with no data

**DoD**
- Repo clones and builds with zero configuration
- No runtime dependency on local data

---

### FND-02 — Git Ignore & Local Config Strategy
**Objective**  
Prevent accidental commits of sensitive data or secrets.

**Tasks**
- Harden `.gitignore` to exclude:
  - `/data`
  - `/imports`
  - `/secrets`
  - local config files
- Create `appsettings.Local.json` pattern
- Document config override strategy

**DoD**
- `git status` never shows local data files
- README documents config usage clearly

---

### FND-03 — Core Documentation Skeleton
**Objective**  
Establish enterprise documentation baseline.

**Tasks**
- Create empty or stub versions of:
  - README.md
  - SECURITY.md
  - ARCHITECTURE.md
  - DATA_MODEL.md
  - IMPORT_FORMATS.md
  - INFERENCE.md
  - DEPLOYMENT.md
  - CHANGELOG.md

**DoD**
- All documents exist and are referenced in README

---

## 2. Database & Persistence Layer (Workstream DB)

### DB-01 — MSSQL Schema Implementation
**Objective**  
Implement full Bronze/Silver/Gold schema exactly as specified.

**Inputs**
- MSSQL Schema + Idempotency artifact

**Tasks**
- Create EF Core models
- Create initial migration
- Explicitly define:
  - PKs
  - FKs
  - Unique constraints
  - Filtered indexes

**DoD**
- Database creates cleanly from migration
- Schema matches design artifact 1-to-1

---

### DB-02 — Migration & Versioning Strategy
**Objective**  
Enable controlled schema evolution.

**Tasks**
- Configure EF Core migrations
- Document migration workflow
- Add rollback notes (manual if required)

**DoD**
- New developer can create DB from scratch using migrations only

---

### DB-03 — Connection Security & Configuration
**Objective**  
Secure database access.

**Tasks**
- Configure connection strings via environment / local config
- Validate no secrets in repo
- Enable optional TDE documentation

**DoD**
- App fails fast if DB config missing
- No secrets committed

---

## 3. Import & Ingestion Pipeline (Workstream IMP)

### IMP-01 — ImportBatch Lifecycle
**Objective**  
Implement ImportBatch tracking and state management.

**Tasks**
- Create ImportBatch service
- Support states:
  - Staged
  - Committed
  - Failed
- Persist metadata

**DoD**
- ImportBatch rows created and updated correctly
- Failed imports are visible and auditable

---

### IMP-02 — RawArtifact Storage
**Objective**  
Store raw export artifacts immutably.

**Tasks**
- Implement RawArtifact persistence
- Compute SHA-256 hashes
- Support DB-stored JSON payloads

**DoD**
- Identical artifacts hash identically
- Raw data is never modified after insert

---

### IMP-03 — ChatGPT Export Parser
**Objective**  
Parse exported conversation JSON into normalized objects.

**Tasks**
- Parse conversations
- Parse messages
- Extract timestamps, roles, ordering
- Handle missing or malformed fields defensively

**DoD**
- Parser handles valid exports without crashing
- Invalid input fails with clear error

---

### IMP-04 — Message Fingerprint Engine (Critical)
**Objective**  
Guarantee idempotent imports.

**Tasks**
- Implement canonical message fingerprint generator
- Normalize text consistently
- Unit test fingerprint determinism

**DoD**
- Same message imported twice yields identical fingerprint
- Tests prove idempotency

---

### IMP-05 — Silver Normalization Pipeline
**Objective**  
Persist Conversations and Messages safely.

**Tasks**
- Upsert Conversation by SourceConversationKey
- Insert Messages with:
  - SequenceIndex
  - Fingerprint enforcement
- Update ImportedFirst/LastBatchId

**DoD**
- Reimporting same data creates no duplicate messages
- Overlapping imports behave deterministically

---

### IMP-06 — Import Observation Logging
**Objective**  
Track provenance of imports.

**Tasks**
- Implement ImportObservation insertions
- At minimum: conversation-level tracking

**DoD**
- Every import batch links to observed conversations

---

## 4. API Layer (Workstream API)

### API-01 — Core API Infrastructure
**Objective**  
Establish internal API pattern.

**Tasks**
- Create API project or area
- Add routing, filters, error handling
- Secure endpoints (auth required)

**DoD**
- API responds consistently with structured errors

---

### API-02 — Import APIs
**Objective**  
Expose import operations.

**Endpoints**
- POST `/api/imports`
- GET `/api/imports`

**DoD**
- Imports can be triggered and monitored via API

---

### API-03 — Conversation APIs
**Endpoints**
- GET `/api/conversations`
- GET `/api/conversations/{id}`
- GET `/api/conversations/{id}/related` (stub initially)

**DoD**
- Conversations retrievable with ordered messages

---

### API-04 — Project & Suggestion APIs
**Endpoints**
- GET `/api/projects`
- GET `/api/suggestions/projects`
- POST `/api/suggestions/projects/{id}/accept|reject|merge`

**DoD**
- Suggestions lifecycle manageable via API

---

## 5. ML / Derived Data Pipelines (Workstream ML)

### ML-01 — InferenceRun Framework
**Objective**  
Standardize ML execution tracking.

**Tasks**
- Implement InferenceRun persistence
- Support status transitions
- Capture model metadata

**DoD**
- All inference writes are traceable to a run

---

### ML-02 — Baseline Conversation Clustering
**Objective**  
Auto-group conversations.

**Tasks**
- Implement ML.NET clustering (TF-IDF or equivalent)
- Assign conversations to suggested projects
- Persist confidence scores

**DoD**
- ProjectSuggestion records created
- Results reproducible across reruns

---

### ML-03 — Topic Extraction
**Objective**  
Label conversations with topics.

**Tasks**
- Extract keywords/topics
- Persist Topic + ConversationTopic
- Store scores

**DoD**
- Topics visible in DB and UI

---

### ML-04 — Drift Metric Calculation
**Objective**  
Detect topic creep over time.

**Tasks**
- Implement rolling window analysis
- Persist ProjectDriftMetric

**DoD**
- Drift scores computed per project timeline

---

### ML-05 — Similarity Detection
**Objective**  
Enable “Have I solved this before?”

**Tasks**
- Compute conversation similarity
- Persist ConversationSimilarity edges

**DoD**
- Related conversations returned by similarity score

---

### ML-06 — Blog Topic Suggestion Engine
**Objective**  
Identify publishable research arcs.

**Tasks**
- Detect long-running projects
- Generate candidate titles + outlines
- Persist BlogTopicSuggestion

**DoD**
- Suggestions are traceable to source conversations

---

## 6. UI / Razor Views (Workstream UI)

### UI-01 — Authentication & Access Control
**Objective**  
Secure all UI routes.

**Tasks**
- Implement login (local or Windows auth)
- Enforce authenticated access

**DoD**
- No anonymous access possible

---

### UI-02 — Import Dashboard
**Objective**  
Visualize imports and status.

**Tasks**
- Upload UI
- Import history table
- Status indicators

**DoD**
- User can monitor all imports visually

---

### UI-03 — Project Dashboard
**Objective**  
Primary navigation surface.

**Tasks**
- Project list
- Metrics summary
- Links to detail views

**DoD**
- Projects navigable and readable

---

### UI-04 — Suggested Projects Inbox
**Objective**  
Human-in-the-loop control.

**Tasks**
- List suggestions
- Accept/reject/merge actions

**DoD**
- User decisions persist across reruns

---

### UI-05 — Conversation Viewer
**Objective**  
Inspect normalized chat threads.

**Tasks**
- Threaded message display
- Metadata (topics, projects)
- Related conversations panel

**DoD**
- Conversations readable and ordered correctly

---

### UI-06 — Timeline & Visualization Views
**Objective**  
Provide cognitive telemetry.

**Tasks**
- Volume over time
- Topic bands
- Drift overlays

**DoD**
- Charts reflect persisted metrics accurately

---

### UI-07 — Blog Topic Suggestions View
**Objective**  
Support publishing workflow.

**Tasks**
- List blog topic candidates
- Show outline + sources
- Status management

**DoD**
- Suggestions usable without manual DB access

---

## 7. Quality, Testing & Hardening (Workstream QA)

### QA-01 — Unit Tests (Critical Paths)
**Scope**
- Fingerprint generation
- Import idempotency
- Parser correctness

**DoD**
- Tests fail on regression
- Deterministic results

---

### QA-02 — Integration Tests
**Scope**
- Import end-to-end
- Reimport scenarios
- Inference reruns

**DoD**
- System stable under repeated imports

---

### QA-03 — Security Verification
**Scope**
- Auth enforcement
- No data exposure
- Config isolation

**DoD**
- Security checklist satisfied

---

### QA-04 — Performance Smoke Testing
**Scope**
- Large import batch
- Timeline rendering
- Similarity queries

**DoD**
- Acceptable performance under expected load

---

## 8. Documentation & BAT Readiness (Workstream DOC)

### DOC-01 — Final Documentation Pass
**Tasks**
- Complete all markdown docs
- Validate consistency with implementation

**DoD**
- Docs reflect reality, not intent

---

### DOC-02 — Deployment Validation
**Tasks**
- Fresh install on clean machine
- Local hosting behind auth

**DoD**
- System deploys from repo + docs alone

---

## 9. Business Acceptance Criteria (Exit Conditions)

The project is **complete and BAT-ready** when:

1. A clean clone + documented setup produces a running system.
2. ChatGPT exports can be imported repeatedly without duplication.
3. Projects, suggestions, timelines, and blog topics are generated and viewable.
4. All ML outputs are traceable, versioned, and reproducible.
5. No personal data exists in the repository.
6. UI requires authentication and exposes no anonymous data.
7. Documentation enables a new engineer to onboard independently.

---

## 10. End of Scope

Any work beyond this point (multi-user SaaS, cloud inference, public sharing, graph DBs) is **explicitly out of scope** for this delivery and should be tracked as a separate roadmap.
