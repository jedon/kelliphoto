# Album Admin Management Design

**Date:** 2026-08-09  
**Status:** Approved  
**Approach:** Shared album/photo management layer (Approach 2)

## Summary

Admins manage albums on the home/gallery grid and on `/admin` with always-on controls: add, delete, hide, show, reorder (drag or position number), multiselect bulk ops, rename, and metadata. Albums and photos share a tag system with suggested groups. Photos support full EXIF editing with write-back to image files.

## Decisions

| Topic | Choice |
| --- | --- |
| Add / delete | Create and delete real folders under the gallery path; update DB catalog |
| Hide vs delete | Hide toggles visibility only; delete removes disk + catalog |
| Non-empty delete | Always recursive (child albums + photos) with strong confirmation |
| Rename | Renames directory on disk and rewrites DB paths |
| Scope | One delivery: album management + tags (albums & photos) + photo EXIF editing |
| EXIF persistence | Write back into image files; keep a DB mirror for search/display |
| Tags | Flat free-form tags with suggested groups (People, Butterflies, …) |
| Home UX | Always-on admin chrome (checkboxes, handles, position); click still navigates unless interacting with controls |

## Architecture

### Surfaces

1. **Home/gallery (`FolderBrowser`)** — Public browse unchanged. For admins: always-on checkboxes, drag handles, 1-based position inputs, sticky bulk action bar, Add album control near breadcrumbs. Pencil opens the detail sheet.
2. **`/admin` Gallery tab** — Same sibling management layer for the selected folder’s children. Tree remains for hierarchy navigation and parent context. Upload/cover tools stay in detail.
3. **Detail sheet** (evolved from `FolderEditDialog`) — Single-album edit: name, description, visibility, position, covers, album tags; entry to photo inspector.
4. **Photo inspector** — EXIF form + photo tags; save writes file then refreshes DB mirror. Photo-grid multiselect supports bulk show/hide and bulk tag apply.

### Server layer

- **`IFolderService` extensions:** create album (`mkdir` + DB), rename (disk move + path rewrite), recursive delete, bulk visibility, reorder siblings (ordered ID list or move-to-index). Existing settings/cover APIs remain.
- **`ITagService` (new):** create/list/autocomplete tags; attach/detach on folders and photos; bulk attach.
- **`IPhotoMetadataService` (new):** read EXIF into DB mirror; edit key fields (+ writable advanced tags); write EXIF to file via ImageSharp; refresh mirror from written file.
- All mutating operations require Admin role. Mutations invalidate `IHomePageCache`.

### Path safety

- Every create/rename/delete resolves under `GallerySettings:GalleryPath`. Reject `..` and paths outside the gallery root.
- Protected system folders cannot be deleted or renamed: `Home Page Highlights` and the configured gallery root / synthetic root used by the app today.

## Components & interactions

### Album grid (admin, always-on)

- Per card: checkbox, drag handle, position number (1-based), pencil (detail sheet).
- Card click navigates into the album unless the pointer interaction started on checkbox / handle / position / pencil.
- Drag-and-drop reorders siblings in the current parent; drop commits full sibling order via `ReorderSiblingsAsync(parentId, orderedFolderIds)`.
- Position input on Enter/blur moves that album to the given index among siblings.

### Bulk action bar (≥1 selected)

- Show, Hide, Delete (recursive, strong confirm), Clear selection.
- Bulk add/remove tags.

### Toolbar (admin, near breadcrumbs)

- **Add album** — child of current folder (or top-level on home). Prompt for name; create disk folder + DB row; append to end of `SortOrder`.
- Select all / none for visible siblings.

### Detail sheet

- Name (disk rename on save), description, visibility, position, covers (existing behavior), album tags with group suggestions + autocomplete.

### Photo inspector & photo multiselect

- Editable key EXIF fields + optional advanced writable tags.
- Photo tags (same tag system).
- Save: write file EXIF → update DB mirror. On write failure, do not update DB.
- Multiselect in album photo grid: Show/Hide, bulk tags; open inspector for a single focused photo.
- Photo file delete is out of scope except as part of recursive album delete.

