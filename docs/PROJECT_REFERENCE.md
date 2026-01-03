# ChatLake: Enterprise-Quality Personal Chat Data Lake + ML-Augmented Knowledge System
Version: 0.1 (Reference Plan)  
Owner: Mark  
Stack Target: ASP.NET MVC + Razor, MSSQL, ML.NET  
Deployment Targets: (1) Open-source GitHub repo (code only) (2) Personal hosted instance (private data)

---

## 1. Purpose

Build a self-hosted system that ingests exported ChatGPT conversations into a **data-lake-style pipeline**, curates them into a **queryable MSSQL backend**, and applies **ML.NET** to generate higher-level structure (projects, topics, drift, similarity, timelines, blog-topic suggestions). The system must be designed from day one as **enterprise-quality**: secure-by-default, reproducible, versioned, auditable, and maintainable.

---

## 2. Objectives

### 2.1 Primary Goals
1. **Ingest & preserve** ChatGPT exports as immutable raw artifacts (data lake “bronze”).
2. **Normalize & index** conversations/messages into a relational model (data lake “silver”).
3. **Augment & analyze** with ML.NET (data lake “gold”):
   - automatic project clustering
   - project suggestion inbox
   - topic drift/topic creep detection
   - timeline visualizations (topics, volume, frequency)
   - similarity/“have I solved this before?”
   - blog post topic suggestions for long-running research threads
4. **Support reimports** safely and predictably, without data loss or corruption.
5. **Open-source the platform** without risking private data leakage.

### 2.2 Non-Goals (for initial releases)
- SaaS / multi-tenant hosting
- Generative writing pipeline for full blog post authoring (topic/outline suggestion only initially)
- External cloud inference dependencies by default
- Full-blown graph database requirement (optional later)

---

## 3. Guiding Principles (Non-Negotiable)

1. **Code is public; data is private.**
2. **Raw imports are immutable and append-only.**
3. **Derived artifacts are reproducible and purgeable.**
4. **All ML outputs are versioned and auditable.**
5. **Security defaults to “private instance” posture.**
6. **Human-in-the-loop for organization changes** (merge/split/rename projects, accept tag suggestions, etc.).
7. **Design for long-term operation**: years of data, periodic imports, stable schema evolution.

---

## 4. Stakeholders & Users

### 4.1 Personas
- **Archivist**: wants full history preserved, searchable, defensible.
- **Synthesizer**: wants structure, clustering, drift detection, cross-linking.
- **Publisher**: wants “post-worthy” topic discovery with source traceability.

### 4.2 Operating Mode
Primarily single-user (Mark), with future-proofing for:
- additional read-only roles
- separate “redacted” views
- multiple local instances (community users)

---

## 5. Key Use Cases

### 5.1 Projects & Topic Creep
- Automatically create logical projects across disparate chats
- Detect drift: when a project’s topic distribution shifts materially over time
- Suggest splits/branches when drift exceeds thresholds

### 5.2 Project Suggestions Inbox
- Suggest new projects and clusters after imports
- Provide confidence scores and representative chats
- Allow accept/merge/reject decisions (tracked as overrides)

### 5.3 Blog Topic Suggestions (Research-to-Publish)
- Identify long-running “research arcs”
- Suggest blog post topics and outline skeletons
- Provide source conversations with traceability

### 5.4 Timelines & Visual Analytics
- Project timeline: volume, frequency, topic bands
- Topic drift overlays
- “bursts” of activity, concept recurrence

### 5.5 Similarity / “Have I solved this before?”
- Semantic retrieval of prior conversations
- Surface the most relevant historic resolution threads

### 5.6 Curriculum/Teaching Extraction (Optional early, strong later)
- Detect “explainer mode” segments
- Tag and group by topic for course material mining

---

## 6. Security, Privacy, and Compliance Concerns

### 6.1 Primary Risks
- Accidental commit of personal data to GitHub
- Exposing the hosted instance publicly
- Export features leaking sensitive content
- Derived data (embeddings/topics) leaking semantics

