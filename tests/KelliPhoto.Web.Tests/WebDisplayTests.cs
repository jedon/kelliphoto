using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using KelliPhoto.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using Xunit;

namespace KelliPhoto.Web.Tests;

public class WebDisplayTests : IDisposable
{
    private readonly string _testGalleryPath;
    private readonly string _testWebImagePath;
    private readonly ApplicationDbContext _context;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IPathService _pathService;
    private readonly IPhotoService _photoService;
    private readonly IFolderService _folderService;
    private readonly IWebImageService _webImageService;
    private readonly Mock<IScanProgressService> _mockProgressService;

    public WebDisplayTests()
    {
        // Create temporary test directories
        var baseTestDir = Path.Combine(Path.GetTempPath(), "kelliphoto-web-tests", Guid.NewGuid().ToString());
        _testGalleryPath = Path.Combine(baseTestDir, "gallery");
        _testWebImagePath = Path.Combine(baseTestDir, "webimages");
        
        Directory.CreateDirectory(_testGalleryPath);
        Directory.CreateDirectory(_testWebImagePath);

        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _contextFactory = new TestDbContextFactory(options);

        // Setup configuration
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GallerySettings:GalleryPath", _testGalleryPath },
                { "GallerySettings:WebImagePath", _testWebImagePath },
                { "GallerySettings:WebAssetsPath", Path.Combine(_testGalleryPath, ".web") },
                { "GallerySettings:WebJpegQuality", "85" },
                { "WatermarkSettings:Enabled", "false" }
            })
            .Build();
        _pathService = new PathService(config);

        // Setup loggers
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var photoLogger = loggerFactory.CreateLogger<PhotoService>();
        var folderLogger = loggerFactory.CreateLogger<FolderService>();
        var webImageLogger = loggerFactory.CreateLogger<WebImageService>();

        _photoService = new PhotoService(_contextFactory, _pathService, photoLogger);
        _folderService = new FolderService(_contextFactory, _pathService, folderLogger);
        _webImageService = new WebImageService(_photoService, _pathService, config, webImageLogger);

        // Setup mock progress service
        _mockProgressService = new Mock<IScanProgressService>();
        _mockProgressService.Setup(s => s.StartScan(It.IsAny<int>(), It.IsAny<int>()));
        _mockProgressService.Setup(s => s.UpdateProgress(It.IsAny<int>(), It.IsAny<int>()));
        _mockProgressService.Setup(s => s.CompleteScan(It.IsAny<int>()));
    }

    [Fact]
    public async Task WebImageService_GeneratesImage_WhenRequested()
    {
        // Arrange
        var testFolderName = "WebTestFolder";
        var testFolderPath = Path.Combine(_testGalleryPath, testFolderName);
        Directory.CreateDirectory(testFolderPath);

        var imagePath = Path.Combine(testFolderPath, "original.jpg");
        CreateTestImageFile(imagePath, 4000, 3000); // Large image

        var folder = await _folderService.CreateOrUpdateFolderAsync(testFolderPath, testFolderName);
        var photos = await _photoService.ScanPhotosInFolderBatchedAsync(
            folder.Id, testFolderPath, _mockProgressService.Object);

        var photo = photos.First();

        // Act
        using var stream = await _webImageService.GetWebImageStreamAsync(photo.Id, maxDimension: 2000, watermark: false);

        // Assert
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);

        // Verify cached file was created
        var cacheFiles = Directory.GetFiles(_testWebImagePath, "*.jpg");
        Assert.True(cacheFiles.Length > 0);
    }

    [Fact]
    public async Task WebImageService_ResizesToMaxDimension()
    {
        // Arrange
        var testFolderName = "ResizeTestFolder";
        var testFolderPath = Path.Combine(_testGalleryPath, testFolderName);
        Directory.CreateDirectory(testFolderPath);

        var imagePath = Path.Combine(testFolderPath, "large.jpg");
        CreateTestImageFile(imagePath, 6000, 4000); // Very large image

        var folder = await _folderService.CreateOrUpdateFolderAsync(testFolderPath, testFolderName);
        var photos = await _photoService.ScanPhotosInFolderBatchedAsync(
            folder.Id, testFolderPath, _mockProgressService.Object);

        var photo = photos.First();

        // Act - Request max 1000px
        using var stream = await _webImageService.GetWebImageStreamAsync(photo.Id, maxDimension: 1000, watermark: false);

        // Assert - Load the generated image and check dimensions
        stream.Position = 0;
        using var image = await SixLabors.ImageSharp.Image.LoadAsync(stream);
        
        // Should be resized (maintaining aspect ratio)
        Assert.True(image.Width <= 1000);
        Assert.True(image.Height <= 1000);
        Assert.True(image.Width > 0 && image.Height > 0);
    }

    [Fact]
    public async Task WebImageService_ReturnsSameImage_OnSubsequentRequests()
    {
        // Arrange
        var testFolderName = "CacheTestFolder";
        var testFolderPath = Path.Combine(_testGalleryPath, testFolderName);
        Directory.CreateDirectory(testFolderPath);

        var imagePath = Path.Combine(testFolderPath, "cache.jpg");
        CreateTestImageFile(imagePath, 2000, 1500);

        var folder = await _folderService.CreateOrUpdateFolderAsync(testFolderPath, testFolderName);
        var photos = await _photoService.ScanPhotosInFolderBatchedAsync(
            folder.Id, testFolderPath, _mockProgressService.Object);

        var photo = photos.First();

        // Act - Request twice
        using var stream1 = await _webImageService.GetWebImageStreamAsync(photo.Id, maxDimension: 2000, watermark: false);
        var cacheFileBeforeSecond = Directory.GetFiles(_testWebImagePath, "*.jpg").FirstOrDefault();
        var fileInfo1 = cacheFileBeforeSecond != null ? new FileInfo(cacheFileBeforeSecond) : null;

        using var stream2 = await _webImageService.GetWebImageStreamAsync(photo.Id, maxDimension: 2000, watermark: false);
        var cacheFileAfterSecond = Directory.GetFiles(_testWebImagePath, "*.jpg").FirstOrDefault();
        var fileInfo2 = cacheFileAfterSecond != null ? new FileInfo(cacheFileAfterSecond) : null;

        // Assert - Same file should be used (cached)
        Assert.NotNull(fileInfo1);
        Assert.NotNull(fileInfo2);
        Assert.Equal(fileInfo1.FullName, fileInfo2.FullName);
        
        // File should not have been regenerated (same timestamp)
        Assert.Equal(fileInfo1.LastWriteTime, fileInfo2.LastWriteTime);
    }

    private void CreateTestImageFile(string path, int width, int height)
    {
        using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
        image.Save(path, new JpegEncoder());
    }

    public void Dispose()
    {
        _context?.Dispose();
        try
        {
            if (Directory.Exists(_testGalleryPath))
            {
                Directory.Delete(_testGalleryPath, true);
            }
            if (Directory.Exists(_testWebImagePath))
            {
                Directory.Delete(_testWebImagePath, true);
            }
        }
        catch { }
    }
}
