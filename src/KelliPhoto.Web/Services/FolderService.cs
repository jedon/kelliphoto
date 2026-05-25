using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace KelliPhoto.Web.Services;

public class FolderService : IFolderService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IPathService _pathService;
    private readonly ILogger<FolderService> _logger;

    public FolderService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IPathService pathService,
        ILogger<FolderService> logger)
    {
        _contextFactory = contextFactory;
        _pathService = pathService;
        _logger = logger;
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
        
        return await query.OrderBy(f => f.Name).ToListAsync();
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
            .OrderBy(f => f.Name)
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
        
        var folders = await query.OrderBy(f => f.Name).ToListAsync();
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
            
            folder = new Folder
            {
                Path = relativePath, // Store relative path
                Name = name,
                ParentId = parentId,
                CreatedAt = DateTime.UtcNow,
                IsVisible = isVisible
            };
            context.Folders.Add(folder);
        }
        else
        {
            folder.Name = name;
            folder.ParentId = parentId;
            // Ensure .thumbnails, Home Page Highlights, and any folder starting with . remain hidden
            // But ensure other folders are visible (in case they were hidden before)
            var nameLower = name.ToLower();
            if (nameLower == ".thumbnails" 
                || nameLower == "home page highlights" 
                || name.StartsWith("."))
            {
                folder.IsVisible = false;
            }
            else if (!folder.IsVisible)
            {
                // If folder exists but is hidden and shouldn't be, make it visible
                folder.IsVisible = true;
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
            .Take(maxCount)
            .ToListAsync();

        return folderPhotos;
    }

    private async Task<Photo?> GetFolderCoverPhotoAsync(ApplicationDbContext context, int folderId)
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
            .OrderBy(f => f.Name)
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
            .Select(f => new FolderRow(f.Id, f.ParentId, f.Name))
            .ToListAsync();

        var descendantIds = GetDescendantFolderIds(folderRows, folderId);
        var orderedChildFolderIds = folderRows
            .Where(f => descendantIds.Contains(f.Id))
            .OrderBy(f => f.Name)
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
            .Select(f => new FolderRow(f.Id, f.ParentId, f.Name))
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

    private sealed record FolderRow(int Id, int? ParentId, string Name);

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
    }

    public async Task<List<Folder>> GetAllFoldersAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Folders
            .AsNoTracking()
            .OrderBy(f => f.Name)
            .ToListAsync();
    }
}
