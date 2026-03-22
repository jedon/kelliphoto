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

public class FolderImageCountTests : IDisposable
{
    private readonly string _testGalleryPath;
    private readonly ApplicationDbContext _context;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IPathService _pathService;
    private readonly IPhotoService _photoService;
    private readonly IFolderService _folderService;
    private readonly Mock<IScanProgressService> _mockProgressService;

    public FolderImageCountTests()
    {
        // Create temporary test directory
        _testGalleryPath = Path.Combine(Path.GetTempPath(), "kelliphoto-count-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testGalleryPath);

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
                { "GallerySettings:GalleryPath", _testGalleryPath }
            })
            .Build();
        _pathService = new PathService(config);

        // Setup logger
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var photoLogger = loggerFactory.CreateLogger<PhotoService>();
        var folderLogger = loggerFactory.CreateLogger<FolderService>();

        _photoService = new PhotoService(_contextFactory, _pathService, photoLogger);
        _folderService = new FolderService(_contextFactory, _pathService, folderLogger);

        // Setup mock progress service
        _mockProgressService = new Mock<IScanProgressService>();
        _mockProgressService.Setup(s => s.StartScan(It.IsAny<int>(), It.IsAny<int>()));
        _mockProgressService.Setup(s => s.UpdateProgress(It.IsAny<int>(), It.IsAny<int>()));
        _mockProgressService.Setup(s => s.CompleteScan(It.IsAny<int>()));
    }

    [Fact]
    public async Task FolderImageCount_MatchesFilesystem_AfterScan()
    {
        // Arrange
        var testFolderName = "TestFolder1";
        var testFolderPath = Path.Combine(_testGalleryPath, testFolderName);
        Directory.CreateDirectory(testFolderPath);

        // Create 5 test images in filesystem
        var imageCount = 5;
        for (int i = 0; i < imageCount; i++)
        {
            CreateTestImageFile(Path.Combine(testFolderPath, $"test{i}.jpg"), 800, 600);
        }

        // Create folder in database
        var folder = await _folderService.CreateOrUpdateFolderAsync(testFolderPath, testFolderName);
        
        // Act - Scan folder
        var photos = await _photoService.ScanPhotosInFolderBatchedAsync(
            folder.Id, 
            testFolderPath, 
            _mockProgressService.Object, 
            batchSize: 50);

        // Assert
        Assert.Equal(imageCount, photos.Count);
        
        var dbCount = await _photoService.GetPhotoCountByFolderIdAsync(folder.Id, includeHidden: false);
        Assert.Equal(imageCount, dbCount);

        // Verify filesystem count matches
        var fsImages = Directory.GetFiles(testFolderPath)
            .Where(f => PhotoService.IsSupportedImageFile(f))
            .Count();
        Assert.Equal(imageCount, fsImages);
    }

    [Fact]
    public async Task FolderImageCount_ExcludesHiddenPhotos()
    {
        // Arrange
        var testFolderName = "TestFolder2";
        var testFolderPath = Path.Combine(_testGalleryPath, testFolderName);
        Directory.CreateDirectory(testFolderPath);

        var imageCount = 3;
        for (int i = 0; i < imageCount; i++)
        {
            CreateTestImageFile(Path.Combine(testFolderPath, $"test{i}.jpg"), 800, 600);
        }

        var folder = await _folderService.CreateOrUpdateFolderAsync(testFolderPath, testFolderName);
        var photos = await _photoService.ScanPhotosInFolderBatchedAsync(
            folder.Id, testFolderPath, _mockProgressService.Object);

        // Act - Hide one photo
        await _photoService.UpdatePhotoVisibilityAsync(photos[0].Id, false);

        // Assert - Visible count should be less than total
        var visibleCount = await _photoService.GetPhotoCountByFolderIdAsync(folder.Id, includeHidden: false);
        var totalCount = await _photoService.GetPhotoCountByFolderIdAsync(folder.Id, includeHidden: true);
        
        Assert.Equal(imageCount - 1, visibleCount);
        Assert.Equal(imageCount, totalCount);
    }

    [Fact]
    public async Task GetPhotosByFolderId_ReturnsCorrectCount()
    {
        // Arrange
        var testFolderName = "TestFolder3";
        var testFolderPath = Path.Combine(_testGalleryPath, testFolderName);
        Directory.CreateDirectory(testFolderPath);

        var imageCount = 10;
        for (int i = 0; i < imageCount; i++)
        {
            CreateTestImageFile(Path.Combine(testFolderPath, $"test{i}.jpg"), 800, 600);
        }

        var folder = await _folderService.CreateOrUpdateFolderAsync(testFolderPath, testFolderName);
        await _photoService.ScanPhotosInFolderBatchedAsync(
            folder.Id, testFolderPath, _mockProgressService.Object);

        // Act
        var photosPage1 = await _photoService.GetPhotosByFolderIdAsync(folder.Id, skip: 0, take: 5);
        var photosPage2 = await _photoService.GetPhotosByFolderIdAsync(folder.Id, skip: 5, take: 5);

        // Assert
        Assert.Equal(5, photosPage1.Count);
        Assert.Equal(5, photosPage2.Count);
        
        // Verify no duplicates
        var allPhotoIds = photosPage1.Select(p => p.Id).Concat(photosPage2.Select(p => p.Id)).ToList();
        var uniqueIds = allPhotoIds.Distinct().ToList();
        Assert.Equal(allPhotoIds.Count, uniqueIds.Count);
    }

    [Fact]
    public async Task ScanPhotos_DetectsNewImages()
    {
        // Arrange
        var testFolderName = "TestFolder4";
        var testFolderPath = Path.Combine(_testGalleryPath, testFolderName);
        Directory.CreateDirectory(testFolderPath);

        var folder = await _folderService.CreateOrUpdateFolderAsync(testFolderPath, testFolderName);

        // First scan - 3 images
        for (int i = 0; i < 3; i++)
        {
            CreateTestImageFile(Path.Combine(testFolderPath, $"test{i}.jpg"), 800, 600);
        }
        var photos1 = await _photoService.ScanPhotosInFolderBatchedAsync(
            folder.Id, testFolderPath, _mockProgressService.Object);
        Assert.Equal(3, photos1.Count);

        // Act - Add 2 more images and scan again
        for (int i = 3; i < 5; i++)
        {
            CreateTestImageFile(Path.Combine(testFolderPath, $"test{i}.jpg"), 800, 600);
        }
        var photos2 = await _photoService.ScanPhotosInFolderBatchedAsync(
            folder.Id, testFolderPath, _mockProgressService.Object);

        // Assert
        var totalCount = await _photoService.GetPhotoCountByFolderIdAsync(folder.Id);
        Assert.Equal(5, totalCount);
    }

    [Fact]
    public async Task PhotosInFolder_AreDistinct()
    {
        // Arrange
        var testFolderName = "TestFolder5";
        var testFolderPath = Path.Combine(_testGalleryPath, testFolderName);
        Directory.CreateDirectory(testFolderPath);

        var imageCount = 10;
        for (int i = 0; i < imageCount; i++)
        {
            CreateTestImageFile(Path.Combine(testFolderPath, $"test{i}.jpg"), 800, 600);
        }

        var folder = await _folderService.CreateOrUpdateFolderAsync(testFolderPath, testFolderName);
        await _photoService.ScanPhotosInFolderBatchedAsync(
            folder.Id, testFolderPath, _mockProgressService.Object);

        // Act - Get all photos
        var allPhotos = await _photoService.GetAllPhotosByFolderIdAsync(folder.Id);

        // Assert - No duplicates
        var photoIds = allPhotos.Select(p => p.Id).ToList();
        var uniqueIds = photoIds.Distinct().ToList();
        Assert.Equal(imageCount, uniqueIds.Count);
        Assert.Equal(imageCount, allPhotos.Count);

        // Verify file paths are unique
        var filePaths = allPhotos.Select(p => p.FilePath).ToList();
        var uniquePaths = filePaths.Distinct().ToList();
        Assert.Equal(imageCount, uniquePaths.Count);
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
        }
        catch { }
    }
}
