using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KelliPhoto.Web.Services;

namespace KelliPhoto.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImagesController : ControllerBase
{
    private readonly IPhotoService _photoService;
    private readonly IThumbnailService _thumbnailService;
    private readonly ILogger<ImagesController> _logger;

    public ImagesController(
        IPhotoService photoService,
        IThumbnailService thumbnailService,
        ILogger<ImagesController> logger)
    {
        _photoService = photoService;
        _thumbnailService = thumbnailService;
        _logger = logger;
    }

    [HttpGet("thumbnail/{photoId}")]
    public async Task<IActionResult> GetThumbnail(int photoId, [FromQuery] int size = 300)
    {
        try
        {
            var stream = await _thumbnailService.GetThumbnailStreamAsync(photoId, size);
            return File(stream, "image/jpeg");
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Thumbnail not found for photo {PhotoId}", photoId);
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting thumbnail for photo {PhotoId}", photoId);
            return StatusCode(500);
        }
    }

    [HttpGet("photo/{photoId}")]
    public async Task<IActionResult> GetPhoto(int photoId)
    {
        try
        {
            var photo = await _photoService.GetPhotoByIdAsync(photoId);
            if (photo == null)
            {
                return NotFound();
            }

            if (!System.IO.File.Exists(photo.FilePath))
            {
                _logger.LogWarning("Photo file not found: {FilePath}", photo.FilePath);
                return NotFound();
            }

            var stream = System.IO.File.OpenRead(photo.FilePath);
            var contentType = GetContentType(photo.FilePath);
            return File(stream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting photo {PhotoId}", photoId);
            return StatusCode(500);
        }
    }

    private static string GetContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".tiff" or ".tif" => "image/tiff",
            _ => "application/octet-stream"
        };
    }
}
