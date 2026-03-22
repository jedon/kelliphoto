# Kelli Photo — documentation

This folder complements the repository [README](../README.md) with material aimed at developers and operators.

| Document | Purpose |
|----------|---------|
| [DEVELOPMENT.md](DEVELOPMENT.md) | Local setup, database migrations, tests, and common commands |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Application structure, major services, and request flow |

| Area | Location |
|------|----------|
| **Docker / Portainer / deployment** | [docs/docker/](docker/README.md) |
| **Automation (migrations, verification)** | [scripts/](../scripts/README.md) |
| **Runbooks & troubleshooting** | Markdown files in this folder (e.g. [START_HERE.md](START_HERE.md), [MIGRATION_GUIDE.md](MIGRATION_GUIDE.md)) |

Older docs may still say `./script.sh` from the repo root; those scripts now live under **`scripts/`** (for example `scripts/verify-deployment.sh`).
