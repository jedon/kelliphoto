using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace KelliPhoto.Web.Services;

public class FolderService : IFolderService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IPathService _pathService;
    private readonly ILogger<FolderService> _logger;
    private readonly IHomePageCache? _homePageCache;
    private bool? _folderCoverPhotosTableAvailable;

    public FolderService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IPathService pathService,
        ILogger<FolderService> logger,
        IHomePageCache? homePageCache = null)
    {
        _contextFactory = contextFactory;
        _pathService = pathService;
        _logger = logger;
        _homePageCache = homePageCache;
    }

    public async Task<List<Folder>> GetRootFoldersAsync(bool includeHidden = false)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Folders
            .AsNoTracking()
            .Where(f => f.ParentId == null)
            .Where(f => !f.Name.StartsWith(".") && 
                       f.Name.ToLower() != "home page highlights" &&
                       f.Name.ToLower() != "testfolder");
        
        if (!includeHidden)
        {
            query = query.Where(f => f.IsVisible);
        }
        
        return await query.OrderBy(f => f.SortOrder).ThenBy(f => f.Name).ToListAsync();
    }

    public async Task<List<Folder>> GetTopLevelFoldersAsync(bool includeHidden = false)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        // Find the root "kelli.photo" folder (or whatever the root gallery folder is)
        // We'll look for folders that have no parent, then get their children
        var query = context.Folders
            .AsNoTracking()
            .Where(f => f.ParentId == null);
        
        if (!includeHidden)
        {
            query = query.Where(f => f.IsVisible);
        }
        
        var rootFolders = await query.ToListAsync();
        
        // If there's a single root folder (like "kelli.photo"), return its children
        // Otherwise return the root folders themselves
        if (rootFolders.Count == 1)
        {
            return await GetChildFoldersAsync(rootFolders[0].Id, includeHidden);
        }
        
        // If multiple root folders, return them (excluding any named "kelli.photo", starting with ".", "Home Page Highlights", or "testfolder")
        return rootFolders
            .Where(f => f.Name.ToLower() != "kelli.photo" &&
                       !f.Name.StartsWith(".") &&
                       f.Name.ToLower() != "home page highlights" &&
                       f.Name.ToLower() != "testfolder")
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .ToList();
    }

    public async Task<Folder?> GetFolderByNameAsync(string name, int? parentId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        // Use case-insensitive comparison
        var nameLower = name.ToLower();
        
        IQueryable<Folder> query;
        
        if (parentId.HasValue)
        {
            query = context.Folders
                .AsNoTracking()
                .Where(f => f.Name.ToLower() == nameLower && f.ParentId == parentId);
        }
        else
        {
            // If no parent specified, search in top-level folders (same logic as GetTopLevelFoldersAsync)
            // First find the root kelli.photo folder, then search its children
            var rootFolder = await context.Folders
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.ParentId == null && f.Name.ToLower() == "kelli.photo".ToLower());
            
            Folder? folder = null;
            
            if (rootFolder != null)
            {
                // Search in children of root folder - include hidden folders for navigation purposes
                folder = await context.Folders
                    .AsNoTracking()
                    .Where(f => f.Name.ToLower() == nameLower && 
                               f.ParentId == rootFolder.Id &&
                               !f.Name.StartsWith("."))
                    .FirstOrDefaultAsync();
                
                _logger.LogDebug("GetFolderByNameAsync: Searched for '{Name}' in root folder children. Found: {Found}", name, folder != null);
            }
            
            // If not found in root children, search in all folders (including hidden for navigation)
            if (folder == null)
            {
                folder = await context.Folders
                    .AsNoTracking()
                    .Where(f => f.Name.ToLower() == nameLower &&
                               !f.Name.StartsWith("."))
                    .FirstOrDefaultAsync();
                
                _logger.LogDebug("GetFolderByNameAsync: Searched for '{Name}' in all folders. Found: {Found}", name, folder != null);
            }
            
            if (folder == null)
            {
                _logger.LogWarning("GetFolderByNameAsync: Folder '{Name}' not found. Checked top-level folders and all visible folders.", name);
            }
            
            return folder;
        }
        
        return await query.FirstOrDefaultAsync();
    }

    public async Task<Folder?> GetFolderByIdAsync(int id)
    {
        // Load folder with immediate parent only
        // Use split query to avoid multiple collection include warning
        // Use AsNoTracking for read-only operations to reduce overhead
        // We only load the immediate parent to avoid deep recursion and concurrency issues
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Folders
            .AsNoTracking()
            .Include(f => f.Parent)
            .AsSplitQuery()
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<List<Folder>> GetBreadcrumbPathAsync(int folderId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var breadcrumbs = new List<Folder>();
        
        // Get the folder first
        var folder = await context.Folders
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == folderId);
        
        if (folder == null)
            return breadcrumbs;
        
        // Walk up the parent chain
        var currentId = folder.ParentId;
        while (currentId.HasValue)
        {
            var parent = await context.Folders
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == currentId.Value);
            
            if (parent == null)
                break;
            
            // Skip "Home Page Highlights" and "kelli.photo" folders in breadcrumbs
            var parentNameLower = parent.Name.ToLower();
            if (parentNameLower != "home page highlights" &&
                parentNameLower != "kelli.photo")
            {
                breadcrumbs.Insert(0, parent);
            }
            
            currentId = parent.ParentId;
        }
        
        return breadcrumbs;
    }

    public async Task<List<Folder>> GetChildFoldersAsync(int parentId, bool includeHidden = false)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Folders
            .AsNoTracking()
            .Where(f => f.ParentId == parentId)
            .Where(f => !f.Name.StartsWith(".") && 
                       f.Name.ToLower() != "home page highlights" &&
                       f.Name.ToLower() != "testfolder");
        
        if (!includeHidden)
        {
            query = query.Where(f => f.IsVisible);
        }
        
        var folders = await query.OrderBy(f => f.SortOrder).ThenBy(f => f.Name).ToListAsync();
        _logger.LogDebug("GetChildFoldersAsync: Found {Count} folders for parent {ParentId}", folders.Count, parentId);
        return folders;
    }

    public async Task<Folder> CreateOrUpdateFolderAsync(string path, string name, int? parentId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        // Convert to relative path for storage
        var relativePath = _pathService.GetRelativePath(path);
        
        var folder = await context.Folders
            .FirstOrDefaultAsync(f => f.Path == relativePath);

        if (folder == null)
        {
            // Hide .thumbnails, Home Page Highlights, and any folder starting with . by default
            var nameLower = name.ToLower();
            var isVisible = nameLower != ".thumbnails" 
                         && nameLower != "home page highlights" 
                         && !name.StartsWith(".");
            
            var maxSort = await context.Folders
                .Where(f => f.ParentId == parentId)
                .Select(f => (int?)f.SortOrder)
                .MaxAsync() ?? -1;

            folder = new Folder
            {
                Path = relativePath, // Store relative path
                Name = name,
                ParentId = parentId,
                CreatedAt = DateTime.UtcNow,
                IsVisible = isVisible,
                SortOrder = maxSort + 1
            };
            context.Folders.Add(folder);
        }
        else
        {
            folder.Name = name;
            folder.ParentId = parentId;
            // Ensure .thumbnails, Home Page Highlights, and any folder starting with . remain hidden
            var nameLower = name.ToLower();
            if (nameLower == ".thumbnails" 
                || nameLower == "home page highlights" 
                || name.StartsWith("."))
            {
                folder.IsVisible = false;
            }
        }

        await context.SaveChangesAsync();
        return folder;
    }

    public async Task<List<Folder>> ScanFoldersAsync(string rootPath)
    {
        var folders = new List<Folder>();
        
        // Normalize and get full path
        var fullRootPath = _pathService.GetFullPath(rootPath);
        
        if (!Directory.Exists(fullRootPath))
        {
            _logger.LogWarning("Gallery path does not exist: {Path}", fullRootPath);
            return folders;
        }

        await ScanFoldersRecursiveAsync(fullRootPath, null, folders);
        return folders;
    }

    private async Task ScanFoldersRecursiveAsync(string currentPath, int? parentId, List<Folder> folders)
    {
        try
        {
            var folderName = Path.GetFileName(currentPath);
            if (string.IsNullOrEmpty(folderName))
            {
                folderName = currentPath;
            }

            // Skip directories starting with . (e.g., .web, .thumbnails, etc.)
            if (folderName.StartsWith("."))
            {
                _logger.LogDebug("Skipping hidden/system folder: {FolderName} ({FolderPath})", folderName, currentPath);
                return;
            }

            _logger.LogDebug("Scanning folder: {FolderName} ({FolderPath})", folderName, currentPath);
            var folder = await CreateOrUpdateFolderAsync(currentPath, folderName, parentId);
            folders.Add(folder);
            _logger.LogTrace("Processed folder: {FolderName} (ID: {FolderId})", folderName, folder.Id);

            var subdirectories = Directory.GetDirectories(currentPath);
            int processedCount = 0;
            foreach (var subdirectory in subdirectories)
            {
                var subFolderName = Path.GetFileName(subdirectory);
                // Skip directories starting with . when recursing
                if (!string.IsNullOrEmpty(subFolderName) && subFolderName.StartsWith("."))
                {
                    _logger.LogDebug("Skipping hidden/system subfolder: {FolderName}", subFolderName);
                    continue;
                }

                await ScanFoldersRecursiveAsync(subdirectory, folder.Id, folders);
                
                // Add a small delay every 10 folders to reduce database load
                // This helps prevent conflicts with user requests
                processedCount++;
                if (processedCount % 10 == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning folder: {Path}", currentPath);
        }
    }

    public async Task<Photo?> GetFolderThumbnailAsync(int folderId)
    {
        var photos = await GetFolderThumbnailPhotosAsync(folderId, maxCount: 1);
        return photos.FirstOrDefault();
    }

    public async Task<IReadOnlyList<Photo>> GetFolderThumbnailPhotosAsync(int folderId, int maxCount = 4)
    {
        if (maxCount < 1)
        {
            return Array.Empty<Photo>();
        }

        await using var context = await _contextFactory.CreateDbContextAsync();

        if (await IsFolderCoverPhotosAvailableAsync(context))
        {
            var curatedCovers = await context.FolderCoverPhotos
                .AsNoTracking()
                .Where(fcp => fcp.FolderId == folderId)
                .OrderBy(fcp => fcp.SortOrder)
                .Take(maxCount)
                .Select(fcp => fcp.Photo)
                .ToListAsync();

            if (curatedCovers.Count > 0)
            {
                return curatedCovers;
            }
        }

        var folder = await context.Folders
            .AsNoTracking()
            .Include(f => f.ThumbnailPhoto)
            .FirstOrDefaultAsync(f => f.Id == folderId);

        if (folder?.ThumbnailPhotoId.HasValue == true && folder.ThumbnailPhoto != null)
        {
            return new[] { folder.ThumbnailPhoto };
        }

        var childFolders = await GetChildFoldersAsync(folderId);
        if (childFolders.Count > 0)
        {
            var childThumbnails = new List<Photo>();
            foreach (var child in childFolders.Take(maxCount))
            {
                var cover = await GetFolderCoverPhotoAsync(context, child.Id);
                if (cover != null)
                {
                    childThumbnails.Add(cover);
                }
            }

            if (childThumbnails.Count > 0)
            {
                return childThumbnails;
            }
        }

        var folderPhotos = await context.Photos
            .AsNoTracking()
            .Where(p => p.FolderId == folderId)
            .OrderBy(p => p.TakenAt ?? p.CreatedAt)
            .Take(1)
            .ToListAsync();

        return folderPhotos;
    }

    private async Task<Photo?> GetFolderCoverPhotoAsync(ApplicationDbContext context, int folderId)
    {
        if (await IsFolderCoverPhotosAvailableAsync(context))
        {
            var curated = await context.FolderCoverPhotos
                .AsNoTracking()
                .Where(fcp => fcp.FolderId == folderId)
                .OrderBy(fcp => fcp.SortOrder)
                .Select(fcp => fcp.Photo)
                .FirstOrDefaultAsync();

            if (curated != null)
            {
                return curated;
            }
        }

        var folder = await context.Folders
            .AsNoTracking()
            .Include(f => f.ThumbnailPhoto)
            .FirstOrDefaultAsync(f => f.Id == folderId);

        if (folder?.ThumbnailPhotoId.HasValue == true && folder.ThumbnailPhoto != null)
        {
            return folder.ThumbnailPhoto;
        }

        var firstPhoto = await context.Photos
            .AsNoTracking()
            .Where(p => p.FolderId == folderId)
            .OrderBy(p => p.TakenAt ?? p.CreatedAt)
            .FirstOrDefaultAsync();

        if (firstPhoto != null)
        {
            return firstPhoto;
        }

        var childFolders = await context.Folders
            .AsNoTracking()
            .Where(f => f.ParentId == folderId)
            .Where(f => !f.Name.StartsWith(".") &&
                        f.Name.ToLower() != "home page highlights" &&
                        f.Name.ToLower() != "testfolder")
            .Where(f => f.IsVisible)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .ToListAsync();

        foreach (var child in childFolders)
        {
            var cover = await GetFolderCoverPhotoAsync(context, child.Id);
            if (cover != null)
            {
                return cover;
            }
        }

        return null;
    }

    private async Task<bool> IsFolderCoverPhotosAvailableAsync(ApplicationDbContext context)
    {
        if (_folderCoverPhotosTableAvailable == true)
        {
            return true;
        }

        if (_folderCoverPhotosTableAvailable == false)
        {
            return false;
        }

        try
        {
            _ = await context.FolderCoverPhotos.AsNoTracking().Select(fcp => fcp.FolderId).FirstOrDefaultAsync();
            _folderCoverPhotosTableAvailable = true;
        }
        catch (Exception ex) when (IsMissingFolderCoverPhotosTable(ex))
        {
            _folderCoverPhotosTableAvailable = false;
            _logger.LogWarning(
                "FolderCoverPhotos table is missing; using legacy thumbnail resolution. Apply migration 20260525120000_AddFolderCoverPhotos.");
        }

        return _folderCoverPhotosTableAvailable == true;
    }

    private static bool IsMissingFolderCoverPhotosTable(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is PostgresException pg && pg.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                return true;
            }
        }

        return false;
    }

    public async Task SetFolderThumbnailAsync(int folderId, int photoId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        var folder = await context.Folders.FindAsync(folderId);
        if (folder == null)
        {
            throw new ArgumentException($"Folder with ID {folderId} not found", nameof(folderId));
        }

        var photo = await context.Photos.FindAsync(photoId);
        if (photo == null)
        {
            throw new ArgumentException($"Photo with ID {photoId} not found", nameof(photoId));
        }

        if (!await IsPhotoValidForFolderCoverAsync(context, folderId, photo.FolderId))
        {
            throw new ArgumentException($"Photo {photoId} does not belong to folder {folderId} or its subfolders.");
        }

        folder.ThumbnailPhotoId = photoId;
        await context.SaveChangesAsync();

        await SyncLegacyThumbnailToCoverPhotosAsync(context, folderId);
    }

    public async Task<bool> IsPhotoValidForFolderCoverAsync(int folderId, int photoId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var photoFolderId = await context.Photos
            .AsNoTracking()
            .Where(p => p.Id == photoId)
            .Select(p => (int?)p.FolderId)
            .FirstOrDefaultAsync();

        if (photoFolderId == null)
        {
            return false;
        }

        return await IsPhotoValidForFolderCoverAsync(context, folderId, photoFolderId.Value);
    }

    public async Task<IReadOnlyList<Photo>> GetPhotosForCoverPickerAsync(int folderId, int maxCount = 120)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var folderRows = await context.Folders
            .AsNoTracking()
            .Select(f => new FolderRow(f.Id, f.ParentId, f.Name, f.SortOrder))
            .ToListAsync();

        var descendantIds = GetDescendantFolderIds(folderRows, folderId);
        var orderedChildFolderIds = folderRows
            .Where(f => descendantIds.Contains(f.Id) && f.Id != folderId)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .Select(f => f.Id)
            .ToList();

        var orderedFolderIds = new List<int> { folderId };
        orderedFolderIds.AddRange(orderedChildFolderIds);

        var photos = await context.Photos
            .AsNoTracking()
            .Include(p => p.Folder)
            .Where(p => orderedFolderIds.Contains(p.FolderId))
            .ToListAsync();

        var photosByFolder = photos.GroupBy(p => p.FolderId).ToDictionary(g => g.Key, g => g.ToList());
        var result = new List<Photo>();

        foreach (var fid in orderedFolderIds)
        {
            if (!photosByFolder.TryGetValue(fid, out var folderPhotos))
            {
                continue;
            }

            foreach (var photo in folderPhotos.OrderBy(p => p.TakenAt ?? DateTime.MaxValue).ThenBy(p => p.Id))
            {
                result.Add(photo);
                if (result.Count >= maxCount)
                {
                    return result;
                }
            }
        }

        return result;
    }

    private static async Task<bool> IsPhotoValidForFolderCoverAsync(
        ApplicationDbContext context,
        int folderId,
        int photoFolderId)
    {
        if (photoFolderId == folderId)
        {
            return true;
        }

        var folderRows = await context.Folders
            .AsNoTracking()
            .Select(f => new FolderRow(f.Id, f.ParentId, f.Name, f.SortOrder))
            .ToListAsync();

        var descendants = GetDescendantFolderIds(folderRows, folderId);
        return descendants.Contains(photoFolderId);
    }

    private static HashSet<int> GetDescendantFolderIds(IReadOnlyList<FolderRow> folders, int rootFolderId)
    {
        var childrenByParent = folders
            .Where(f => f.ParentId.HasValue)
            .GroupBy(f => f.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Id).ToList());

        var descendants = new HashSet<int>();
        var queue = new Queue<int>();

        if (childrenByParent.TryGetValue(rootFolderId, out var directChildren))
        {
            foreach (var childId in directChildren)
            {
                queue.Enqueue(childId);
            }
        }

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!descendants.Add(id))
            {
                continue;
            }

            if (childrenByParent.TryGetValue(id, out var grandchildren))
            {
                foreach (var grandchildId in grandchildren)
                {
                    queue.Enqueue(grandchildId);
                }
            }
        }

        return descendants;
    }

    private sealed record FolderRow(int Id, int? ParentId, string Name, int SortOrder);

    public async Task<IReadOnlyList<Photo>> GetFolderCoverPhotosAsync(int folderId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.FolderCoverPhotos
            .AsNoTracking()
            .Where(fcp => fcp.FolderId == folderId)
            .OrderBy(fcp => fcp.SortOrder)
            .Select(fcp => fcp.Photo)
            .ToListAsync();
    }

    public async Task SetFolderCoverPhotosAsync(int folderId, IReadOnlyList<int> photoIds)
    {
        if (photoIds.Count > 4)
        {
            throw new ArgumentException("A folder can have at most 4 cover photos.", nameof(photoIds));
        }

        await using var context = await _contextFactory.CreateDbContextAsync();

        var folder = await context.Folders.FindAsync(folderId)
            ?? throw new ArgumentException($"Folder with ID {folderId} not found", nameof(folderId));

        var distinctIds = photoIds.Distinct().ToList();
        if (distinctIds.Count != photoIds.Count)
        {
            throw new ArgumentException("Duplicate photo IDs are not allowed.", nameof(photoIds));
        }

        if (distinctIds.Count > 0)
        {
            var photos = await context.Photos
                .Where(p => distinctIds.Contains(p.Id))
                .ToListAsync();

            if (photos.Count != distinctIds.Count)
            {
                throw new ArgumentException("One or more photo IDs were not found.", nameof(photoIds));
            }

            foreach (var photo in photos)
            {
                if (!await IsPhotoValidForFolderCoverAsync(context, folderId, photo.FolderId))
                {
                    throw new ArgumentException(
                        $"Photo {photo.Id} does not belong to folder {folderId} or its subfolders.",
                        nameof(photoIds));
                }
            }
        }

        var existing = await context.FolderCoverPhotos
            .Where(fcp => fcp.FolderId == folderId)
            .ToListAsync();
        context.FolderCoverPhotos.RemoveRange(existing);

        for (var i = 0; i < distinctIds.Count; i++)
        {
            context.FolderCoverPhotos.Add(new FolderCoverPhoto
            {
                FolderId = folderId,
                PhotoId = distinctIds[i],
                SortOrder = i
            });
        }

        folder.ThumbnailPhotoId = distinctIds.Count > 0 ? distinctIds[0] : null;
        await context.SaveChangesAsync();
        _homePageCache?.Invalidate();
    }

    public async Task ClearFolderCoverPhotosAsync(int folderId)
    {
        await SetFolderCoverPhotosAsync(folderId, Array.Empty<int>());
    }

    public async Task<bool> FolderHasChildrenAsync(int folderId, bool includeHidden = true)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Folders.AsNoTracking().Where(f => f.ParentId == folderId);
        if (!includeHidden)
        {
            query = query.Where(f => f.IsVisible);
        }

        return await query.AnyAsync();
    }

    private static async Task SyncLegacyThumbnailToCoverPhotosAsync(ApplicationDbContext context, int folderId)
    {
        var folder = await context.Folders.FindAsync(folderId);
        if (folder?.ThumbnailPhotoId == null)
        {
            return;
        }

        var existing = await context.FolderCoverPhotos
            .Where(fcp => fcp.FolderId == folderId)
            .ToListAsync();
        if (existing.Count > 0)
        {
            return;
        }

        context.FolderCoverPhotos.Add(new FolderCoverPhoto
        {
            FolderId = folderId,
            PhotoId = folder.ThumbnailPhotoId.Value,
            SortOrder = 0
        });
        await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Photo>> GetPhotosInFolderForCoverPickerAsync(int folderId, int maxCount = 120)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Photos
            .AsNoTracking()
            .Where(p => p.FolderId == folderId)
            .OrderBy(p => p.TakenAt ?? p.CreatedAt)
            .ThenBy(p => p.Id)
            .Take(maxCount)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Folder>> GetSiblingFoldersAsync(int folderId, bool includeHidden = true)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var folder = await context.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == folderId);
        if (folder == null)
        {
            return Array.Empty<Folder>();
        }

        var query = context.Folders.AsNoTracking().Where(f => f.ParentId == folder.ParentId);
        if (!includeHidden)
        {
            query = query.Where(f => f.IsVisible);
        }

        return await query.OrderBy(f => f.SortOrder).ThenBy(f => f.Name).ToListAsync();
    }

    public async Task UpdateFolderSettingsAsync(int folderId, string name, int sortOrder, bool isVisible, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Folder name is required.", nameof(name));
        }

        await using var context = await _contextFactory.CreateDbContextAsync();
        var folder = await context.Folders.FindAsync(folderId)
            ?? throw new ArgumentException($"Folder with ID {folderId} not found", nameof(folderId));

        var siblings = await context.Folders
            .Where(f => f.ParentId == folder.ParentId && f.Id != folderId)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .ToListAsync();

        var targetIndex = Math.Clamp(sortOrder, 0, siblings.Count);
        siblings.Insert(targetIndex, folder);

        for (var i = 0; i < siblings.Count; i++)
        {
            siblings[i].SortOrder = i;
        }

        folder.Name = name.Trim();
        folder.IsVisible = isVisible;
        folder.Description = description;

        await context.SaveChangesAsync();
        _homePageCache?.Invalidate();
    }

    public async Task UpdateFolderVisibilityAsync(int folderId, bool isVisible)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var folder = await context.Folders.FindAsync(folderId);
        if (folder == null)
        {
            throw new ArgumentException($"Folder with ID {folderId} not found", nameof(folderId));
        }
        folder.IsVisible = isVisible;
        await context.SaveChangesAsync();
        _homePageCache?.Invalidate();
    }

    public async Task UpdateFolderDescriptionAsync(int folderId, string? description)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var folder = await context.Folders.FindAsync(folderId);
        if (folder == null)
        {
            throw new ArgumentException($"Folder with ID {folderId} not found", nameof(folderId));
        }
        folder.Description = description;
        await context.SaveChangesAsync();
        _homePageCache?.Invalidate();
    }

    public async Task<List<Folder>> GetAllFoldersAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Folders
            .AsNoTracking()
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .ToListAsync();
    }

    public async Task<Folder> CreateAlbumAsync(int? parentFolderId, string name)
    {
        var sanitized = SanitizeAlbumName(name);

        await using var context = await _contextFactory.CreateDbContextAsync();

        // Never create a second ParentId == null row (breaks GetTopLevelFoldersAsync single-root mode).
        // Null parent means "under gallery mount root".
        Folder parent;
        if (parentFolderId is null)
        {
            parent = await FindGalleryMountRootAsync(context)
                ?? throw new InvalidOperationException(
                    "Cannot create album: gallery mount root folder was not found in the catalog.");
            parentFolderId = parent.Id;
        }
        else
        {
            parent = await context.Folders.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == parentFolderId.Value)
                ?? throw new ArgumentException($"Parent folder {parentFolderId} not found.", nameof(parentFolderId));
        }

        var parentRelative = parent.Path ?? string.Empty;
        var parentFull = _pathService.EnsureUnderGalleryRoot(_pathService.GetFullPath(parentRelative));
        var newFull = _pathService.EnsureUnderGalleryRoot(Path.Combine(parentFull, sanitized));
        var newRelative = _pathService.GetRelativePath(newFull);

        if (Directory.Exists(newFull))
            throw new InvalidOperationException($"Directory already exists: {sanitized}");

        if (await context.Folders.AnyAsync(f => f.Path == newRelative))
            throw new InvalidOperationException($"Album path already exists in catalog: {newRelative}");

        Directory.CreateDirectory(newFull);

        try
        {
            var maxSort = await context.Folders
                .Where(f => f.ParentId == parentFolderId)
                .Select(f => (int?)f.SortOrder)
                .MaxAsync() ?? -1;

            var folder = new Folder
            {
                Name = sanitized,
                Path = newRelative,
                ParentId = parentFolderId,
                CreatedAt = DateTime.UtcNow,
                IsVisible = true,
                SortOrder = maxSort + 1
            };
            context.Folders.Add(folder);
            await context.SaveChangesAsync();
            _homePageCache?.Invalidate();
            return folder;
        }
        catch
        {
            try
            {
                if (Directory.Exists(newFull) && !Directory.EnumerateFileSystemEntries(newFull).Any())
                    Directory.Delete(newFull);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Failed to clean up directory after create failure: {Path}", newFull);
            }

            throw;
        }
    }

    public async Task RenameAlbumAsync(int folderId, string newName)
    {
        var sanitized = SanitizeAlbumName(newName);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var folder = await context.Folders.FirstOrDefaultAsync(f => f.Id == folderId)
            ?? throw new ArgumentException($"Folder with ID {folderId} not found", nameof(folderId));

        if (IsProtectedFolder(folder))
            throw new InvalidOperationException($"Cannot rename protected folder '{folder.Name}'.");

        var oldRelative = folder.Path ?? string.Empty;
        var oldFull = _pathService.EnsureUnderGalleryRoot(_pathService.GetFullPath(oldRelative));

        var parentDir = Path.GetDirectoryName(oldFull)
            ?? _pathService.EnsureUnderGalleryRoot(_pathService.GetFullPath(string.Empty));
        var newFull = _pathService.EnsureUnderGalleryRoot(Path.Combine(parentDir, sanitized));
        var newRelative = _pathService.GetRelativePath(newFull);

        if (string.Equals(oldFull, newFull, StringComparison.OrdinalIgnoreCase))
        {
            folder.Name = sanitized;
            await context.SaveChangesAsync();
            _homePageCache?.Invalidate();
            return;
        }

        if (Directory.Exists(newFull))
            throw new InvalidOperationException($"Target directory already exists: {sanitized}");

        if (await context.Folders.AnyAsync(f => f.Id != folderId && f.Path == newRelative))
            throw new InvalidOperationException($"Album path already exists in catalog: {newRelative}");

        if (!Directory.Exists(oldFull))
            throw new DirectoryNotFoundException($"Album directory not found: {oldFull}");

        Directory.Move(oldFull, newFull);

        try
        {
            var descendantIds = await GetDescendantFolderIdsAsync(context, folderId);
            var subtreeIds = descendantIds.Append(folderId).ToList();

            var foldersToRewrite = await context.Folders
                .Where(f => subtreeIds.Contains(f.Id))
                .ToListAsync();

            foreach (var f in foldersToRewrite)
            {
                f.Path = RewritePathPrefix(f.Path, oldRelative, newRelative);
                if (f.Id == folderId)
                    f.Name = sanitized;
            }

            var photos = await context.Photos
                .Where(p => subtreeIds.Contains(p.FolderId))
                .ToListAsync();

            foreach (var photo in photos)
            {
                photo.FilePath = RewritePathPrefix(photo.FilePath, oldRelative, newRelative);
            }

            await context.SaveChangesAsync();
            _homePageCache?.Invalidate();
        }
        catch
        {
            try
            {
                if (Directory.Exists(newFull) && !Directory.Exists(oldFull))
                    Directory.Move(newFull, oldFull);
            }
            catch (Exception moveBackEx)
            {
                _logger.LogError(moveBackEx,
                    "DB rename failed and disk move-back also failed. Old={Old} New={New}",
                    oldFull, newFull);
            }

            throw;
        }
    }

    public async Task DeleteAlbumRecursiveAsync(int folderId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var folder = await context.Folders.FirstOrDefaultAsync(f => f.Id == folderId)
            ?? throw new ArgumentException($"Folder with ID {folderId} not found", nameof(folderId));

        if (IsProtectedFolder(folder))
            throw new InvalidOperationException($"Cannot delete protected folder '{folder.Name}'.");

        var fullPath = _pathService.EnsureUnderGalleryRoot(_pathService.GetFullPath(folder.Path ?? string.Empty));

        if (Directory.Exists(fullPath))
        {
            try
            {
                Directory.Delete(fullPath, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete album directory {Path}; aborting DB purge", fullPath);
                throw;
            }
        }
        else
        {
            _logger.LogWarning(
                "Album directory missing at {Path}; purging catalog subtree for folder {FolderId}",
                fullPath, folderId);
        }

        await PurgeFolderSubtreeFromDbAsync(context, folderId);
        _homePageCache?.Invalidate();
    }

    public async Task ReorderSiblingsAsync(int? parentFolderId, IReadOnlyList<int> orderedFolderIds)
    {
        if (orderedFolderIds is null)
            throw new ArgumentNullException(nameof(orderedFolderIds));

        await using var context = await _contextFactory.CreateDbContextAsync();

        // Null parent means "siblings under gallery mount root" (same remap as CreateAlbumAsync).
        if (parentFolderId is null)
        {
            var mount = await FindGalleryMountRootAsync(context)
                ?? throw new InvalidOperationException(
                    "Cannot reorder albums: gallery mount root folder was not found in the catalog.");
            parentFolderId = mount.Id;
        }

        var siblings = await context.Folders
            .Where(f => f.ParentId == parentFolderId)
            .ToListAsync();

        // Require exact sibling count and distinct IDs so duplicates like [1,1,2] cannot
        // collapse via HashSet and silently pass a set-equality check.
        var orderedSet = orderedFolderIds.ToHashSet();
        if (orderedFolderIds.Count != siblings.Count
            || orderedSet.Count != orderedFolderIds.Count
            || !siblings.Select(f => f.Id).ToHashSet().SetEquals(orderedSet))
        {
            throw new ArgumentException(
                "orderedFolderIds must be a distinct permutation of all sibling folder IDs for the parent.",
                nameof(orderedFolderIds));
        }

        var byId = siblings.ToDictionary(f => f.Id);
        for (var i = 0; i < orderedFolderIds.Count; i++)
        {
            byId[orderedFolderIds[i]].SortOrder = i;
        }

        await context.SaveChangesAsync();
        _homePageCache?.Invalidate();
    }

    public async Task SetFoldersVisibilityAsync(IReadOnlyList<int> folderIds, bool isVisible)
    {
        if (folderIds is null || folderIds.Count == 0)
            return;

        await using var context = await _contextFactory.CreateDbContextAsync();
        var folders = await context.Folders
            .Where(f => folderIds.Contains(f.Id))
            .ToListAsync();

        foreach (var folder in folders)
        {
            folder.IsVisible = isVisible;
        }

        await context.SaveChangesAsync();
        _homePageCache?.Invalidate();
    }

    public async Task<(int ChildAlbumCount, int PhotoCount)> GetAlbumSubtreeCountsAsync(int folderId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        if (!await context.Folders.AnyAsync(f => f.Id == folderId))
            throw new ArgumentException($"Folder with ID {folderId} not found", nameof(folderId));

        var descendantIds = await GetDescendantFolderIdsAsync(context, folderId);
        var allIds = descendantIds.Append(folderId).ToList();
        var photoCount = await context.Photos.CountAsync(p => allIds.Contains(p.FolderId));
        return (descendantIds.Count, photoCount);
    }

    public bool IsProtectedFolder(Folder folder)
    {
        if (string.Equals(folder.Name, "Home Page Highlights", StringComparison.OrdinalIgnoreCase))
            return true;

        return IsGalleryMountRoot(folder);
    }

    private async Task<Folder?> FindGalleryMountRootAsync(ApplicationDbContext context)
    {
        var candidates = await context.Folders.AsNoTracking()
            .Where(f => f.ParentId == null)
            .ToListAsync();

        return candidates.FirstOrDefault(IsGalleryMountRoot);
    }

    private bool IsGalleryMountRoot(Folder folder)
    {
        var relative = (folder.Path ?? string.Empty).Trim()
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\');

        if (string.IsNullOrEmpty(relative) || relative == ".")
            return true;

        try
        {
            var full = _pathService.EnsureUnderGalleryRoot(_pathService.GetFullPath(folder.Path ?? string.Empty));
            var galleryRoot = _pathService.EnsureUnderGalleryRoot(_pathService.GetFullPath(string.Empty));
            return string.Equals(full, galleryRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string SanitizeAlbumName(string name)
    {
        if (name is null)
            throw new ArgumentException("Album name is required.", nameof(name));

        var trimmed = name.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed is "." or "..")
            throw new ArgumentException("Invalid album name.", nameof(name));

        if (trimmed.Contains('/') || trimmed.Contains('\\'))
            throw new ArgumentException("Album name cannot contain path separators.", nameof(name));

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Album name contains invalid characters.", nameof(name));

        return trimmed;
    }

    private static string RewritePathPrefix(string path, string oldPrefix, string newPrefix)
    {
        path ??= string.Empty;
        oldPrefix ??= string.Empty;
        newPrefix ??= string.Empty;

        var pathN = path.Replace('\\', '/');
        var oldN = oldPrefix.Replace('\\', '/');
        var newN = newPrefix.Replace('\\', '/');

        if (string.Equals(pathN, oldN, StringComparison.OrdinalIgnoreCase))
            return newPrefix;

        var oldWithSep = string.IsNullOrEmpty(oldN) ? string.Empty : oldN.TrimEnd('/') + "/";
        if (string.IsNullOrEmpty(oldWithSep))
        {
            // Renaming mount root relative "" — unlikely; treat as prefix of everything
            var combined = string.IsNullOrEmpty(newN) ? pathN : newN.TrimEnd('/') + "/" + pathN.TrimStart('/');
            return combined.Replace('/', Path.DirectorySeparatorChar);
        }

        if (pathN.StartsWith(oldWithSep, StringComparison.OrdinalIgnoreCase))
        {
            var suffix = pathN.Substring(oldWithSep.Length);
            var rewritten = string.IsNullOrEmpty(newN)
                ? suffix
                : newN.TrimEnd('/') + "/" + suffix;
            return rewritten.Replace('/', Path.DirectorySeparatorChar);
        }

        return path;
    }

    private static async Task<List<int>> GetDescendantFolderIdsAsync(ApplicationDbContext context, int folderId)
    {
        var result = new List<int>();
        var frontier = new Queue<int>();
        frontier.Enqueue(folderId);

        while (frontier.Count > 0)
        {
            var id = frontier.Dequeue();
            var children = await context.Folders
                .AsNoTracking()
                .Where(f => f.ParentId == id)
                .Select(f => f.Id)
                .ToListAsync();

            foreach (var childId in children)
            {
                result.Add(childId);
                frontier.Enqueue(childId);
            }
        }

        return result;
    }

    private async Task PurgeFolderSubtreeFromDbAsync(ApplicationDbContext context, int rootFolderId)
    {
        // Prefer an explicit transaction so a mid-purge failure does not leave a half-deleted catalog.
        // InMemory (and some test providers) do not support transactions — proceed without in that case.
        IDbContextTransaction? tx = null;
        try
        {
            tx = await context.Database.BeginTransactionAsync();
        }
        catch (InvalidOperationException)
        {
            // InMemory provider: transactions unsupported; purge continues without transactional atomicity.
            _logger.LogDebug("Database provider does not support transactions; purging folder {FolderId} without a transaction.", rootFolderId);
        }

        try
        {
            await PurgeFolderSubtreeFromDbCoreAsync(context, rootFolderId);

            if (tx != null)
                await tx.CommitAsync();
        }
        catch
        {
            if (tx != null)
                await tx.RollbackAsync();
            throw;
        }
        finally
        {
            if (tx != null)
                await tx.DisposeAsync();
        }
    }

    private static async Task PurgeFolderSubtreeFromDbCoreAsync(ApplicationDbContext context, int rootFolderId)
    {
        var descendantIds = await GetDescendantFolderIdsAsync(context, rootFolderId);
        var allIds = descendantIds.Append(rootFolderId).ToList();

        var photoIds = await context.Photos
            .Where(p => allIds.Contains(p.FolderId))
            .Select(p => p.Id)
            .ToListAsync();

        // Clear thumbnail FKs that point at photos we are about to remove (including other folders)
        var foldersHoldingThumbs = await context.Folders
            .Where(f => f.ThumbnailPhotoId != null && photoIds.Contains(f.ThumbnailPhotoId.Value))
            .ToListAsync();
        foreach (var f in foldersHoldingThumbs)
            f.ThumbnailPhotoId = null;

        // Also clear thumbnails on folders being deleted (may point outside subtree)
        var foldersInSubtree = await context.Folders
            .Where(f => allIds.Contains(f.Id))
            .ToListAsync();
        foreach (var f in foldersInSubtree)
            f.ThumbnailPhotoId = null;

        await context.SaveChangesAsync();

        var covers = await context.FolderCoverPhotos
            .Where(c => allIds.Contains(c.FolderId) || photoIds.Contains(c.PhotoId))
            .ToListAsync();
        context.FolderCoverPhotos.RemoveRange(covers);

        var photos = await context.Photos
            .Where(p => allIds.Contains(p.FolderId))
            .ToListAsync();
        context.Photos.RemoveRange(photos);
        await context.SaveChangesAsync();

        // Delete deepest folders first (ParentId Restrict)
        var remaining = foldersInSubtree.ToDictionary(f => f.Id);
        while (remaining.Count > 0)
        {
            var leaves = remaining.Values
                .Where(f => !remaining.Values.Any(c => c.ParentId == f.Id))
                .ToList();

            if (leaves.Count == 0)
            {
                // Cycle safeguard — delete whatever remains
                context.Folders.RemoveRange(remaining.Values);
                remaining.Clear();
                break;
            }

            foreach (var leaf in leaves)
            {
                context.Folders.Remove(leaf);
                remaining.Remove(leaf.Id);
            }

            await context.SaveChangesAsync();
        }
    }
}
