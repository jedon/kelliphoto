# Album Admin Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Give admins always-on album management on home and `/admin` (add/delete/hide/show/reorder with drag or position, multiselect), plus album/photo tags and photo EXIF edit with write-back to disk.

**Architecture:** Shared admin management UI layer on top of `FolderBrowser` and `/admin`; extend `IFolderService` for disk-backed CRUD/reorder; add `ITagService` and `IPhotoMetadataService`; new EF entities `Tag`, `FolderTag`, `PhotoTag`, `PhotoExif`.

**Tech Stack:** ASP.NET Core Blazor Server, EF Core + PostgreSQL, ImageSharp EXIF, xUnit, existing `IPathService` / `IHomePageCache`.

**Spec:** `docs/superpowers/specs/2026-08-09-album-admin-management-design.md`

---

## File map

| File | Responsibility |
| --- | --- |
| `src/KelliPhoto.Web/Data/Models/Tag.cs` | Tag entity |
| `src/KelliPhoto.Web/Data/Models/FolderTag.cs` | Folder↔Tag join |
| `src/KelliPhoto.Web/Data/Models/PhotoTag.cs` | Photo↔Tag join |
| `src/KelliPhoto.Web/Data/Models/PhotoExif.cs` | EXIF DB mirror |
| `src/KelliPhoto.Web/Data/ApplicationDbContext.cs` | DbSets + relationships |
| `src/KelliPhoto.Web/Migrations/*_AddTagsAndPhotoExif.cs` | Migration |
| `src/KelliPhoto.Web/Services/IFolderService.cs` + `FolderService.cs` | Create/rename/delete/reorder/bulk visibility + path guards |
| `src/KelliPhoto.Web/Services/ITagService.cs` + `TagService.cs` | Tags CRUD + attach |
| `src/KelliPhoto.Web/Services/IPhotoMetadataService.cs` + `PhotoMetadataService.cs` | EXIF read/write/mirror |
| `src/KelliPhoto.Web/Program.cs` | DI registration |
| `src/KelliPhoto.Web/Components/AlbumAdminToolbar.razor` | Add / select all / bulk bar |
| `src/KelliPhoto.Web/Components/AlbumAdminGrid.razor` | Admin sibling grid (checkbox, drag, position) |
| `src/KelliPhoto.Web/Components/FolderEditDialog.razor` | Extend with tags + disk rename |
| `src/KelliPhoto.Web/Components/PhotoMetadataDialog.razor` | EXIF + photo tags |
| `src/KelliPhoto.Web/Components/FolderBrowser.razor` | Wire admin grid/toolbar when `IsAdmin` |
| `src/KelliPhoto.Web/Components/PhotoGrid.razor` | Admin multiselect + open metadata |
| `src/KelliPhoto.Web/Pages/Admin.razor` | Reuse album admin grid for children |
| `src/KelliPhoto.Web/wwwroot/js/albumAdminDnD.js` | Drag-drop helpers for Blazor |
| `tests/KelliPhoto.Web.Tests/FolderAlbumCrudTests.cs` | Create/rename/delete/reorder/protect |
| `tests/KelliPhoto.Web.Tests/TagServiceTests.cs` | Tags |
| `tests/KelliPhoto.Web.Tests/PhotoMetadataServiceTests.cs` | EXIF round-trip |

---

### Task 1: Path containment helper + folder service API stubs

**Files:**
- Modify: `src/KelliPhoto.Web/Services/IPathService.cs`, `PathService.cs`
- Modify: `src/KelliPhoto.Web/Services/IFolderService.cs`
- Create: `tests/KelliPhoto.Web.Tests/PathContainmentTests.cs`

- [x] **Step 1: Write failing tests for containment**

```csharp
public class PathContainmentTests
{
    [Fact]
    public void EnsureUnderGalleryRoot_AllowsChildPath()
    {
        var gallery = Path.Combine(Path.GetTempPath(), "gal-" + Guid.NewGuid());
        Directory.CreateDirectory(gallery);
        try
        {
            var ps = CreatePathService(gallery);
            var child = Path.Combine(gallery, "albums", "a");
            Assert.Equal(Path.GetFullPath(child), ps.EnsureUnderGalleryRoot(child));
        }
        finally { Directory.Delete(gallery, true); }
    }

    [Fact]
    public void EnsureUnderGalleryRoot_RejectsEscape()
    {
        var gallery = Path.Combine(Path.GetTempPath(), "gal-" + Guid.NewGuid());
        Directory.CreateDirectory(gallery);
        try
        {
            var ps = CreatePathService(gallery);
            Assert.Throws<InvalidOperationException>(() =>
                ps.EnsureUnderGalleryRoot(Path.Combine(gallery, "..", "outside")));
        }
        finally { Directory.Delete(gallery, true); }
    }

    private static IPathService CreatePathService(string galleryPath)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GallerySettings:GalleryPath"] = galleryPath
            }).Build();
        return new PathService(config);
    }
}
```