## Data model

### Existing

- `Folder`: `Name`, `Path`, `ParentId`, `IsVisible`, `SortOrder`, `Description`, covers/thumbnail.
- `Photo`: `Filename`, `FilePath`, `FolderId`, `IsVisible`, `DisplayName`, `Description`, `TakenAt`, dimensions, size.

### New: tags

```
Tag
  Id, Name (unique, case-insensitive), Group? (e.g. People, Butterflies)

FolderTag
  FolderId, TagId  (PK composite)

PhotoTag
  PhotoId, TagId   (PK composite)
```

Suggested groups are UI defaults, not a closed enum. New tags may optionally set a group.

### New: photo EXIF mirror

`PhotoExif` (1:1 with `Photo`):

- Columns for search/sort: `DateTaken`, `CameraMake`, `CameraModel`, `Lens`, `FocalLength`, `Aperture`, `ShutterSpeed`, `Iso`, `GpsLatitude`, `GpsLongitude`, `Artist`, `Copyright`, `ImageDescription`.
- `ExtraJson` for remaining EXIF tags.
- Populated on scan/upload and on explicit refresh-from-file.
- After successful write-back, mirror is refreshed from the written file so DB matches disk.

## Operations (disk + DB)

### Create

1. Validate name (non-empty, safe path segment).
2. Resolve parent physical path under gallery root.
3. `Directory.CreateDirectory`.
4. Insert `Folder` with `SortOrder = max(siblings)+1`, `IsVisible = true`.
5. Invalidate home cache.

### Rename

1. Validate new name; ensure target path does not exist.
2. `Directory.Move` old → new under gallery root.
3. In one DB transaction: update folder `Name`/`Path`; rewrite descendant folder paths and photo `FilePath`s.
4. If DB fails after disk move, attempt move-back and report error.
5. Invalidate home cache.

### Delete (recursive)

1. Block if protected folder.
2. Confirm UI: album name + counts of child albums and photos; explicit “Delete permanently”.
3. Resolve and verify physical path is under gallery root.
4. Delete directory tree on disk.
5. Purge catalog subtree (folder tags, photo tags, covers, thumbnails, photos, child folders, folder).
6. If disk delete fails, abort without DB purge; show error.
7. Invalidate home cache.

### Reorder

- Accept full ordered sibling ID list for a parent (null parent = roots / top-level set used by UI).
- Normalize `SortOrder` to `0..n-1`. Last write wins under concurrency.

### Visibility

- Single and bulk `IsVisible` updates; no disk changes.

## Error handling

- Disk permission / missing path / name collision: clear admin message; no silent partial success.
- Bulk ops: per-item summary (succeeded count + failure list).
- EXIF write failure: keep previous DB mirror; show error.
- Contained-path checks on every mutating path operation.

## Testing

- **Unit:** create/rename/delete path rewriting; sibling reorder (list + index); bulk visibility; tag uniqueness and attach/detach; EXIF round-trip on temp JPEG; gallery-root containment; protected-folder blocks.
- **Integration:** admin-only gates; cache invalidation after mutations.
- Extend patterns from `FolderSortOrderTests`, folder visibility tests, and existing folder service tests.

## Out of scope

- Public (non-admin) tagging or EXIF editing.
- Standalone photo file delete outside album recursive delete.
- Moving an album to a different parent (cross-folder move) — reorder is siblings-only in this delivery.
- Non-JPEG EXIF write guarantees beyond what ImageSharp supports for the formats already used by the gallery; unsupported write formats fail with a clear message.

## Success criteria

- Admin can add, delete, hide, show, and reorder albums on home and `/admin` via drag or position number, including multiselect bulk ops.
- Rename updates disk and catalog consistently.
- Albums and photos can be tagged with free-form labels and suggested groups.
- Photo EXIF can be edited and persists in the image file and DB mirror.
- Public gallery behavior unchanged for non-admins; hidden albums remain admin-visible only.
