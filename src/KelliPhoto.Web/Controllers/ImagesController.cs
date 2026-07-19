using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KelliPhoto.Web.Services;
using Microsoft.Extensions.Configuration;

namespace KelliPhoto.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImagesController : ControllerBase
{
    private const int ThumbnailSizeMin = 50;
    private const int ThumbnailSizeMax = 800;
    private const int ThumbnailSizeDefault = 300;
    private const int WebMaxMin = 400;
    private const int WebMaxMax = 2400;
    private const int WebMaxDefault = 2000;

    private readonly IPhotoService _photoService;
    private readonly IThumbnailService _thumbnailService;
    private readonly IWebImageService _webImageService;
    private readonly IPathService _pathService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ImagesController> _logger;

    public ImagesController(
        IPhotoService photoService,
        IThumbnailService thumbnailService,
        IWebImageService webImageService,
        IPathService pathService,
        IConfiguration configuration,
        ILogger<ImagesController> logger)
    {
        _photoService = photoService;
        _thumbnailService = thumbnailService;
        _webImageService = webImageService;
        _pathService = pathService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("thumbnail/{photoId}")]
    public async Task<IActionResult> GetThumbnail(int photoId, [FromQuery] int size = 300)
    {
        size = ClampThumbnailSize(size);

        if (!IsAdminUser() && !await _photoService.CanPublicViewPhotoAsync(photoId))
        {
            return NotFound();
        }

        try
        {
            var stream = await _thumbnailService.GetThumbnailStreamAsync(photoId, size);
            return File(stream, "image/jpeg");
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Thumbnail not found for photo {PhotoId}: {Message}", photoId, ex.Message);
            
            // Try to get the photo to see if it exists
            var photo = await _photoService.GetPhotoByIdAsync(photoId);
            if (photo == null)
            {
                _logger.LogWarning("Photo {PhotoId} does not exist in database", photoId);
                return NotFound();
            }
            
            var resolved = _pathService.ResolveExistingPhotoFilePath(photo.FilePath);
            var primaryPath = _pathService.GetFullPath(photo.FilePath);
            _logger.LogWarning("Photo {PhotoId} exists but thumbnail generation failed. PrimaryPath: {PrimaryPath}, ResolvedPath: {ResolvedPath}, FileExists: {FileExists}", 
                photoId, primaryPath, resolved, resolved != null);
            
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting thumbnail for photo {PhotoId}: {Message}", photoId, ex.Message);
            return StatusCode(500);
        }
    }

