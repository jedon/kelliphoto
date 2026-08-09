using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace KelliPhoto.Web.Services;

public class PhotoService : IPhotoService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IPathService _pathService;
    private readonly ILogger<PhotoService> _logger;
    private readonly IHomePageCache? _homePageCache;
    private readonly IPhotoMetadataService? _photoMetadataService;
    private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif" };

    public PhotoService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IPathService pathService,
        ILogger<PhotoService> logger,
        IHomePageCache? homePageCache = null,
        IPhotoMetadataService? photoMetadataService = null)
    {
        _contextFactory = contextFactory;
        _pathService = pathService;
        _logger = logger;
        _homePageCache = homePageCache;
        _photoMetadataService = photoMetadataService;
    }

    public async Task<List<Photo>> GetPhotosByFolderIdAsync(int folderId, int skip = 0, int take = 50, bool includeHidden = false)
    {
        _logger.LogDebug("GetPhotosByFolderIdAsync called: FolderId={FolderId}, Skip={Skip}, Take={Take}, IncludeHidden={IncludeHidden}", 
            folderId, skip, take, includeHidden);
        
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        try
        {
            var query = context.Photos
                .AsNoTracking()
                .Where(p => p.FolderId == folderId);
            
            if (!includeHidden)
            {
                query = query.Where(p => p.IsVisible);
            }
            
            query = query
                .OrderByDescending(p => p.TakenAt ?? p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Skip(skip)
                .Take(take);
            
            var photos = await query.ToListAsync();
            
            // Remove duplicates by ID (in case of any data inconsistencies)
            photos = photos.GroupBy(p => p.Id).Select(g => g.First()).ToList();
            
            _logger.LogInformation("SQL query returned {Count} distinct photos for FolderId={FolderId}", photos.Count, folderId);
            
            return photos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPhotosByFolderIdAsync for FolderId={FolderId}: {Message}", folderId, ex.Message);
            throw;
        }
    }

    public async Task<Photo?> GetPhotoByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Photos
            .Include(p => p.Folder)
            .Include(p => p.Thumbnails)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<bool> CanPublicViewPhotoAsync(int photoId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var photo = await context.Photos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == photoId);

        if (photo == null || !photo.IsVisible)
        {
            return false;
        }

        return await CanPublicViewFolderInternalAsync(context, photo.FolderId);
    }

    /// <summary>
    /// Whether a non-admin may browse a folder (list photos). Home Page Highlights is
    /// intentionally hidden from the album tree but is the public homepage gallery.
    /// </summary>
    public async Task<bool> CanPublicViewFolderAsync(int folderId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await CanPublicViewFolderInternalAsync(context, folderId);
    }

    private static async Task<bool> CanPublicViewFolderInternalAsync(ApplicationDbContext context, int folderId)
    {
        while (true)
        {
            var folder = await context.Folders
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == folderId);

            if (folder == null)
            {
                return false;
            }

            // Homepage gallery lives in a system folder that stays hidden from the tree.
            if (string.Equals(folder.Name, "Home Page Highlights", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!folder.IsVisible)
            {
                return false;
            }

            if (folder.ParentId == null)
            {
                return true;
            }

            folderId = folder.ParentId.Value;
        }
    }

    public async Task<int> GetPhotoCountByFolderIdAsync(int folderId, bool includeHidden = false)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Photos.Where(p => p.FolderId == folderId);
        
        if (!includeHidden)
        {
            query = query.Where(p => p.IsVisible);
        }
        
        return await query.CountAsync();
    }

    public async Task<Photo> CreateOrUpdatePhotoAsync(string filePath, int folderId, string filename)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        // Convert to relative path for storage
        var relativePath = _pathService.GetRelativePath(filePath);
        var fullPath = _pathService.GetFullPath(relativePath);
        
        // First check if photo exists in the CORRECT folder (FilePath + FolderId)
        var photo = await context.Photos
            .FirstOrDefaultAsync(p => p.FilePath == relativePath && p.FolderId == folderId);
        var isNew = photo == null;

        if (photo == null)
        {
            // Check if photo exists in a DIFFERENT folder (duplicate in wrong folder)
            var duplicatePhoto = await context.Photos
                .FirstOrDefaultAsync(p => p.FilePath == relativePath && p.FolderId != folderId);
            
            if (duplicatePhoto != null)
            {
                // Photo exists in wrong folder - delete it
                _logger.LogWarning("Found duplicate photo {PhotoId} with FilePath {FilePath} in wrong folder {WrongFolderId}, removing it before adding to folder {CorrectFolderId}", 
                    duplicatePhoto.Id, relativePath, duplicatePhoto.FolderId, folderId);
                context.Photos.Remove(duplicatePhoto);
            }
            
            var fileInfo = new FileInfo(fullPath);
            var fileSize = fileInfo.Exists ? fileInfo.Length : 0;
            DateTime? takenAt = null;
            int? width = null;
            int? height = null;

            try
            {
                if (fileInfo.Exists)
                {
                    using var image = await Image.LoadAsync(fullPath);
                    width = image.Width;
                    height = image.Height;

                    // Try to extract EXIF date
                    if (image.Metadata.ExifProfile != null)
                    {
                        var dateTaken = image.Metadata.ExifProfile.Values
                            .FirstOrDefault(v => v.Tag == ExifTag.DateTimeOriginal || v.Tag == ExifTag.DateTime);
                        if (dateTaken != null && DateTime.TryParse(dateTaken.ToString(), out var date))
                        {
                            takenAt = date;
                        }
                    }

                    // Fallback to file creation time
                    if (takenAt == null)
                    {
                        takenAt = fileInfo.CreationTimeUtc;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading image metadata for {FilePath}", fullPath);
            }

            photo = new Photo
            {
                FilePath = relativePath, // Store relative path
                Filename = filename,
                FolderId = folderId,
                FileSize = fileSize,
                Width = width,
                Height = height,
                TakenAt = takenAt,
                CreatedAt = DateTime.UtcNow
            };
            context.Photos.Add(photo);
        }
        else
        {
            // Photo exists in correct folder, just update metadata
            photo.Filename = filename;
        }

        await context.SaveChangesAsync();

        if (isNew && _photoMetadataService is not null)
        {
            try
            {
                await _photoMetadataService.RefreshFromFileAsync(photo.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh EXIF mirror for photo {PhotoId} at {FilePath}", photo.Id, relativePath);
            }
        }

        return photo;
    }

    public async Task<List<Photo>> ScanPhotosInFolderAsync(int folderId, string folderPath, IScanProgressService? progressService = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var photos = new List<Photo>();

        // Resolve relative path to full path for file operations
        var fullFolderPath = _pathService.GetFullPath(folderPath);
        
        if (!Directory.Exists(fullFolderPath))
        {
            return photos;
        }

        try
        {
            var files = Directory.GetFiles(fullFolderPath)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            _logger.LogInformation("Scanning folder (baseline): {FolderPath} - Found {FileCount} image files", fullFolderPath, files.Count);
            
            // Start progress tracking
            progressService?.StartScan(folderId, files.Count);
            
            int processedCount = 0;
            int errorCount = 0;
            var dbCallCount = 0;
            var saveCallCount = 0;
            
            foreach (var filePath in files)
            {
                try
                {
                    var filename = Path.GetFileName(filePath);
                    processedCount++;
                    
                    var photo = await CreateOrUpdatePhotoAsync(filePath, folderId, filename);
                    photos.Add(photo);
                    dbCallCount += 2; // One query + one save per photo
                    saveCallCount++;
                    
                    // Update progress every photo
                    progressService?.UpdateProgress(folderId, processedCount);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogError(ex, "Error processing photo {Count}/{Total}: {FilePath}", processedCount, files.Count, filePath);
                    // Still update progress even on error
                    progressService?.UpdateProgress(folderId, processedCount);
                }
            }
            
            // Check if folder is "Home Page Highlights" to invalidate cache
            try
            {
                await using var checkContext = await _contextFactory.CreateDbContextAsync();
                var folder = await checkContext.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == folderId);
                if (folder != null && string.Equals(folder.Name, "Home Page Highlights", StringComparison.OrdinalIgnoreCase))
                {
                    _homePageCache?.Invalidate();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking folder name for cache invalidation in ScanPhotosInFolderAsync");
            }
            
            // Mark scan as complete
            progressService?.CompleteScan(folderId);
            
            stopwatch.Stop();
            
            _logger.LogInformation("Completed scanning folder (baseline): {FolderPath} - Processed {ProcessedCount}/{TotalCount} photos successfully, {ErrorCount} errors in {ElapsedMs}ms ({DbCalls} DB calls, {SaveCalls} saves)", 
                fullFolderPath, processedCount - errorCount, files.Count, errorCount, stopwatch.ElapsedMilliseconds, dbCallCount, saveCallCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning photos in folder: {FolderPath}", fullFolderPath);
        }

        return photos;
    }

    public async Task<List<Photo>> ScanPhotosInFolderBatchedAsync(int folderId, string folderPath, IScanProgressService? progressService = null, int batchSize = 50)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var photos = new List<Photo>();

        // Resolve relative path to full path for file operations
        var fullFolderPath = _pathService.GetFullPath(folderPath);
        
        if (!Directory.Exists(fullFolderPath))
        {
            return photos;
        }

        try
        {
            var files = Directory.GetFiles(fullFolderPath)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            _logger.LogInformation("Scanning folder (batched): {FolderPath} - Found {FileCount} image files", fullFolderPath, files.Count);
            
            // Start progress tracking
            progressService?.StartScan(folderId, files.Count);
            
            var loadExistingStopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Load all existing photos for this folder in one query
            // Use relative path as key since that's what we store in DB
            await using var context = await _contextFactory.CreateDbContextAsync();
            var existingPhotosInFolder = await context.Photos
                .Where(p => p.FolderId == folderId)
                .ToDictionaryAsync(p => p.FilePath, p => p);
            
            // Also load all photos with same FilePath in OTHER folders to detect duplicates
            // Get file paths we're about to scan
            var relativePathsToCheck = files.Select(f => _pathService.GetRelativePath(f)).ToList();
            
            // Find photos with same FilePath but in different folders
            var duplicatePhotos = await context.Photos
                .Where(p => relativePathsToCheck.Contains(p.FilePath) && p.FolderId != folderId)
                .ToListAsync();
            
            // Delete duplicates immediately - they shouldn't be in wrong folders
            if (duplicatePhotos.Any())
            {
                _logger.LogWarning("Found {Count} duplicate photos in wrong folders, removing them", duplicatePhotos.Count);
                context.Photos.RemoveRange(duplicatePhotos);
                await context.SaveChangesAsync();
            }
            
            loadExistingStopwatch.Stop();
            _logger.LogInformation("Loaded {Count} existing photos from database in {ElapsedMs}ms (removed {DuplicateCount} duplicates from wrong folders)", 
                existingPhotosInFolder.Count, loadExistingStopwatch.ElapsedMilliseconds, duplicatePhotos.Count);
            
            var newPhotos = new List<Photo>();
            var updatedPhotos = new List<Photo>();
            int processedCount = 0;
            int errorCount = 0;
            var processStopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Process files in batches
            for (int i = 0; i < files.Count; i += batchSize)
            {
                var batch = files.Skip(i).Take(batchSize).ToList();
                var batchStopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                foreach (var filePath in batch)
                {
                    try
                    {
                        var filename = Path.GetFileName(filePath);
                        var relativePath = _pathService.GetRelativePath(filePath);
                        // filePath is already the full path from Directory.GetFiles
                        var fullPath = filePath;
                        
                        processedCount++;
                        
                        // Check if photo exists in correct folder
                        if (existingPhotosInFolder.TryGetValue(relativePath, out var existingPhoto))
                        {
                            // Update existing photo in correct folder
                            existingPhoto.Filename = filename;
                            updatedPhotos.Add(existingPhoto);
                        }
                        else
                        {
                            // Note: Duplicates were already removed at the start of the scan
                            // Create new photo
                            var fileInfo = new FileInfo(fullPath);
                            var fileSize = fileInfo.Exists ? fileInfo.Length : 0;
                            DateTime? takenAt = null;
                            int? width = null;
                            int? height = null;

                            try
                            {
                                if (fileInfo.Exists)
                                {
                                    using var image = await Image.LoadAsync(fullPath);
                                    width = image.Width;
                                    height = image.Height;

                                    // Try to extract EXIF date
                                    if (image.Metadata.ExifProfile != null)
                                    {
                                        var dateTaken = image.Metadata.ExifProfile.Values
                                            .FirstOrDefault(v => v.Tag == ExifTag.DateTimeOriginal || v.Tag == ExifTag.DateTime);
                                        if (dateTaken != null && DateTime.TryParse(dateTaken.ToString(), out var date))
                                        {
                                            takenAt = date;
                                        }
                                    }

                                    // Fallback to file creation time
                                    if (takenAt == null)
                                    {
                                        takenAt = fileInfo.CreationTimeUtc;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error reading image metadata for {FilePath}", fullPath);
                            }

                            var photo = new Photo
                            {
                                FilePath = relativePath,
                                Filename = filename,
                                FolderId = folderId,
                                FileSize = fileSize,
                                Width = width,
                                Height = height,
                                TakenAt = takenAt,
                                CreatedAt = DateTime.UtcNow
                            };
                            
                            newPhotos.Add(photo);
                            existingPhotosInFolder[relativePath] = photo; // Add to dictionary to avoid duplicates in current folder
                        }
                        
                        // Update progress every photo
                        progressService?.UpdateProgress(folderId, processedCount);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.LogError(ex, "Error processing photo {Count}/{Total}: {FilePath}", processedCount, files.Count, filePath);
                        progressService?.UpdateProgress(folderId, processedCount);
                    }
                }
                
                batchStopwatch.Stop();
                
                // Save batch to database
                if (newPhotos.Count > 0 || updatedPhotos.Count > 0)
                {
                    var saveStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    
                    // Use a new context for each batch save to avoid tracking issues
                    await using var saveContext = await _contextFactory.CreateDbContextAsync();
                    
                    // Add new photos
                    if (newPhotos.Count > 0)
                    {
                        saveContext.Photos.AddRange(newPhotos);
                        photos.AddRange(newPhotos);
                    }
                    
                    // Update existing photos - need to attach them first
                    if (updatedPhotos.Count > 0)
                    {
                        foreach (var photo in updatedPhotos)
                        {
                            saveContext.Photos.Attach(photo);
                            saveContext.Entry(photo).Property(p => p.Filename).IsModified = true;
                        }
                        photos.AddRange(updatedPhotos);
                    }
                    
                    await saveContext.SaveChangesAsync();
                    saveStopwatch.Stop();
                    
                    _logger.LogDebug("Saved batch of {NewCount} new and {UpdatedCount} updated photos in {ElapsedMs}ms", 
                        newPhotos.Count, updatedPhotos.Count, saveStopwatch.ElapsedMilliseconds);

                    // Best-effort EXIF mirror population for newly inserted photos
                    if (_photoMetadataService is not null && newPhotos.Count > 0)
                    {
                        foreach (var newPhoto in newPhotos)
                        {
                            try
                            {
                                await _photoMetadataService.RefreshFromFileAsync(newPhoto.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex,
                                    "Failed to refresh EXIF mirror for photo {PhotoId} at {FilePath}",
                                    newPhoto.Id, newPhoto.FilePath);
                            }
                        }
                    }
                    
                    // Clear batch lists
                    newPhotos.Clear();
                    updatedPhotos.Clear();
                }
                
                _logger.LogDebug("Processed batch {BatchNum} ({StartIndex}-{EndIndex}) in {ElapsedMs}ms", 
                    (i / batchSize) + 1, i + 1, Math.Min(i + batchSize, files.Count), batchStopwatch.ElapsedMilliseconds);
            }
            
            processStopwatch.Stop();
            
            // Check if folder is "Home Page Highlights" to invalidate cache
            try
            {
                await using var checkContext = await _contextFactory.CreateDbContextAsync();
                var folder = await checkContext.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == folderId);
                if (folder != null && string.Equals(folder.Name, "Home Page Highlights", StringComparison.OrdinalIgnoreCase))
                {
                    _homePageCache?.Invalidate();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking folder name for cache invalidation in ScanPhotosInFolderBatchedAsync");
            }
            
            // Mark scan as complete
            progressService?.CompleteScan(folderId);
            
            stopwatch.Stop();
            
            _logger.LogInformation("Completed scanning folder (batched): {FolderPath} - Processed {ProcessedCount}/{TotalCount} photos successfully, {ErrorCount} errors in {TotalMs}ms (Load: {LoadMs}ms, Process: {ProcessMs}ms)", 
                fullFolderPath, processedCount - errorCount, files.Count, errorCount, 
                stopwatch.ElapsedMilliseconds, loadExistingStopwatch.ElapsedMilliseconds, processStopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning photos in folder: {FolderPath}", fullFolderPath);
        }

        return photos;
    }

    public async Task<bool> NeedsFolderScanAsync(int folderId, string folderPath)
    {
        // Resolve relative path to full path
        var fullFolderPath = _pathService.GetFullPath(folderPath);
        
        if (!Directory.Exists(fullFolderPath))
        {
            return false;
        }

        await using var context = await _contextFactory.CreateDbContextAsync();

        // Get all photos in DB for this folder
        var dbPhotos = await context.Photos
            .AsNoTracking()
            .Where(p => p.FolderId == folderId)
            .ToListAsync();

        // Get all image files in folder
        var files = Directory.GetFiles(fullFolderPath)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        // Quick comparison: check if counts match
        if (files.Count != dbPhotos.Count)
        {
            _logger.LogInformation("Folder {FolderId} needs scan: file count mismatch (DB: {DbCount}, FS: {FsCount})", 
                folderId, dbPhotos.Count, files.Count);
            return true;
        }

        // Create dictionaries for quick lookup - convert DB relative paths to full paths for comparison
        var dbFiles = dbPhotos.ToDictionary(
            p => _pathService.GetFullPath(p.FilePath), 
            p => p.FileSize);
        var fsFiles = files.ToDictionary(f => f, f => new FileInfo(f).Length);

        // Check for missing files in DB or size mismatches
        foreach (var file in files)
        {
            if (!dbFiles.ContainsKey(file))
            {
                _logger.LogInformation("Folder {FolderId} needs scan: new file found: {FileName}", folderId, Path.GetFileName(file));
                return true;
            }

            if (fsFiles[file] != dbFiles[file])
            {
                _logger.LogInformation("Folder {FolderId} needs scan: file size changed: {FileName}", folderId, Path.GetFileName(file));
                return true;
            }
        }

        // Check for files in DB that no longer exist
        foreach (var dbPhoto in dbPhotos)
        {
            var dbPhotoFullPath = _pathService.GetFullPath(dbPhoto.FilePath);
            if (!fsFiles.ContainsKey(dbPhotoFullPath))
            {
                _logger.LogInformation("Folder {FolderId} needs scan: file missing from filesystem: {FileName}", 
                    folderId, dbPhoto.Filename);
                return true;
            }
        }

        return false;
    }

    public static bool IsSupportedImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    public async Task UpdatePhotoVisibilityAsync(int photoId, bool isVisible)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var photo = await context.Photos.FindAsync(photoId);
        if (photo == null)
        {
            throw new ArgumentException($"Photo with ID {photoId} not found", nameof(photoId));
        }
        photo.IsVisible = isVisible;
        await context.SaveChangesAsync();

        try
        {
            var folder = await context.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == photo.FolderId);
            if (folder != null && string.Equals(folder.Name, "Home Page Highlights", StringComparison.OrdinalIgnoreCase))
            {
                _homePageCache?.Invalidate();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking folder name for cache invalidation in UpdatePhotoVisibilityAsync. Falling back to unconditional invalidation.");
            _homePageCache?.Invalidate();
        }
    }

    public async Task SetPhotosVisibilityAsync(IReadOnlyList<int> photoIds, bool isVisible)
    {
        if (photoIds is null || photoIds.Count == 0)
            return;

        await using var context = await _contextFactory.CreateDbContextAsync();
        var distinctIds = photoIds.Distinct().ToList();
        var photos = await context.Photos
            .Where(p => distinctIds.Contains(p.Id))
            .ToListAsync();

        foreach (var photo in photos)
        {
            photo.IsVisible = isVisible;
        }

        await context.SaveChangesAsync();
        _homePageCache?.Invalidate();
    }

    public async Task UpdatePhotoDisplayNameAsync(int photoId, string? displayName)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var photo = await context.Photos.FindAsync(photoId);
        if (photo == null)
        {
            throw new ArgumentException($"Photo with ID {photoId} not found", nameof(photoId));
        }
        photo.DisplayName = displayName;
        await context.SaveChangesAsync();
    }

    public async Task UpdatePhotoDescriptionAsync(int photoId, string? description)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var photo = await context.Photos.FindAsync(photoId);
        if (photo == null)
        {
            throw new ArgumentException($"Photo with ID {photoId} not found", nameof(photoId));
        }
        photo.Description = description;
        await context.SaveChangesAsync();
    }

    public async Task<List<Photo>> GetAllPhotosByFolderIdAsync(int folderId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Photos
            .AsNoTracking()
            .Where(p => p.FolderId == folderId)
            .OrderByDescending(p => p.TakenAt ?? p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .ToListAsync();
    }

    public async Task<AdjacentPhotos?> GetAdjacentPhotoIdsAsync(int photoId, bool includeHidden = false)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var currentPhoto = await context.Photos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == photoId);

        if (currentPhoto == null)
        {
            return null;
        }

        if (!includeHidden && !currentPhoto.IsVisible)
        {
            return null;
        }

        var query = context.Photos
            .AsNoTracking()
            .Where(p => p.FolderId == currentPhoto.FolderId);

        if (!includeHidden)
        {
            query = query.Where(p => p.IsVisible);
        }

        var orderedIds = await query
            .OrderByDescending(p => p.TakenAt ?? p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Select(p => p.Id)
            .ToListAsync();

        int indexInList = orderedIds.IndexOf(photoId);
        if (indexInList == -1)
        {
            return null;
        }

        int index = indexInList + 1;
        int total = orderedIds.Count;

        int? prevId = indexInList > 0 ? orderedIds[indexInList - 1] : null;
        int? nextId = indexInList < total - 1 ? orderedIds[indexInList + 1] : null;

        return new AdjacentPhotos(prevId, nextId, index, total);
    }
}