- [x] **Step 2: Run tests — expect FAIL** (method missing)

```powershell
dotnet test tests/KelliPhoto.Web.Tests/KelliPhoto.Web.Tests.csproj --filter "FullyQualifiedName~PathContainmentTests"
```

- [x] **Step 3: Implement `EnsureUnderGalleryRoot` on `IPathService` / `PathService`**

```csharp
// IPathService
string EnsureUnderGalleryRoot(string fullPath);

// PathService — normalize both sides, require fullPath starts with gallery root + separator (or equals root)
public string EnsureUnderGalleryRoot(string fullPath)
{
    var root = Path.GetFullPath(GalleryBasePath)
        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    var candidate = Path.GetFullPath(fullPath);
    if (candidate.Equals(root, StringComparison.OrdinalIgnoreCase))
        return candidate;
    var prefix = root + Path.DirectorySeparatorChar;
    if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Path is outside gallery root: {fullPath}");
    return candidate;
}
```

- [x] **Step 4: Extend `IFolderService` with new method signatures (throw `NotImplementedException` in `FolderService` until Task 2–3)**

```csharp
Task<Folder> CreateAlbumAsync(int? parentFolderId, string name);
Task RenameAlbumAsync(int folderId, string newName);
Task DeleteAlbumRecursiveAsync(int folderId);
Task ReorderSiblingsAsync(int? parentFolderId, IReadOnlyList<int> orderedFolderIds);
Task SetFoldersVisibilityAsync(IReadOnlyList<int> folderIds, bool isVisible);
Task<(int ChildAlbumCount, int PhotoCount)> GetAlbumSubtreeCountsAsync(int folderId);
bool IsProtectedFolder(Folder folder);
```

`IsProtectedFolder`: true when name equals `"Home Page Highlights"` (case-insensitive) or folder is the single gallery root used by `GetTopLevelFoldersAsync` (name match for root like `kelli.photo` / path is gallery root relative). Prefer: protect by name `Home Page Highlights` and by `ParentId == null` root folder(s) that represent the mount root.

- [x] **Step 5: Run containment tests — PASS; commit**

```powershell
git add src/KelliPhoto.Web/Services/IPathService.cs src/KelliPhoto.Web/Services/PathService.cs src/KelliPhoto.Web/Services/IFolderService.cs src/KelliPhoto.Web/Services/FolderService.cs tests/KelliPhoto.Web.Tests/PathContainmentTests.cs
git commit -m "feat: add gallery path containment and album CRUD service API"
```

---

### Task 2: Create, rename, delete, reorder, bulk visibility (TDD)

**Files:**
- Modify: `src/KelliPhoto.Web/Services/FolderService.cs`
- Create: `tests/KelliPhoto.Web.Tests/FolderAlbumCrudTests.cs`
- Follow fixture pattern from `FolderSortOrderTests` (in-memory EF + temp `GallerySettings:GalleryPath`)

- [x] **Step 1: Write failing CRUD tests**

Cover at least:
1. `CreateAlbumAsync` creates directory under parent and DB row with next `SortOrder`.
2. `RenameAlbumAsync` renames directory and rewrites child folder paths + photo `FilePath`s.
3. `DeleteAlbumRecursiveAsync` removes disk tree and DB subtree; protected folder throws.
4. `ReorderSiblingsAsync` sets `SortOrder` to match ID list order.
5. `SetFoldersVisibilityAsync` updates multiple folders.
6. Escape / outside-root paths throw (via malicious name `..`).

Use a dedicated temp gallery directory per test class (not `Path.GetTempPath()` root alone).

- [x] **Step 2: Run — expect FAIL / NotImplemented**

- [x] **Step 3: Implement methods in `FolderService`**

Create:
- Sanitize name: trim; reject empty, `.`, `..`, path separators, invalid filename chars.
- Parent physical path = `_pathService.GetFullPath(parent.Path)` then `EnsureUnderGalleryRoot`.
- New path = combine parent + name; ensure under root; `Directory.CreateDirectory`; insert folder.

