# ChatLake

**ChatLake** is a self-hosted, enterprise-quality system for ingesting, structuring, and analyzing exported conversational data (e.g., ChatGPT exports) using a data lake architecture, relational persistence, and local ML inference.

This repository contains **code only**.  
All personal data remains **local, private, and excluded from version control by design**.

---

## Purpose

ChatLake is designed to solve a specific long-term problem:

> How do you safely preserve, organize, analyze, and synthesize years of conversational research, debugging, teaching, and creative work — without losing provenance, context, or control?

The system emphasizes:
- **Immutability of raw data**
- **Reproducibility of derived insights**
- **Human-in-the-loop decision making**
- **Security-first local deployment**
- **Enterprise-quality documentation and auditability**

---

## Key Capabilities (Planned)

- Import and preserve ChatGPT exports as immutable raw artifacts
- Normalize conversations and messages into a relational model (SQL Server)
- Automatically suggest logical “projects” across disparate chats
- Detect topic drift and long-running research arcs
- Provide timeline visualizations (volume, frequency, topic evolution)
- Surface “have I solved this before?” similarity links
- Suggest blog post topics and outlines from long-running projects
- Support safe, repeatable re-imports without duplication
- Operate fully offline with local ML.NET inference

---

## Architecture Overview

- **ASP.NET MVC + Razor** — UI and internal APIs
- **MSSQL** — Bronze / Silver / Gold data lake layers
- **ML.NET** — Local, deterministic inference
- **EF Core** — Schema migrations and persistence
- **No cloud dependencies by default**

See `/docs` for detailed design artifacts.

---

## Repository Structure

```

/src        Application source code
/tests      Unit and integration tests
/docs       Authoritative project documentation
/examples   Synthetic examples only (no real data)

````

---

## Security & Data Handling

**Important:**  
This repository intentionally contains **no real data**.

Design guarantees:
- Raw imports are local-only and ignored by git
- No telemetry or external data transmission
- No cloud inference by default
- Authentication required for all UI access
- Derived ML outputs are reproducible and purgeable

See `SECURITY.md` for full details.

---

## Getting Started (Development)

### Prerequisites
- .NET 10 SDK (LTS)
- SQL Server (local or containerized)
- Git

### Build
```bash
dotnet restore
dotnet build
dotnet test
````

At this stage, the application boots with no database or data dependencies.

---

## Project Status

This project is **under active development** and follows a staged, milestone-based delivery plan.

See:

* `/docs/PROJECT_REFERENCE.md`
* `/docs/MSSQL_SCHEMA.md`
* `/docs/DELIVERY_PLAN.md`

---

## Non-Goals

The following are explicitly **out of scope** for this repository:

* Hosted SaaS offering
* Multi-tenant cloud deployment
* Automatic publication of private data
* Generative content writing without human review

---

## Contributing

This project is being built with enterprise discipline from day one.

Before contributing:

* Read `CONTRIBUTING.md`
* Understand the data security model
* Never commit personal or imported data

---

## License

License to be determined prior to public release.

---

## Disclaimer

This software processes potentially sensitive personal data.
Users are solely responsible for securing their deployments and complying with applicable laws and policies.

