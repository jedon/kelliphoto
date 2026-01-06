using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace KelliPhoto.Web.Services;

public class ThumbnailService : IThumbnailService
{
    private readonly ApplicationDbContext _context;
    private readonly IPhotoService _photoService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ThumbnailService> _logger;

    public ThumbnailService(
        ApplicationDbContext context,
        IPhotoService photoService,
        IConfiguration configuration,
        ILogger<ThumbnailService> logger)
    {
        _context = context;
        _photoService = photoService;
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

        // Check if thumbnail already exists
        var existingThumbnail = await _context.Thumbnails
            .FirstOrDefaultAsync(t => t.PhotoId == photoId && t.Size == size);

        if (existingThumbnail != null && File.Exists(existingThumbnail.FilePath))
        {
            return existingThumbnail.FilePath;
        }

        // Create thumbnail
        var thumbnailPath = await CreateThumbnailAsync(photo, size);
        return thumbnailPath;
    }

    public async Task<Stream> GetThumbnailStreamAsync(int photoId, int size = 300)
    {
        var thumbnailPath = await GetOrCreateThumbnailAsync(photoId, size);
        
        if (!File.Exists(thumbnailPath))
        {
            throw new FileNotFoundException($"Thumbnail not found: {thumbnailPath}");
        }

        return File.OpenRead(thumbnailPath);
    }

    public async Task DeleteThumbnailAsync(int photoId, int size)
    {
        var thumbnail = await _context.Thumbnails
            .FirstOrDefaultAsync(t => t.PhotoId == photoId && t.Size == size);

        if (thumbnail != null)
        {
            if (File.Exists(thumbnail.FilePath))
            {
                try
                {
                    File.Delete(thumbnail.FilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error deleting thumbnail file: {FilePath}", thumbnail.FilePath);
                }
            }

            _context.Thumbnails.Remove(thumbnail);
            await _context.SaveChangesAsync();
        }
    }

    private async Task<string> CreateThumbnailAsync(Photo photo, int size)
    {
        if (!File.Exists(photo.FilePath))
        {
            throw new FileNotFoundException($"Photo file not found: {photo.FilePath}");
        }

        var thumbnailBasePath = _configuration["GallerySettings:ThumbnailPath"] 
            ?? Path.Combine(Path.GetDirectoryName(photo.FilePath) ?? "", ".thumbnails");
        
        Directory.CreateDirectory(thumbnailBasePath);

        var thumbnailFileName = $"{photo.Id}_{size}.jpg";
        var thumbnailPath = Path.Combine(thumbnailBasePath, thumbnailFileName);

        try
        {
            using var image = await Image.LoadAsync(photo.FilePath);
            
            var (width, height) = CalculateThumbnailDimensions(image.Width, image.Height, size);
            
            image.Mutate(x => x
                .Resize(new ResizeOptions
                {
                    Size = new Size(width, height),
                    Mode = ResizeMode.Max
                }));

            await image.SaveAsJpegAsync(thumbnailPath);

            // Save thumbnail info to database
            var thumbnail = new Thumbnail
            {
                PhotoId = photo.Id,
                Size = size,
                FilePath = thumbnailPath,
                CreatedAt = DateTime.UtcNow
            };

            _context.Thumbnails.Add(thumbnail);
            await _context.SaveChangesAsync();

            return thumbnailPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating thumbnail for photo {PhotoId}", photo.Id);
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