Rename:
- Block if protected.
- `Directory.Move` after `EnsureUnderGalleryRoot` on old and new.
- Transaction: update folder + all descendants’ `Path` (string replace of old path prefix) + photos’ `FilePath`.
- On DB failure after move: attempt `Directory.Move` back.

Delete:
- Block if protected.
- Counts helper for UI.
- `EnsureUnderGalleryRoot` → `Directory.Delete(path, recursive: true)` → purge DB (covers, thumbnails, photos, child folders depth-first or cascade where configured). Note: Folder parent restrict may require deleting children first in code.
- If directory missing but DB exists: purge DB only after confirming path would have been under root (log warning).

Reorder:
- Load siblings for `parentFolderId`; verify set equality with `orderedFolderIds`; assign `SortOrder = index`; save; invalidate cache.

Bulk visibility: update all IDs; invalidate cache.

- [x] **Step 4: Tests PASS; commit**

```powershell
git commit -m "feat: implement disk-backed album create, rename, delete, reorder"
```

---

### Task 3: Tag entities, migration, TagService

**Files:**
- Create models: `Tag.cs`, `FolderTag.cs`, `PhotoTag.cs`
- Modify: `ApplicationDbContext.cs`
- Add EF migration `AddTagsAndPhotoExif` (PhotoExif table can be added empty in Task 4 if preferred — **this task: tags only**; Task 4 adds PhotoExif in same or follow-up migration)
- Create: `ITagService.cs`, `TagService.cs`
- Register in `Program.cs`
- Create: `tests/KelliPhoto.Web.Tests/TagServiceTests.cs`

**Preferred groups constant** (UI + service):

```csharp
public static class TagGroups
{
    public static readonly string[] Suggested = ["People", "Butterflies", "Places", "Events"];
}
```

- [x] **Step 1: Failing TagService tests** — create tag (case-insensitive unique), attach/detach folder & photo, autocomplete by prefix, bulk attach to folders.

- [x] **Step 2: Implement models + DbContext config**

```csharp
public class Tag
{
    public int Id { get; set; }
    [Required, MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(100)] public string? Group { get; set; }
}
// FolderTag / PhotoTag composite keys
```

Unique index on `Tag.Name` with store lowercase or use EF value comparer — simplest: normalize to trimmed original display but uniqueness via `Name.ToLower()` shadow or enforce in service with `EF.Functions.ILike` / in-memory `StringComparer.OrdinalIgnoreCase`.

- [x] **Step 3: Migration + TagService + DI**

- [x] **Step 4: Tests PASS; commit**

```powershell
git commit -m "feat: add tag model and TagService for albums and photos"
```

---

### Task 4: PhotoExif model + PhotoMetadataService (read/write)

**Files:**
- Create: `PhotoExif.cs`, `IPhotoMetadataService.cs`, `PhotoMetadataService.cs`
- Migration for `PhotoExif` (or extend Task 3 migration if not applied yet)
- Hook: after photo create in `PhotoService` scan/upload, call `RefreshFromFileAsync` (best-effort log on failure)
- Tests: `PhotoMetadataServiceTests.cs` with a temp JPEG written via ImageSharp

- [x] **Step 1: Failing test — write EXIF DateTaken/Artist to temp JPEG, RefreshFromFile, assert mirror; UpdateAsync writes file + mirror**

- [x] **Step 2: Implement `PhotoExif` 1:1 with `PhotoId` PK/FK cascade delete**

Columns per spec + `ExtraJson` (nvarchar/json).

- [x] **Step 3: `PhotoMetadataService`
  - `RefreshFromFileAsync(photoId)` — resolve path via `IPathService.ResolveExistingPhotoFilePath`, load ImageSharp ExifProfile, map known tags, stash rest in ExtraJson.
  - `GetAsync(photoId)`
  - `UpdateAsync(photoId, PhotoExifUpdate dto)` — load image, set Exif values, save image to disk (same path), then refresh mirror. On IO/ImageSharp failure: throw; do not update DB.
  - Invalidate home cache if `TakenAt` / description-related fields change on Photo row (`Photo.TakenAt` sync from DateTaken).

- [x] **Step 4: Tests PASS; commit**

```powershell
git commit -m "feat: add PhotoExif mirror and EXIF read/write service"
```

---

### Task 5: Album admin JS drag-drop + AlbumAdminGrid/Toolbar components

