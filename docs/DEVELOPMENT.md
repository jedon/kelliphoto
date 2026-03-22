# Development

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL (local or remote) for the application database
- Optional: Docker for stack parity with production

## Clone and configure

1. Clone the repository. From the **repository root**, run the commands below.

2. Copy or edit configuration (do not commit secrets):

   - `src/KelliPhoto.Web/appsettings.json` — base settings (override locally with `appsettings.Development.json` or environment variables).
   - Set `ConnectionStrings:DefaultConnection` to your PostgreSQL instance.
   - Set `GallerySettings` paths to a directory of images you can read from the dev machine.

3. Apply EF Core migrations:

   ```powershell
   dotnet ef database update --project src\KelliPhoto.Web
   ```

4. Run the site:

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

The test project lives under `tests/KelliPhoto.Web.Tests`.

## Logging

Serilog is configured in `appsettings.json` (console plus optional file sink). Adjust `Serilog:MinimumLevel` for verbosity during debugging.

## Related docs

- [ARCHITECTURE.md](ARCHITECTURE.md) — how the app is organized
- [../docker/README.md](../docker/README.md) — container deployment
