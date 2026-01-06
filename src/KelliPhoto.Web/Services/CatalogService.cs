using KelliPhoto.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace KelliPhoto.Web.Services;

public class CatalogService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CatalogService> _logger;
    private readonly TimeSpan _scanInterval = TimeSpan.FromHours(24);

    public CatalogService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<CatalogService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Catalog service started");

        // Wait a bit for the application to fully start
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCatalogScanAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during catalog scan");
            }

            await Task.Delay(_scanInterval, stoppingToken);
        }
    }

    private async Task PerformCatalogScanAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var folderService = scope.ServiceProvider.GetRequiredService<IFolderService>();
        var photoService = scope.ServiceProvider.GetRequiredService<IPhotoService>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var galleryPath = _configuration["GallerySettings:GalleryPath"];
        if (string.IsNullOrEmpty(galleryPath))
        {
            _logger.LogWarning("Gallery path not configured");
            return;
        }

        _logger.LogInformation("Starting catalog scan of {GalleryPath}", galleryPath);

        // Scan folders
        var folders = await folderService.ScanFoldersAsync(galleryPath);
        _logger.LogInformation("Scanned {Count} folders", folders.Count);

        // Scan photos in each folder
        var totalPhotos = 0;
        foreach (var folder in folders)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var photos = await photoService.ScanPhotosInFolderAsync(folder.Id, folder.Path);
            totalPhotos += photos.Count;

            if (folder.Id % 10 == 0) // Log progress every 10 folders
            {
                _logger.LogInformation("Scanned {PhotoCount} photos so far", totalPhotos);
            }
        }

        _logger.LogInformation("Catalog scan completed. Total photos: {TotalPhotos}", totalPhotos);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Catalog service stopped");
        await base.StopAsync(cancellationToken);
    }
}
