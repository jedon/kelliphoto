using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace KelliPhoto.Web.Services;

public class CatalogService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CatalogService> _logger;
    private readonly IHomePageCache? _homePageCache;
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private bool _startupScanCompleted = false;

    public CatalogService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<CatalogService> logger,
        IHomePageCache? homePageCache = null)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        _homePageCache = homePageCache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Catalog service started");

        // Wait a bit for the application to fully start
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        // Perform startup scan: folders + root photos only
        try
        {
            await PerformStartupScanAsync(stoppingToken);
            _startupScanCompleted = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during startup scan");
        }

        // Schedule midnight scans
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                var midnight = now.Date.AddDays(1); // Next midnight
                var timeUntilMidnight = midnight - now;

                _logger.LogInformation("Next full catalog scan scheduled for {Midnight}", midnight);
                await Task.Delay(timeUntilMidnight, stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                {
                    await PerformFullCatalogScanAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled catalog scan");
                // Wait 1 hour before retrying if there's an error
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    public async Task TriggerFullScanAsync()
    {
        if (!await _scanLock.WaitAsync(0))
        {
            _logger.LogWarning("Full scan already in progress");
            return;
        }

        try
        {
            _logger.LogInformation("Manual full catalog scan triggered");
            await PerformFullCatalogScanAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual catalog scan");
            throw;
        }
        finally
        {
            _scanLock.Release();
        }
    }

    private async Task PerformStartupScanAsync(CancellationToken cancellationToken)
    {
        var galleryPath = _configuration["GallerySettings:GalleryPath"];
        if (string.IsNullOrEmpty(galleryPath))
        {
            _logger.LogWarning("Gallery path not configured");
            return;
        }

        _logger.LogInformation("Starting startup scan: folders and root photos only");

        // Step 1: Scan folder structure (always needed for navigation)
        List<Folder> folders = new();
        int retryCount = 0;
        const int maxRetries = 3;
        
        while (retryCount < maxRetries)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var folderService = scope.ServiceProvider.GetRequiredService<IFolderService>();
                folders = await folderService.ScanFoldersAsync(galleryPath);
                _logger.LogInformation("Scanned {Count} folders", folders.Count);
                break;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("second operation") || ex.Message.Contains("concurrently"))
            {
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    _logger.LogError(ex, "Failed to scan folders after {RetryCount} retries", maxRetries);
                    return;
                }
                _logger.LogWarning("Concurrency error scanning folders, retrying ({RetryCount}/{MaxRetries})...", retryCount, maxRetries);
                await Task.Delay(TimeSpan.FromSeconds(2 * retryCount), cancellationToken);
            }
        }

        // Step 2: Scan photos in "Home Page Highlights" folder (homepage gallery)
        // Use batched method with batch size of 400 for optimal performance
        using var photoScope = _serviceProvider.CreateScope();
        var photoService = photoScope.ServiceProvider.GetRequiredService<IPhotoService>();
        var folderServiceForPhotos = photoScope.ServiceProvider.GetRequiredService<IFolderService>();
        var progressService = photoScope.ServiceProvider.GetRequiredService<IScanProgressService>();
        
        var homePageFolder = await folderServiceForPhotos.GetFolderByNameAsync("Home Page Highlights");
        var totalPhotos = 0;
        
        if (homePageFolder != null)
        {
            try
            {
                _logger.LogInformation("Scanning Home Page Highlights folder: {FolderPath}", homePageFolder.Path);
                var photos = await photoService.ScanPhotosInFolderBatchedAsync(homePageFolder.Id, homePageFolder.Path, progressService, batchSize: 400);
                totalPhotos = photos.Count;
                _logger.LogInformation("Scanned {PhotoCount} photos in Home Page Highlights folder", photos.Count);
                _homePageCache?.Invalidate();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning photos in Home Page Highlights folder {FolderId}", homePageFolder.Id);
            }
        }
        else
        {
            _logger.LogWarning("Home Page Highlights folder not found, skipping homepage photo scan");
        }

        _logger.LogInformation("Startup scan completed. Scanned {FolderCount} folders and {PhotoCount} photos in Home Page Highlights", folders.Count, totalPhotos);
        _logger.LogInformation("Startup scan finished. No further scans will run until midnight or manual trigger.");
    }

    private async Task PerformFullCatalogScanAsync(CancellationToken cancellationToken)
    {
        if (!await _scanLock.WaitAsync(0))
        {
            _logger.LogWarning("Full scan already in progress");
            return;
        }

        try
        {
            await PerformCatalogScanAsync(cancellationToken);
            _homePageCache?.Invalidate();
        }
        finally
        {
            _scanLock.Release();
        }
    }

    private async Task PerformCatalogScanAsync(CancellationToken cancellationToken)
    {
        var galleryPath = _configuration["GallerySettings:GalleryPath"];
        if (string.IsNullOrEmpty(galleryPath))
        {
            _logger.LogWarning("Gallery path not configured");
            return;
        }

        _logger.LogInformation("Starting catalog scan of {GalleryPath}", galleryPath);

        // Scan folders with retry logic
        List<Folder> folders = new();
        int retryCount = 0;
        const int maxRetries = 3;
        
        while (retryCount < maxRetries)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var folderService = scope.ServiceProvider.GetRequiredService<IFolderService>();
                folders = await folderService.ScanFoldersAsync(galleryPath);
                _logger.LogInformation("Scanned {Count} folders", folders.Count);
                break; // Success, exit retry loop
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("second operation") || ex.Message.Contains("concurrently"))
            {
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    _logger.LogError(ex, "Failed to scan folders after {RetryCount} retries due to concurrency issues", maxRetries);
                    return;
                }
                _logger.LogWarning("Concurrency error scanning folders, retrying ({RetryCount}/{MaxRetries})...", retryCount, maxRetries);
                await Task.Delay(TimeSpan.FromSeconds(2 * retryCount), cancellationToken); // Exponential backoff
            }
        }

        // Scan photos in each folder using batched method with batch size of 400
        // Full directories are processed at once (up to 400 photos), larger folders use batches of 400
        var totalPhotos = 0;
        var processedFolders = 0;
        const int batchSize = 400;
        
        using var photoScope = _serviceProvider.CreateScope();
        var photoService = photoScope.ServiceProvider.GetRequiredService<IPhotoService>();
        
        foreach (var folder in folders)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Add a small delay between folder processing to reduce database load
            // This helps prevent conflicts with user requests
            if (processedFolders > 0 && processedFolders % 5 == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }

            try
            {
                var photos = await photoService.ScanPhotosInFolderBatchedAsync(folder.Id, folder.Path, batchSize: batchSize);
                totalPhotos += photos.Count;
                processedFolders++;

                if (processedFolders % 10 == 0) // Log progress every 10 folders
                {
                    _logger.LogInformation("Scanned {PhotoCount} photos so far from {FolderCount} folders", totalPhotos, processedFolders);
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("second operation") || ex.Message.Contains("concurrently"))
            {
                // Skip this folder if there's a concurrency error and continue with the next
                _logger.LogWarning(ex, "Concurrency error scanning photos in folder {FolderId}, skipping", folder.Id);
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken); // Brief delay before continuing
            }
            catch (Exception ex)
            {
                // Log other errors but continue processing
                _logger.LogError(ex, "Error scanning photos in folder {FolderId}", folder.Id);
            }
        }

        _logger.LogInformation("Catalog scan completed. Total photos: {TotalPhotos} from {FolderCount} folders", totalPhotos, processedFolders);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Catalog service stopped");
        await base.StopAsync(cancellationToken);
    }
}
