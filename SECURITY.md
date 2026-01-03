# Security Policy – ChatLake

## 1. Overview

ChatLake is a self-hosted system designed to ingest, store, and analyze **potentially sensitive conversational data** (e.g., ChatGPT exports).  
As such, **security is a first-class design concern**, not an afterthought.

This document describes:
- The project’s security posture
- Data handling guarantees
- Deployment expectations
- Vulnerability reporting process

---

## 2. Security Model

### 2.1 Core Security Principles

The following principles are **non-negotiable**:

1. **Code is public. Data is private.**
2. **No real data is ever committed to the repository.**
3. **Raw imported data is immutable and append-only.**
4. **Derived data is reproducible and purgeable.**
5. **The default deployment model is private and authenticated.**
6. **No telemetry or external data transmission is enabled by default.**

Any change that weakens these principles will not be accepted.

---

## 3. Data Classification

ChatLake assumes imported data may include:

- Personal information (PII)
- Health-related discussions
- Financial or employment context
- Unpublished writing or research
- Longitudinal behavioral data

As a result, **all imported content is treated as sensitive by default**.

---

## 4. Repository Data Guarantees

### 4.1 What This Repository Will NEVER Contain

- ChatGPT exports
- Conversation text
- Embeddings or vector data
- Attachments
- Database files
- Model artifacts
- Secrets or credentials

These are explicitly excluded via `.gitignore` and policy.

### 4.2 Allowed Content

- Source code
- Documentation
- Database schema definitions
- Import parsers
- Synthetic test data (clearly labeled)

---

## 5. Local Deployment Expectations

ChatLake is **not designed to be publicly exposed by default**.

### 5.1 Required Controls (Personal Instance)

- Authentication enabled for all UI routes
- No anonymous API access
- Imports stored outside web root
- Configuration via environment variables or local config files
- Database access restricted to application identity

### 5.2 Strongly Recommended Controls

- SQL Server Transparent Data Encryption (TDE)
- OS-level disk encryption
- Firewall rules restricting database access
- Reverse proxy with authentication if hosted remotely
- Backups stored securely and encrypted

---

## 6. Import & Data Handling Security

### 6.1 Raw Data (Bronze Layer)

- Stored immutably
- Never modified after ingestion
- Used only as a source for rebuilds
- Can be deleted entirely if required by the user

### 6.2 Curated & Derived Data (Silver / Gold)

- Fully rebuildable from raw imports
- Versioned by inference run
- Can be purged independently of raw data
- Must never be treated as authoritative over raw data

---

## 7. ML / Inference Security

ChatLake uses **local ML.NET inference** only.

### Guarantees
- No cloud inference by default
- No external model calls
- No automatic model downloads at runtime
- Deterministic execution where feasible

### Considerations
- Embeddings may leak semantic meaning
- Treat derived vectors as sensitive data
- Do not export embeddings by default

---

## 8. Open Source & Forking Guidance

Users who fork or deploy ChatLake are responsible for:

- Securing their hosting environment
- Complying with applicable laws and regulations
- Ensuring no sensitive data is exposed publicly
- Reviewing export and sharing features carefully

The maintainers of this repository **do not have access to user data** and are not responsible for downstream deployments.

---

## 9. Vulnerability Reporting

### 9.1 Reporting a Security Issue

If you discover a security vulnerability:

- **Do not open a public issue**
- **Do not include exploit details in a PR**

Instead, report privately by:
- Contacting the repository owner directly via GitHub
- Or using GitHub’s private security advisory feature (if enabled)

Please include:
- Description of the issue
- Steps to reproduce
- Potential impact
- Suggested remediation (if known)

---

## 10. Security Fix Policy

- Security issues will be triaged promptly
- Fixes will prioritize data safety over backward compatibility
- Breaking changes may be introduced if required to protect users
- Public disclosure will occur only after a fix is available

---

## 11. Supported Versions

Only actively maintained versions of ChatLake are eligible for security fixes.

At this time:
- `main` branch is considered active
- No long-term support branches exist yet

---

## 12. Final Statement

ChatLake is designed to help users **understand and synthesize their own data**, not expose it.

If a feature:
- increases the risk of accidental disclosure
- obscures provenance
- weakens auditability
- or bypasses authentication

…it is likely a security regression and should be treated as such.

When in doubt: **choose the safer option and document the decision**.

