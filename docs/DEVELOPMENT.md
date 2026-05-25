# Development

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL (local or remote) for the application database
- Optional: Docker for stack parity with production

## Clone and configure

1. Clone the repository. From the **repository root**, run the commands below.

2. **Secrets and connection strings (recommended):** copy `src/KelliPhoto.Web/.env.example` to `src/KelliPhoto.Web/.env` (the file is gitignored). The app loads it at startup via [DotNetEnv](https://github.com/tonerdo/dotnet-env) before configuration binds, so nested keys use **double underscores** (for example `ConnectionStrings__DefaultConnection`, `Email__SmtpPassword`). The same variable names work in the shell and in Docker Compose; see `docker/.env.example` for compose-specific notes.

3. **Non-secret settings** stay in `appsettings.json` / `appsettings.Development.json` (for example `GallerySettings` paths). If `DefaultConnection` is missing and the app is not running under the integration-test host, startup fails with a clear error so you do not accidentally run against no database.

4. **EF Core CLI** (`dotnet ef`): `ApplicationDbContextFactory` also loads `.env` so migrations pick up the connection string without duplicating it in JSON.

5. **GitHub Actions:** [CI/CD](../.github/workflows/ci-cd.yml) expects repository secrets `CONNECTION_STRINGS__DEFAULT_CONNECTION` and `EMAIL__SMTP_PASSWORD`. See [CICD_SETUP.md](CICD_SETUP.md) for deploy secrets and Playwright e2e.

6. Apply EF Core migrations:

   ```powershell
   dotnet ef database update --project src\KelliPhoto.Web
   ```

7. Run the site:

   ```powershell
   dotnet run --project src\KelliPhoto.Web
   ```

## New migrations

After model changes:

```powershell
dotnet ef migrations add <MigrationName> --project src\KelliPhoto.Web
dotnet ef database update --project src\KelliPhoto.Web
```

## Tests

From the repository root:

```powershell
dotnet test
```

**Playwright (browser regression):**

```powershell
cd e2e
npm ci
npx playwright install chromium
npm test
```

The test project lives under `tests/KelliPhoto.Web.Tests`. **Integration tests** use `WebApplicationFactory` with `ASPNETCORE_ENVIRONMENT=Testing` and `KELLIPHOTO_INTEGRATION_TEST=1`; the app switches Entity Framework to an in-memory database and skips HTTPS redirection. Each test run should use a **configured** `WebApplicationFactory` from `WithWebHostBuilder` (not only the shared fixture type) so `GallerySettings` overrides match the temp directories where tests create image files.

## Logging

Serilog is configured in `appsettings.json` (console plus optional file sink). Adjust `Serilog:MinimumLevel` for verbosity during debugging.

## Related docs

- [ARCHITECTURE.md](ARCHITECTURE.md) — how the app is organized
- [docker/README.md](docker/README.md) — container deployment
