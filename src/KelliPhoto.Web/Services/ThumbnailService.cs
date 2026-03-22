using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace KelliPhoto.Web.Services;

public class ThumbnailService : IThumbnailService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IPhotoService _photoService;
    private readonly IPathService _pathService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ThumbnailService> _logger;

    public ThumbnailService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IPhotoService photoService,
        IPathService pathService,
        IConfiguration configuration,
        ILogger<ThumbnailService> logger)
    {
        _contextFactory = contextFactory;
        _photoService = photoService;
        _pathService = pathService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GetOrCreateThumbnailAsync(int photoId, int size = 300)
    {
        var photo = await _photoService.GetPhotoByIdAsync(photoId);
        if (photo == null)
        {
            throw new FileNotFoundException($"Photo with id {photoId} not found");
        }

        await using var context = await _contextFactory.CreateDbContextAsync();

        // Check if thumbnail already exists
        var existingThumbnail = await context.Thumbnails
            .FirstOrDefaultAsync(t => t.PhotoId == photoId && t.Size == size);

        if (existingThumbnail != null)
        {
            var thumbnailFullPath = _pathService.GetFullPath(existingThumbnail.FilePath);
            if (File.Exists(thumbnailFullPath))
            {
                return thumbnailFullPath;
            }

            _logger.LogWarning(
                "Thumbnail DB row for photo {PhotoId} size {Size} points to missing file {Path}; removing stale row",
                photoId, size, thumbnailFullPath);
            context.Thumbnails.Remove(existingThumbnail);
            await context.SaveChangesAsync();
        }

        // Create thumbnail
        var thumbnailPath = await CreateThumbnailAsync(photo, size);
        return thumbnailPath;
    }

    public async Task<Stream> GetThumbnailStreamAsync(int photoId, int size = 300)
    {
        var thumbnailPath = await GetOrCreateThumbnailAsync(photoId, size);
        
        // thumbnailPath is already a full path from GetOrCreateThumbnailAsync
        if (!File.Exists(thumbnailPath))
        {
            throw new FileNotFoundException($"Thumbnail not found: {thumbnailPath}");
        }

        return File.OpenRead(thumbnailPath);
    }

    public async Task DeleteThumbnailAsync(int photoId, int size)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        var thumbnail = await context.Thumbnails
            .FirstOrDefaultAsync(t => t.PhotoId == photoId && t.Size == size);

        if (thumbnail != null)
        {
            var thumbnailFullPath = _pathService.GetFullPath(thumbnail.FilePath);
            if (File.Exists(thumbnailFullPath))
            {
                try
                {
                    File.Delete(thumbnailFullPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error deleting thumbnail file: {FilePath}", thumbnailFullPath);
                }
            }

            context.Thumbnails.Remove(thumbnail);
            await context.SaveChangesAsync();
        }
    }

    private async Task<string> CreateThumbnailAsync(Photo photo, int size)
    {
        var photoFullPath = _pathService.ResolveExistingPhotoFilePath(photo.FilePath);
        
        _logger.LogDebug(
            "Creating thumbnail for photo {PhotoId} (size: {Size}), StoredPath: {StoredPath}, ResolvedPath: {ResolvedPath}",
            photo.Id, size, photo.FilePath, photoFullPath ?? _pathService.GetFullPath(photo.FilePath));
        
        if (photoFullPath == null || !File.Exists(photoFullPath))
        {
            var attempted = _pathService.GetFullPath(photo.FilePath);
            _logger.LogError("Photo file not found: {FilePath} for photo {PhotoId}", attempted, photo.Id);
            throw new FileNotFoundException($"Photo file not found: {attempted}");
        }

        var thumbnailBasePath = _configuration["GallerySettings:ThumbnailPath"] 
            ?? Path.Combine(Path.GetDirectoryName(photoFullPath) ?? "", ".thumbnails");
        
        // Normalize thumbnail path
        thumbnailBasePath = _pathService.NormalizePath(thumbnailBasePath);
        
        _logger.LogDebug("Thumbnail base path: {ThumbnailBasePath}", thumbnailBasePath);
        
        try
        {
            Directory.CreateDirectory(thumbnailBasePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create thumbnail directory: {ThumbnailBasePath}", thumbnailBasePath);
            throw;
        }

        var thumbnailFileName = $"{photo.Id}_{size}.jpg";
        var thumbnailPath = Path.Combine(thumbnailBasePath, thumbnailFileName);

        try
        {
            _logger.LogDebug("Loading image from {FilePath}", photoFullPath);
            using var image = await Image.LoadAsync(photoFullPath);

            var (width, height) = CalculateThumbnailDimensions(image.Width, image.Height, size);

            _logger.LogDebug("Resizing image to {Width}x{Height}", width, height);

            image.Mutate(x => x
                .Resize(new ResizeOptions
                {
                    Size = new Size(width, height),
                    Mode = ResizeMode.Max
                }));

            _logger.LogDebug("Saving thumbnail to {ThumbnailPath}", thumbnailPath);
            await image.SaveAsJpegAsync(thumbnailPath);

            // Save thumbnail info to database (store relative path)
            await using var context = await _contextFactory.CreateDbContextAsync();

            var thumbnailRelativePath = _pathService.GetRelativePath(thumbnailPath);

            var thumbnail = new Thumbnail
            {
                PhotoId = photo.Id,
                Size = size,
                FilePath = thumbnailRelativePath,
                CreatedAt = DateTime.UtcNow
            };

            context.Thumbnails.Add(thumbnail);
            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Many browsers request the same thumbnail at once; unique index (PhotoId, Size) races.
                _logger.LogDebug(ex, "Concurrent thumbnail insert for photo {PhotoId} size {Size}", photo.Id, size);
                if (File.Exists(thumbnailPath))
                    return thumbnailPath;

                await using var relCtx = await _contextFactory.CreateDbContextAsync();
                var row = await relCtx.Thumbnails.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.PhotoId == photo.Id && t.Size == size);
                if (row == null)
                    throw;

                var existingPath = _pathService.GetFullPath(row.FilePath);
                if (!File.Exists(existingPath))
                    throw new FileNotFoundException($"Thumbnail row exists but file missing: {existingPath}", ex);
                return existingPath;
            }

            _logger.LogInformation("Successfully created thumbnail for photo {PhotoId} at {ThumbnailPath}",
                photo.Id, thumbnailPath);

            return thumbnailPath;
        }
        catch (UnknownImageFormatException ex)
        {
            _logger.LogWarning(ex, "Unsupported or corrupt image format for photo {PhotoId} at {Path}", photo.Id, photoFullPath);
            throw new FileNotFoundException($"Image could not be decoded: {photoFullPath}", ex);
        }
        catch (InvalidImageContentException ex)
        {
            _logger.LogWarning(ex, "Invalid image content for photo {PhotoId} at {Path}", photo.Id, photoFullPath);
            throw new FileNotFoundException($"Image could not be decoded: {photoFullPath}", ex);
        }
        catch (ImageProcessingException ex)
        {
            _logger.LogWarning(ex, "Image processing failed for photo {PhotoId} at {Path}", photo.Id, photoFullPath);
            throw new FileNotFoundException($"Image could not be processed: {photoFullPath}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating thumbnail for photo {PhotoId} from {FilePath}: {Message}",
                photo.Id, photo.FilePath, ex.Message);
            throw;
        }
    }

    private static (int width, int height) CalculateThumbnailDimensions(int originalWidth, int originalHeight, int maxSize)
    {
        if (originalWidth <= maxSize && originalHeight <= maxSize)
        {
            return (originalWidth, originalHeight);
        }

        var ratio = Math.Min((double)maxSize / originalWidth, (double)maxSize / originalHeight);
        return ((int)(originalWidth * ratio), (int)(originalHeight * ratio));
    }
}