### 6.2 Mandatory Controls (MVP)
- Aggressive `.gitignore` + repository structure that prevents data inclusion
- No telemetry by default
- Authentication required for all UI/API endpoints (personal instance)
- Import files stored outside web root
- Raw data stored separately from curated/derived data (logical separation at minimum)

### 6.3 Recommended Controls (Phase 2)
- SQL Server TDE for at-rest encryption
- Optional field/column encryption for raw message content
- “Redacted mode” views and export policies
- Audit logs for imports, inference runs, and user overrides

---

## 7. System Architecture Overview

### 7.1 Zones (Data Lake Pattern)
- **Bronze (Raw)**: store original exports as blobs + metadata, immutable
- **Silver (Curated)**: normalized relational tables (Conversation, Message, etc.)
- **Gold (Derived)**: ML outputs (Topics, Clusters, Similarity edges, Drift metrics)

### 7.2 Application Layers
- ASP.NET MVC + Razor (UI)
- ASP.NET Web API endpoints (internal + UI use)
- Domain services (import, parsing, inference, visualization)
- MSSQL backend
- ML.NET inference pipelines (offline/batch + on-demand)

### 7.3 Inference Execution Model
- Batch run after import (default)
- Re-runnable with model/version identifiers
- Results stored with `InferenceRunId` and reproducible metadata

---

## 8. Data Model (Conceptual)

### 8.1 Core Entities (Silver)
- ImportBatch
- RawArtifact (JSON blob + hash)
- Conversation
- Message
- Attachment (metadata only initially)
- TokenStats (optional)

### 8.2 Derived Entities (Gold)
- Project (system + user-managed)
- ProjectSuggestion
- ClusterAssignment (Conversation→Cluster/Project)
- TopicLabel (Conversation/Message-level)
- DriftMetric (Project over time)
- SimilarityEdge (Conversation↔Conversation)
- Concept (keywords/entities)
- InferenceRun (versioned model execution)

### 8.3 Overrides / Human Decisions
- UserOverride table(s) capturing:
  - accepted/rejected project suggestions
  - merges/splits/renames
  - manual tagging decisions
  - suppressed suggestions

---

## 9. ML.NET Strategy

### 9.1 Phase 1 (Deterministic, Lightweight)
- Text featurization (TF-IDF / n-grams) for baseline clustering and tagging
- KMeans clustering at conversation level
- Drift via rolling-window topic distribution differences

### 9.2 Phase 2 (Embeddings + Similarity)
- ONNX-based embeddings (local)
- Cosine similarity edges
- Better clustering + retrieval

### 9.3 Model Governance
- Each run produces:
  - model version
  - feature extraction config hash
  - timestamp
  - training data snapshot reference (ImportBatch set)
- Never overwrite; always append new run

---

## 10. Reimport & Incremental Operation

### 10.1 Requirements
- Reimports must not duplicate messages incorrectly
- Must be idempotent where possible
- Must record provenance (which export batch produced which records)

### 10.2 Approach
- Use content hashes and stable IDs (where available) to deduplicate
- Track all ingests under ImportBatch
- Allow reprocessing of derived data without touching raw

---

## 11. UI/UX Requirements (Razor)

### 11.1 Core Screens
- Import dashboard (upload, validate, status, history)
- Project dashboard (list, search, metrics)
- Suggested projects inbox (approve/merge/reject)
- Project detail (timeline, topics, drift, top chats)
- Conversation viewer (threaded display, metadata, tags)
- Similar chats (“related threads”) panel
- Blog topic suggestions view (with traceability)

### 11.2 Non-Functional UI Requirements
- Fast navigation (pagination, search)
- Clear provenance (what is inferred vs user-decided)
- Minimal “foot-guns” for export/sharing

---

## 12. APIs (Internal + Optional External)

### 12.1 Internal API Endpoints (initial set)
- GET /api/projects
- GET /api/projects/{id}
- GET /api/projects/{id}/timeline
- GET /api/suggestions/projects
- POST /api/suggestions/projects/{id}/accept | reject | merge | split
- GET /api/conversations/{id}
- GET /api/conversations/{id}/related
- POST /api/imports
- POST /api/inference/run

