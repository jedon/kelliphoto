# Architecture overview

Kelli Photo is an **ASP.NET Core Blazor Server** application backed by **PostgreSQL** via **Entity Framework Core**. Thumbnails and web-sized images are generated with **ImageSharp** and cached on disk.

## High-level layout

| Area | Role |
|------|------|
| `Pages/` | Blazor routable pages (gallery, contact, admin, etc.) |
| `Components/` | Reusable UI (folder browser, photo grid, viewer) |
| `Controllers/` | MVC API endpoints (images, admin actions, contact, scan progress) |
| `Data/` | EF Core `DbContext`, entities (`Folder`, `Photo`, Identity) |
| `Services/` | Domain and infrastructure logic |
| `Shared/` | Layout and navigation chrome |

The host uses `MapFallbackToPage("/_Host")` so Blazor handles the main UI; controllers serve binary/image routes and JSON APIs.

## Major services

Registration lives in `Program.cs`. Summary:

- **Singleton**: `IPathService`, `IScanProgressService`, `IRateLimitService` — paths, background scan signaling, rate limiting.
- **Scoped**: `IFolderService`, `IPhotoService`, `IThumbnailService`, `IWebImageService`, `INavigationService`, `IEmailService` — catalog queries, thumbnails, web renditions, URLs/navigation, outbound mail.
- **Hosted service**: `CatalogService` — indexes files from the configured gallery path into the database.

Identity uses `AddDbContextPool` with the same `ApplicationDbContext` as the rest of the app. A separate `IDbContextFactory<ApplicationDbContext>` supports thread-safe context creation where needed.

## Authorization

- Default Identity UI (Razor Pages under `Areas/Identity`) handles login/logout.
- Policy **`AdminOnly`** requires role `Admin` for protected operations (e.g. admin UI and APIs).

## Configuration sections (reference)

These keys are documented at a high level; use environment-specific files or secrets stores for production values.

- **`ConnectionStrings:DefaultConnection`** — Npgsql connection string.
- **`GallerySettings`** — gallery root, thumbnail and web image paths, optional legacy Windows path mapping and folder name aliases.
- **`WatermarkSettings`** — optional overlay for delivered images.
- **`Admin`** — seed credentials for the initial admin user (change in production).
- **`Email`** — SMTP settings for the contact form and notifications.

## Docker

Production-style runs use the files under `docker/` (multi-stage build, Compose stack, nginx). See [docker/README.md](../docker/README.md) for service ports, volumes, and migration commands inside the container.
