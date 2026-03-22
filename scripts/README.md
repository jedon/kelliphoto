# Scripts

PowerShell (`.ps1`) and shell (`.sh`) automation for this repository. Run paths are relative to the **repository root** unless noted.

| Path | Purpose |
|------|---------|
| [apply-migrations.ps1](apply-migrations.ps1) / [apply-migrations.sh](apply-migrations.sh) | Run `dotnet ef database update` for `src/KelliPhoto.Web` |
| [apply-migration-to-server.ps1](apply-migration-to-server.ps1) / [.sh](apply-migration-to-server.sh) | Apply `complete-migration.sql` via `psql` (expects file at repo root) |
| [quick-fix-database.sh](quick-fix-database.sh) | One-liner: apply `complete-migration.sql` and restart `kelliphoto-web` |
| [verify-deployment.sh](verify-deployment.sh) | Health checks for containers, DB, and site |
| [docker/](docker/) | Host / iptables / Postgres helper scripts for deployment |
| [docker/nginx/](docker/nginx/) | Nginx install helper (copies config from `docker/nginx/` in the repo) |

Examples:

```powershell
.\scripts\apply-migrations.ps1 Development
```

```bash
./scripts/apply-migrations.sh Development
```