    [HttpGet("photo/{photoId}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetPhoto(int photoId)
    {
        try
        {
            var photo = await _photoService.GetPhotoByIdAsync(photoId);
            if (photo == null)
            {
                return NotFound();
            }

            // Resolve relative path to full path
            var photoFullPath = _pathService.ResolveExistingPhotoFilePath(photo.FilePath);
            if (photoFullPath == null)
            {
                _logger.LogWarning("Photo file not found: {FilePath}", _pathService.GetFullPath(photo.FilePath));
                return NotFound();
            }

            var stream = System.IO.File.OpenRead(photoFullPath);
            var contentType = GetContentType(photoFullPath);
            return File(stream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting photo {PhotoId}", photoId);
            return StatusCode(500);
        }
    }

    // Public endpoint: serves a resized (and optionally watermarked) derivative from a separate cache folder.
    [HttpGet("web/{photoId}")]
    public async Task<IActionResult> GetWebPhoto(
        int photoId,
        [FromQuery] int max = 2000,
        [FromQuery] bool watermark = true,
        CancellationToken cancellationToken = default)
    {
        max = ClampWebMax(max);

        if (!IsAdminUser() && !await _photoService.CanPublicViewPhotoAsync(photoId))
        {
            return NotFound();
        }

        if (!IsAdminUser())
        {
            watermark = true;
        }

        try
        {
            var stream = await _webImageService.GetWebImageStreamAsync(photoId, max, watermark, cancellationToken);
            return File(stream, "image/jpeg");
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Web image not found for photo {PhotoId}: {Message}", photoId, ex.Message);
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting web photo {PhotoId}", photoId);
            return StatusCode(500);
        }
    }

    // Serve web assets from the .web directory (watermarks, backgrounds, logos, etc.)
    // HEAD is explicit: [HttpGet] alone does not always register HEAD, so curl -I and probes were 404.
    [HttpGet("webasset/{filename}")]
    [HttpHead("webasset/{filename}")]
    public IActionResult GetWebAsset(string filename)
    {
        try
        {
            // Sanitize filename to prevent directory traversal
            var safeFilename = Path.GetFileName(filename);
            if (string.IsNullOrEmpty(safeFilename) || safeFilename != filename)
            {
                _logger.LogWarning("Invalid filename requested: {Filename}", filename);
                return BadRequest();
            }

            // Resolve web assets path
            string webAssetsPath;
            var configuredPath = _configuration["GallerySettings:WebAssetsPath"];
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                // If already absolute, normalize it; otherwise resolve relative to gallery path
                if (Path.IsPathRooted(configuredPath) || configuredPath.StartsWith(@"\\", StringComparison.Ordinal))
                {
                    webAssetsPath = _pathService.NormalizePath(configuredPath);
                }
                else
                {
                    webAssetsPath = _pathService.GetFullPath(configuredPath);
                }
            }
            else
            {
                // Fallback to .web subdirectory of gallery path
                var galleryPath = _configuration["GallerySettings:GalleryPath"] ?? "";
                webAssetsPath = _pathService.GetFullPath(Path.Combine(galleryPath, ".web"));
            }
            
            _logger.LogDebug("Resolved web assets path: {WebAssetsPath} for filename: {Filename}", webAssetsPath, filename);
            
            var filePath = Path.Combine(webAssetsPath, safeFilename);
            
            // Normalize paths for security check
            var normalizedFilePath = Path.GetFullPath(filePath);
            var normalizedWebAssetsPath = Path.GetFullPath(webAssetsPath);
            
            // Ensure the file is within the web assets directory (security check)
            if (!normalizedFilePath.StartsWith(normalizedWebAssetsPath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Attempted directory traversal: {FilePath} (base: {BasePath})", normalizedFilePath, normalizedWebAssetsPath);
                return BadRequest();
            }

            if (!System.IO.File.Exists(normalizedFilePath))
            {
                var galleryPathSetting = _configuration["GallerySettings:GalleryPath"];
                if (safeFilename.Equals("logo.png", StringComparison.OrdinalIgnoreCase))
                {
                    var resolvedLogo = ResolveLogoGraphicPath(normalizedWebAssetsPath, galleryPathSetting);
                    if (resolvedLogo != null)
                        normalizedFilePath = resolvedLogo;
                }
                else if (safeFilename.Equals("background.png", StringComparison.OrdinalIgnoreCase))
                {
                    var resolvedBg = ResolveBackgroundPath(normalizedWebAssetsPath);
                    if (resolvedBg != null)
                        normalizedFilePath = resolvedBg;
                }

                if (!System.IO.File.Exists(normalizedFilePath))
                {
                    _logger.LogWarning("Web asset not found: {FilePath} (resolved from: {WebAssetsPath}/{Filename})",
                        normalizedFilePath, webAssetsPath, safeFilename);
                    return NotFound();
                }
            }

            var contentType = GetContentType(normalizedFilePath);
            var stream = System.IO.File.OpenRead(normalizedFilePath);
            return File(stream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving web asset {Filename}: {Message}", filename, ex.Message);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Site requests logo.png; on disk it may be .web/logo.png, .web/kelliphotologo.png, or kelliphotologo.png
    /// at GalleryPath (mount root) or its parent (when GalleryPath is .../source).
    /// </summary>
    private string? ResolveLogoGraphicPath(string webAssetsPathFull, string? galleryPathSetting)
    {
        var webRoot = Path.GetFullPath(webAssetsPathFull);
        foreach (var name in new[] { "logo.png", "kelliphotologo.png" })
        {
            var p = Path.GetFullPath(Path.Combine(webRoot, name));
            if (p.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(p))
                return p;
        }

        if (string.IsNullOrWhiteSpace(galleryPathSetting))
            return null;

        var g = Path.GetFullPath(_pathService.NormalizePath(galleryPathSetting));
        var onMount = Path.GetFullPath(Path.Combine(g, "kelliphotologo.png"));
        if (onMount.StartsWith(g, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(onMount))
            return onMount;

        var parentDir = Directory.GetParent(g)?.FullName;
        if (string.IsNullOrEmpty(parentDir))
            return null;

        var parentFull = Path.GetFullPath(parentDir);
        var parentLogo = Path.GetFullPath(Path.Combine(parentFull, "kelliphotologo.png"));
        if (parentLogo.StartsWith(parentFull, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(parentLogo))
            return parentLogo;

        return null;
    }

    /// <summary>
    /// CSS requests background.png under .web; also accept .jpg variants and sibling gallery folder web/.
    /// </summary>
    private static string? ResolveBackgroundPath(string webAssetsPathFull)
    {
        var webRoot = Path.GetFullPath(webAssetsPathFull);
        foreach (var name in new[] { "background.png", "background.jpg", "Background.png", "Background.jpg" })
        {
            var p = Path.GetFullPath(Path.Combine(webRoot, name));
            if (p.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(p))
                return p;
        }

        var galleryRoot = Path.GetFullPath(Path.Combine(webRoot, ".."));
        foreach (var name in new[] { "background.png", "background.jpg" })
        {
            var p = Path.GetFullPath(Path.Combine(galleryRoot, "web", name));
            if (p.StartsWith(galleryRoot, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(p))
                return p;
        }

        return null;
    }

    private bool IsAdminUser() =>
        User.Identity?.IsAuthenticated == true && User.IsInRole(RoleNames.Admin);

    private static int ClampThumbnailSize(int size) =>
        Math.Clamp(size <= 0 ? ThumbnailSizeDefault : size, ThumbnailSizeMin, ThumbnailSizeMax);

    private static int ClampWebMax(int max) =>
        Math.Clamp(max <= 0 ? WebMaxDefault : max, WebMaxMin, WebMaxMax);

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
