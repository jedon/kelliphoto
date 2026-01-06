using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace KelliPhoto.Web.Services;

public class PhotoService : IPhotoService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PhotoService> _logger;
    private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif" };

    public PhotoService(ApplicationDbContext context, ILogger<PhotoService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Photo>> GetPhotosByFolderIdAsync(int folderId, int skip = 0, int take = 50)
    {
        return await _context.Photos
            .Where(p => p.FolderId == folderId)
            .OrderByDescending(p => p.TakenAt ?? p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<Photo?> GetPhotoByIdAsync(int id)
    {
        return await _context.Photos
            .Include(p => p.Folder)
            .Include(p => p.Thumbnails)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<int> GetPhotoCountByFolderIdAsync(int folderId)
    {
        return await _context.Photos
            .CountAsync(p => p.FolderId == folderId);
    }

    public async Task<Photo> CreateOrUpdatePhotoAsync(string filePath, int folderId, string filename)
    {
        var photo = await _context.Photos
            .FirstOrDefaultAsync(p => p.FilePath == filePath);

        if (photo == null)
        {
            var fileInfo = new FileInfo(filePath);
            var fileSize = fileInfo.Exists ? fileInfo.Length : 0;
            DateTime? takenAt = null;
            int? width = null;
            int? height = null;

            try
            {
                if (fileInfo.Exists)
                {
                    using var image = await Image.LoadAsync(filePath);
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
                _logger.LogWarning(ex, "Error reading image metadata for {FilePath}", filePath);
            }

            photo = new Photo
            {
                FilePath = filePath,
                Filename = filename,
                FolderId = folderId,
                FileSize = fileSize,
                Width = width,
                Height = height,
                TakenAt = takenAt,
                CreatedAt = DateTime.UtcNow
            };
            _context.Photos.Add(photo);
        }
        else
        {
            photo.Filename = filename;
        }

        await _context.SaveChangesAsync();
        return photo;
    }

    public async Task<List<Photo>> ScanPhotosInFolderAsync(int folderId, string folderPath)
    {
        var photos = new List<Photo>();

        if (!Directory.Exists(folderPath))
        {
            return photos;
        }

        try
        {
            var files = Directory.GetFiles(folderPath)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            foreach (var filePath in files)
            {
                try
                {
                    var filename = Path.GetFileName(filePath);
                    var photo = await CreateOrUpdatePhotoAsync(filePath, folderId, filename);
                    photos.Add(photo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing photo: {FilePath}", filePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning photos in folder: {FolderPath}", folderPath);
        }

        return photos;
    }

    public static bool IsSupportedImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }
}