**Files:**
- Create: `wwwroot/js/albumAdminDnD.js`
- Create: `Components/AlbumAdminToolbar.razor` (+ css if needed)
- Create: `Components/AlbumAdminGrid.razor` (+ css)
- Register script in `Pages/_Host.cshtml` or `MainLayout` if not already loading wwwroot js

**Behavior:**
- Parameters: `IReadOnlyList<Folder> Albums`, `int? ParentFolderId`, `EventCallback OnChanged`
- Selection: `HashSet<int>`
- Position inputs 1-based → call `ReorderSiblingsAsync` after moving one id to index
- Drag: JS sortable or HTML5 DnD calling `DotNet.invokeMethodAsync` / `IJSObjectReference` with new order → `ReorderSiblingsAsync`
- Toolbar: Add album (prompt name → `CreateAlbumAsync`), Select all/none, bulk Show/Hide/Delete (confirm with counts from `GetAlbumSubtreeCountsAsync`), bulk tag add/remove via `ITagService`
- Pencil → `EventCallback<int> OnEditAlbum`

- [x] **Step 1: Implement JS + components (manual verify via existing app later; unit-test service already done)**

- [x] **Step 2: Commit**

```powershell
git commit -m "feat: add shared album admin grid and toolbar components"
```

---

### Task 6: Wire FolderBrowser (home) + Admin page

**Files:**
- Modify: `FolderBrowser.razor` — when `IsAdmin`, render `AlbumAdminToolbar` + `AlbumAdminGrid` instead of plain folder cards for child/root album lists; keep public card markup for non-admin
- Modify: `Admin.razor` — for selected folder, show `AlbumAdminGrid` of children + toolbar; keep existing detail panel for covers/upload or open `FolderEditDialog`
- Extend: `FolderEditDialog.razor` — on name save use `RenameAlbumAsync` when name changed; add tag editor using `ITagService`; keep covers/visibility/description via existing `UpdateFolderSettingsAsync` / cover APIs

- [x] **Step 1: Wire UI; ensure non-admin path unchanged**

- [x] **Step 2: Commit**

```powershell
git commit -m "feat: wire album admin management into gallery and admin page"
```

---

### Task 7: Photo grid multiselect + PhotoMetadataDialog

**Files:**
- Create: `Components/PhotoMetadataDialog.razor`
- Modify: `PhotoGrid.razor` — when admin: checkboxes, bulk show/hide (`IPhotoService` visibility if present; else add `SetPhotosVisibilityAsync`), bulk tags, button to open metadata dialog for one photo
- If `IPhotoService` lacks visibility bulk, add minimal method mirroring folders

- [x] **Step 1: Implement dialog bound to `IPhotoMetadataService` + `ITagService`**

- [x] **Step 2: Commit**

```powershell
git commit -m "feat: add photo EXIF/tag inspector and admin photo multiselect"
```

---

### Task 8: Verification suite + polish

- [x] **Step 1: Run full unit test project**

```powershell
dotnet test tests/KelliPhoto.Web.Tests/KelliPhoto.Web.Tests.csproj
```

Expected: all pass.

- [x] **Step 2: Fix any regressions from FolderService constructor/DI**

- [x] **Step 3: Final commit if fixes needed**

```powershell
git commit -m "test: fix album admin regressions and polish"
```

---

## Spec coverage checklist

| Spec requirement | Task |
| --- | --- |
| Create album on disk + DB | 2, 5–6 |
| Recursive delete disk + DB; hide ≠ delete | 2, 5 |
| Rename disk + path rewrite | 2, 6 |
| Reorder drag + position number | 2, 5 |
| Multiselect show/hide/delete/tags | 5, 7 |
| Always-on home admin chrome | 6 |
| `/admin` shared layer | 6 |
| Tags albums + photos, suggested groups | 3, 5–7 |
| EXIF write-back + DB mirror | 4, 7 |
| Path containment + protected folders | 1–2 |
| Cache invalidation | 2–4 |
| Tests | 1–4, 8 |

## Plan self-review notes

- No TBD placeholders in task contracts.
- Method names aligned across tasks: `CreateAlbumAsync`, `RenameAlbumAsync`, `DeleteAlbumRecursiveAsync`, `ReorderSiblingsAsync`, `SetFoldersVisibilityAsync`.
- PhotoExif migration timing: Task 3 tags-only; Task 4 adds PhotoExif (two migrations OK).
- Cross-parent album move explicitly out of scope per spec.