### 12.2 Security
- Auth required
- No anonymous endpoints in personal instance
- Optional API key for non-browser tooling (later)

---

## 13. Documentation & Enterprise Deliverables

### 13.1 Repo-Level Documentation (required early)
- README.md (what it is, quickstart, non-goals)
- SECURITY.md (data handling, safe defaults, deployment notes)
- CONTRIBUTING.md (dev setup, coding standards, PR rules)
- ARCHITECTURE.md (layers, zones, key decisions)
- DATA_MODEL.md (schema, relationships, migrations)
- IMPORT_FORMATS.md (supported exports, validation rules)
- INFERENCE.md (pipelines, model governance)
- DEPLOYMENT.md (IIS, reverse proxy, private hosting)
- CHANGELOG.md (versioned changes)

### 13.2 Engineering Standards
- Solution structure and naming conventions
- Logging strategy (structured logs)
- Error handling & validation patterns
- Automated DB migrations (EF Core Migrations recommended)
- Automated tests:
  - parsing tests
  - import idempotency tests
  - inference output shape tests

---

## 14. Task Tracking & Work Management

### 14.1 Work Items (GitHub Issues + Projects)
Use GitHub Projects with:
- Epics (Milestones)
- Issues (user stories + technical tasks)
- Labels:
  - area:import / area:ml / area:ui / area:db / area:security
  - type:feature / type:bug / type:tech-debt
  - priority:p0/p1/p2

### 14.2 Definition of Done (DoD)
A feature is “done” when:
- implemented + reviewed
- documented (as needed)
- tested (unit/integration where relevant)
- secured (auth checks, no data exposure)
- observable (logs/metrics for the workflow)

---

## 15. Milestones (Initial Roadmap)

### Milestone 0 — Repository & Safety Baseline
- Repo scaffolding + documentation skeleton
- `.gitignore` hardened (data folders excluded)
- Local config pattern established (no secrets in repo)

### Milestone 1 — Bronze Ingestion
- ImportBatch + RawArtifact storage
- Validation + hashing + logging
- Import history UI

### Milestone 2 — Silver Normalization
- Conversation/Message schema
- Parser pipeline from RawArtifact → normalized
- Conversation viewer UI + search

### Milestone 3 — Gold: Baseline ML
- Clustering + project suggestion inbox
- Manual accept/reject + overrides persistence
- Project dashboards

### Milestone 4 — Visual Timelines
- Timeline charts: volume/frequency/topics
- Drift metrics overlays

### Milestone 5 — Similarity + “Solved before”
- Related conversations via embeddings or improved features
- Related panel in conversation viewer

### Milestone 6 — Blog Topic Suggestions
- Research-arc detection heuristics
- Topic/outline suggestions + source traceability

---

## 16. Acceptance Criteria (System-Level)

1. Re-importing the same export does not corrupt or duplicate normalized messages (or duplicates are detectably controlled by hash/provenance).
2. Raw artifacts are stored immutably and can be used to rebuild all derived layers.
3. Users can approve/reject suggestions and those decisions persist across inference reruns.
4. Project views show timeline metrics and topic drift in a way that is explainable.
5. No private data is present in the open-source repository, and the build runs without it.
6. Hosted instance requires authentication and does not expose data anonymously by default.

---

## 17. Open Questions / Decisions Log (to be resolved early)

- Export formats supported (ChatGPT export variants): JSON structure confirmation and versioning strategy
- Embedding approach:
  - TF-IDF first vs ONNX embeddings early
- Hosting model for personal instance:
  - IIS + Windows Auth vs ASP.NET Identity
- Data retention policies for derived artifacts:
  - purge/rebuild cadence and storage constraints
- Visualizations library choice (server-rendered vs client JS charting)

---

## 18. Immediate Next Steps (Actionable)

1. Create repo structure + docs skeleton (empty but present).
2. Lock down `.gitignore` and local config conventions.
3. Draft the conceptual MSSQL schema (Bronze/Silver/Gold + Overrides + InferenceRun).
4. Implement Milestone 1 import pipeline end-to-end (UI + API + DB + logs).
5. Add baseline unit tests for parsing and idempotency.
