using System.Collections.Concurrent;
using KelliPhoto.Web.Data.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace KelliPhoto.Web.Services;

public sealed class WebImageService : IWebImageService
{
    private sealed record WatermarkSettings(
        bool Enabled,
        string? ImagePath,
        float Opacity,
        float RelativeWidth,
        int MarginPx);

    private readonly IPhotoService _photoService;
    private readonly IPathService _pathService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebImageService> _logger;

    // Prevent duplicate work under load for same cache key.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public WebImageService(
        IPhotoService photoService,
        IPathService pathService,
        IConfiguration configuration,
        ILogger<WebImageService> logger)
    {
        _photoService = photoService;
        _pathService = pathService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Stream> GetWebImageStreamAsync(
        int photoId,
        int maxDimension = 2000,
        bool watermark = true,
        CancellationToken cancellationToken = default)
    {
        var fullPath = await GetOrCreateWebImagePathAsync(photoId, maxDimension, watermark, cancellationToken);
        return File.OpenRead(fullPath);
    }

    private async Task<string> GetOrCreateWebImagePathAsync(
        int photoId,
        int maxDimension,
        bool watermark,
        CancellationToken cancellationToken)
    {
        if (maxDimension <= 0)
        {
            maxDimension = 2000;
        }

        var webBasePath = GetWebImageBasePath();
        Directory.CreateDirectory(webBasePath);

        var settings = GetWatermarkSettings(watermark);
        var quality = GetJpegQuality();
        var cacheKey = BuildCacheKey(photoId, maxDimension, quality, settings);
        var targetPath = Path.Combine(webBasePath, $"{cacheKey}.jpg");

        if (File.Exists(targetPath))
        {
            return targetPath;
        }

        var gate = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(targetPath))
            {
                return targetPath;
            }

            var photo = await _photoService.GetPhotoByIdAsync(photoId);
            if (photo == null)
            {
                throw new FileNotFoundException($"Photo with id {photoId} not found");
            }

            var photoFullPath = _pathService.ResolveExistingPhotoFilePath(photo.FilePath);
            if (photoFullPath == null)
            {
                throw new FileNotFoundException($"Photo file not found: {_pathService.GetFullPath(photo.FilePath)}");
            }

            var tempPath = targetPath + $".tmp.{Guid.NewGuid():N}";
            try
            {
                await CreateWebImageAsync(photo, photoFullPath, tempPath, maxDimension, quality, settings, cancellationToken);

                // Best-effort atomic publish under concurrency.
                if (!File.Exists(targetPath))
                {
                    File.Move(tempPath, targetPath);
                }
                else
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                    // ignore cleanup failures
                }
                throw;
            }

            return targetPath;
        }
        finally
        {
            gate.Release();

            // Opportunistic cleanup to avoid unbounded growth of lock objects.
            if (gate.CurrentCount == 1)
            {
                _locks.TryRemove(cacheKey, out _);
            }
        }
    }

    private string GetWebImageBasePath()
    {
        var configured = _configuration["GallerySettings:WebImagePath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return _pathService.NormalizePath(configured);
        }

        // Fallback for dev/local only. In production, set GallerySettings:WebImagePath to a persistent volume.
        var fallback = Path.Combine(Path.GetTempPath(), "kelliphoto-webimages");
        _logger.LogWarning("GallerySettings:WebImagePath not set; using temp cache path: {Path}", fallback);
        return _pathService.NormalizePath(fallback);
    }

    private int GetJpegQuality()
    {
        var raw = _configuration["GallerySettings:WebJpegQuality"];
        return int.TryParse(raw, out var q) ? Math.Clamp(q, 40, 95) : 85;
    }

    private WatermarkSettings GetWatermarkSettings(bool watermarkRequested)
    {
        var enabled = watermarkRequested && _configuration.GetValue("WatermarkSettings:Enabled", false);
        var imagePath = _configuration["WatermarkSettings:ImagePath"];
        bool pathExists = false;
        
        // Check if the configured path exists
        if (!string.IsNullOrWhiteSpace(imagePath))
        {
            pathExists = File.Exists(imagePath);
            if (pathExists)
            {
                _logger.LogDebug("Watermark path found: {Path}", imagePath);
            }
            else
            {
                _logger.LogWarning("Watermark path from config does not exist: {Path}", imagePath);
            }
        }
        
        // If ImagePath is not set or doesn't exist, try to resolve it from WebAssetsPath
        if (string.IsNullOrWhiteSpace(imagePath) || !pathExists)
        {
            var webAssetsPath = _configuration["GallerySettings:WebAssetsPath"];
            if (!string.IsNullOrWhiteSpace(webAssetsPath))
            {
                var resolvedPath = Path.Combine(webAssetsPath, "watermark.png");
                if (File.Exists(resolvedPath))
                {
                    imagePath = resolvedPath;
                    pathExists = true;
                    _logger.LogInformation("Resolved watermark path from WebAssetsPath: {Path}", resolvedPath);
                }
                else
                {
                    // Use resolved path even if it doesn't exist (will log warning later when trying to use it)
                    if (string.IsNullOrWhiteSpace(_configuration["WatermarkSettings:ImagePath"]))
                    {
                        imagePath = resolvedPath;
                        _logger.LogWarning("Using default watermark path (file may not exist): {Path}", resolvedPath);
                    }
                    else
                    {
                        _logger.LogWarning("Watermark file not found at configured path or WebAssetsPath. Configured: {ConfigPath}, WebAssetsPath: {WebAssetsPath}", 
                            _configuration["WatermarkSettings:ImagePath"], resolvedPath);
                    }
                }
            }
            else
            {
                _logger.LogWarning("WebAssetsPath not configured, cannot resolve watermark path");
            }
        }

        var opacity = _configuration.GetValue("WatermarkSettings:Opacity", 0.28f);
        if (opacity < 0f || opacity > 1f) opacity = 0.28f;

        var relativeWidth = _configuration.GetValue("WatermarkSettings:RelativeWidth", 0.22f);
        if (relativeWidth <= 0f || relativeWidth > 1f) relativeWidth = 0.22f;

        var margin = _configuration.GetValue("WatermarkSettings:MarginPx", 24);
        if (margin < 0) margin = 24;

        _logger.LogDebug("Watermark settings: Enabled={Enabled}, Path={Path}, Exists={Exists}, Opacity={Opacity}, RelativeWidth={RelativeWidth}, Margin={Margin}", 
            enabled, imagePath, pathExists, opacity, relativeWidth, margin);

        return new WatermarkSettings(enabled, imagePath, opacity, relativeWidth, margin);
    }

    private static string BuildCacheKey(int photoId, int maxDimension, int quality, WatermarkSettings settings)
    {
        // Keep filenames stable and filesystem-safe.
        var wmPart = "nowm";
        if (settings.Enabled)
        {
            var stamp = "nostamp";
            if (!string.IsNullOrWhiteSpace(settings.ImagePath) && File.Exists(settings.ImagePath))
            {
                stamp = File.GetLastWriteTimeUtc(settings.ImagePath).Ticks.ToString();
            }

            // Use integers to keep it short/stable.
            var op = (int)Math.Round(settings.Opacity * 100);
            var rw = (int)Math.Round(settings.RelativeWidth * 100);
            wmPart = $"wm_op{op}_rw{rw}_m{settings.MarginPx}_s{stamp}";
        }

        return $"p{photoId}_max{maxDimension}_q{quality}_{wmPart}";
    }

    private async Task CreateWebImageAsync(
        Photo photo,
        string photoFullPath,
        string outputPath,
        int maxDimension,
        int quality,
        WatermarkSettings watermark,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Generating web image: PhotoId={PhotoId}, Max={Max}, Watermark={WatermarkEnabled}, Output={Output}",
            photo.Id, maxDimension, watermark.Enabled, outputPath);

        using var image = await Image.LoadAsync(photoFullPath, cancellationToken);

        image.Mutate(x => x.AutoOrient());

        // Resize to max dimension (maintain aspect ratio).
        var (w, h) = CalculateMaxDimensions(image.Width, image.Height, maxDimension);
        if (w != image.Width || h != image.Height)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(w, h),
                Mode = ResizeMode.Max
            }));
        }

        if (watermark.Enabled && !string.IsNullOrWhiteSpace(watermark.ImagePath) && File.Exists(watermark.ImagePath))
        {
            await ApplyImageWatermarkAsync(image, watermark, cancellationToken);
        }
        else if (watermark.Enabled)
        {
            _logger.LogWarning("Watermark enabled but WatermarkSettings:ImagePath missing or not found. Path={Path}", watermark.ImagePath);
        }

        // Strip metadata from the publicly served derivative.
        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        var encoder = new JpegEncoder
        {
            Quality = quality
        };

        await image.SaveAsJpegAsync(outputPath, encoder, cancellationToken);
    }

    private static (int width, int height) CalculateMaxDimensions(int originalWidth, int originalHeight, int maxSize)
    {
        if (originalWidth <= maxSize && originalHeight <= maxSize)
        {
            return (originalWidth, originalHeight);
        }

        var ratio = Math.Min((double)maxSize / originalWidth, (double)maxSize / originalHeight);
        return ((int)Math.Round(originalWidth * ratio), (int)Math.Round(originalHeight * ratio));
    }

    private static async Task ApplyImageWatermarkAsync(Image image, WatermarkSettings watermark, CancellationToken cancellationToken)
    {
        using var watermarkImage = await Image.LoadAsync(watermark.ImagePath!, cancellationToken);

        // Scale watermark relative to output image width.
        var desiredWidth = (int)Math.Round(image.Width * watermark.RelativeWidth);
        desiredWidth = Math.Clamp(desiredWidth, 120, Math.Max(120, image.Width)); // lower bound keeps it visible

        if (watermarkImage.Width != desiredWidth)
        {
            var ratio = (double)desiredWidth / watermarkImage.Width;
            var desiredHeight = Math.Max(1, (int)Math.Round(watermarkImage.Height * ratio));
            watermarkImage.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(desiredWidth, desiredHeight),
                Mode = ResizeMode.Max
            }));
        }

        var xPos = Math.Max(0, image.Width - watermarkImage.Width - watermark.MarginPx);
        var yPos = Math.Max(0, image.Height - watermarkImage.Height - watermark.MarginPx);

        image.Mutate(x => x.DrawImage(watermarkImage, new Point(xPos, yPos), watermark.Opacity));
    }
}

