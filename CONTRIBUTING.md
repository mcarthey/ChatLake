# Contributing to ChatLake

Thank you for your interest in contributing to **ChatLake**.

This project is intentionally built with **enterprise-level discipline**, even at early stages. Contributions are welcome, but they must adhere strictly to the project’s architectural, security, and quality standards.

---

## 1. Core Principles (Read First)

Before contributing, you must understand and agree to the following non-negotiable principles:

1. **Code is public. Data is private.**
2. **No real or personal data is ever committed.**
3. **All derived data must be reproducible from raw imports.**
4. **Human decisions override automated inference.**
5. **Security defaults to local, private operation.**

If a contribution violates any of these principles, it will not be accepted.

---

## 2. What This Repository Is (and Is Not)

### This repository **is**:
- A self-hosted system for structuring and analyzing exported conversational data
- An offline-first, ML.NET–based analysis platform
- A long-lived research and knowledge management tool
- Designed for deterministic, auditable behavior

### This repository **is not**:
- A hosted SaaS product
- A cloud-first or telemetry-enabled system
- A generative content publishing platform
- A place to store or test against real chat data

---

## 3. Development Environment

### Required
- .NET **10 LTS**
- SQL Server (local instance or container)
- Git

### Optional
- Docker (for SQL Server)
- Visual Studio / Rider / VS Code

---

## 4. Repository Structure
```

/src Application source code
/tests Unit and integration tests
/docs Authoritative project documentation
/examples Synthetic examples only

```
**Never place real data in any directory under version control.**

---

## 5. Data Handling Rules (Critical)

### 🚫 Never Commit
- ChatGPT exports
- Conversation text
- Embeddings
- Attachments
- Personal notes or logs
- Database files
- `.onnx` or trained model artifacts

### ✅ Allowed
- Synthetic example data (clearly labeled)
- Schema definitions
- Parsers that operate on abstract input
- Test cases using generated content

Violations will result in immediate rejection of the contribution.

---

## 6. Coding Standards

### General
- Prefer clarity over cleverness
- Favor explicit over implicit behavior
- Fail fast on invalid input
- Log deterministically

### C# Guidelines
- Nullable reference types enabled
- Async APIs where appropriate
- No static state for domain logic
- Domain logic belongs in `ChatLake.Core`
- Infrastructure concerns belong in `ChatLake.Infrastructure`

---

## 7. Database & Persistence Rules

- Schema changes **must** use EF Core migrations
- Migrations must be reviewed carefully
- No destructive migrations without explicit justification
- Raw (Bronze) tables are **append-only**

If a change would invalidate historical imports, it must be discussed first.

---

## 8. ML / Inference Contributions

ML contributions must satisfy:

- Deterministic behavior
- Versioned execution (`InferenceRun`)
- Reproducibility from persisted data
- Clear documentation of inputs and outputs

**No black-box models. No cloud inference by default.**

---

## 9. Testing Requirements

All contributions must include appropriate tests:

### Required
- Unit tests for core logic
- Deterministic test outcomes
- Idempotency tests for import logic

### Strongly Recommended
- Integration tests for import and inference pipelines

If behavior cannot be tested, it must be explicitly justified.

---

## 10. Documentation Expectations

Changes that affect:
- Architecture
- Data model
- Import formats
- Inference logic
- Security posture

**Must update the corresponding document in `/docs`.**

Documentation is considered part of the deliverable.

---

## 11. Pull Request Process

1. Create a feature branch from `main`
2. Ensure the solution builds and tests pass:
   ```bash
   dotnet build
   dotnet test
	```

3. Update documentation as needed
4. Open a pull request with:
   - Clear description
   - Scope and motivation
   - Any breaking changes called out explicitly

PRs may be rejected for:

- Scope creep
- Insufficient documentation
- Security risk
- Non-deterministic behavior

------

## 12. Issue Tracking & Work Items

Please align contributions with existing issues or milestones when possible.

If proposing a major change:

- Open an issue first
- Describe motivation, impact, and alternatives
- Await agreement before implementation

------

## 13. Code of Conduct

Be professional, respectful, and precise.

This project values:

- Thoughtful engineering
- Clear reasoning
- Constructive disagreement

------

## 14. Final Reminder

**This project deals with potentially sensitive personal data.**
Even though none exists in the repository, design decisions must always assume it will exist in real deployments.

If in doubt:

- Choose the safer option
- Ask before committing
- Document your assumptions

------

Thank you for contributing responsibly.
