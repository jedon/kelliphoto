using System.Diagnostics;
using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using KelliPhoto.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Jpeg;
using Xunit;

namespace KelliPhoto.Web.Tests;

// Simple test implementation of IDbContextFactory
public class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
{
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
    {
        _options = options;
    }

    public ApplicationDbContext CreateDbContext()
    {
        return new ApplicationDbContext(_options);
    }

    public async ValueTask<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return await ValueTask.FromResult(new ApplicationDbContext(_options));
    }
}

public class PhotoScanningPerformanceTests : IDisposable
{
    private readonly string _testFolderPath;
    private readonly ApplicationDbContext _context;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IPathService _pathService;
    private readonly IPhotoService _photoService;
    private readonly Mock<IScanProgressService> _mockProgressService;

    public PhotoScanningPerformanceTests()
    {
        // Create a temporary test folder
        _testFolderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testFolderPath);

        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        // Create DbContextFactory
        _contextFactory = new TestDbContextFactory(options);

        // Setup PathService
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GallerySettings:GalleryPath", _testFolderPath }
            })
            .Build();
        _pathService = new PathService(config);

        // Setup logger
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<PhotoService>();

        // Create PhotoService
        _photoService = new PhotoService(_contextFactory, _pathService, logger);

        // Setup mock progress service
        _mockProgressService = new Mock<IScanProgressService>();
        _mockProgressService.Setup(s => s.StartScan(It.IsAny<int>(), It.IsAny<int>()));
        _mockProgressService.Setup(s => s.UpdateProgress(It.IsAny<int>(), It.IsAny<int>()));
        _mockProgressService.Setup(s => s.CompleteScan(It.IsAny<int>()));

        // Create a test folder in the database
        // Use empty path to represent the root gallery folder
        var folder = new Folder
        {
            Name = "TestFolder",
            Path = "", // Empty path means root gallery folder
            IsVisible = true
        };
        _context.Folders.Add(folder);
        _context.SaveChanges();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public async Task CompareScanningMethods_WithDifferentPhotoCounts(int photoCount)
    {
        // Arrange
        var folder = await _context.Folders.FirstAsync();
        await CreateTestImages(photoCount);

        // Act - Baseline method
        var baselineStopwatch = Stopwatch.StartNew();
        var baselinePhotos = await _photoService.ScanPhotosInFolderAsync(
            folder.Id, 
            folder.Path, 
            _mockProgressService.Object);
        baselineStopwatch.Stop();

        // Clear database for batched test
        _context.Photos.RemoveRange(_context.Photos);
        await _context.SaveChangesAsync();
        
        // Wait a bit to ensure cleanup is complete
        await Task.Delay(100);

        // Act - Batched method (batch size 50)
        var batchedStopwatch = Stopwatch.StartNew();
        var batchedPhotos = await _photoService.ScanPhotosInFolderBatchedAsync(
            folder.Id, 
            folder.Path, 
            _mockProgressService.Object, 
            batchSize: 50);
        batchedStopwatch.Stop();

        // Assert
        Assert.Equal(photoCount, baselinePhotos.Count);
        Assert.Equal(photoCount, batchedPhotos.Count);
        
        // Log results
        var improvement = ((double)(baselineStopwatch.ElapsedMilliseconds - batchedStopwatch.ElapsedMilliseconds) / baselineStopwatch.ElapsedMilliseconds) * 100;
        Console.WriteLine($"\n=== Performance Comparison for {photoCount} photos ===");
        Console.WriteLine($"Baseline Method: {baselineStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Batched Method (50): {batchedStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Improvement: {improvement:F2}%");
        Console.WriteLine($"Speedup: {baselineStopwatch.ElapsedMilliseconds / (double)batchedStopwatch.ElapsedMilliseconds:F2}x");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public async Task CompareBatchedMethods_WithDifferentBatchSizes(int batchSize)
    {
        // Arrange
        var photoCount = 200;
        var folder = await _context.Folders.FirstAsync();
        await CreateTestImages(photoCount);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var photos = await _photoService.ScanPhotosInFolderBatchedAsync(
            folder.Id, 
            folder.Path, 
            _mockProgressService.Object, 
            batchSize: batchSize);
        stopwatch.Stop();

        // Assert
        Assert.Equal(photoCount, photos.Count);
        
        // Log results
        Console.WriteLine($"\n=== Batch Size {batchSize} for {photoCount} photos ===");
        Console.WriteLine($"Time: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Average per photo: {stopwatch.ElapsedMilliseconds / (double)photoCount:F2}ms");
    }

    [Fact]
    public async Task BaselineMethod_WithExistingPhotos_UpdatesCorrectly()
    {
        // Arrange
        var folder = await _context.Folders.FirstAsync();
        var photoCount = 50;
        await CreateTestImages(photoCount);

        // First scan - all new photos
        var firstScan = await _photoService.ScanPhotosInFolderAsync(
            folder.Id, 
            folder.Path, 
            _mockProgressService.Object);
        
        Assert.Equal(photoCount, firstScan.Count);
        
        // Wait a bit between scans
        await Task.Delay(100);

        // Second scan - should update existing photos
        var secondScanStopwatch = Stopwatch.StartNew();
        var secondScan = await _photoService.ScanPhotosInFolderAsync(
            folder.Id, 
            folder.Path, 
            _mockProgressService.Object);
        secondScanStopwatch.Stop();

        Assert.Equal(photoCount, secondScan.Count);
        Console.WriteLine($"\n=== Baseline Update Scan for {photoCount} existing photos ===");
        Console.WriteLine($"Time: {secondScanStopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ScanPhotosInFolderBatchedAsync_CallsRefreshFromFileForNewPhotos()
    {
        var folder = await _context.Folders.FirstAsync();
        await CreateTestImages(3);

        var mockMeta = new Mock<IPhotoMetadataService>();
        mockMeta.Setup(m => m.RefreshFromFileAsync(It.IsAny<int>())).Returns(Task.CompletedTask);

        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<PhotoService>();
        var service = new PhotoService(
            _contextFactory,
            _pathService,
            logger,
            photoMetadataService: mockMeta.Object);

        var photos = await service.ScanPhotosInFolderBatchedAsync(
            folder.Id,
            folder.Path,
            _mockProgressService.Object,
            batchSize: 2);

        Assert.Equal(3, photos.Count);
        foreach (var photo in photos)
            mockMeta.Verify(m => m.RefreshFromFileAsync(photo.Id), Times.Once);
    }

    [Fact]
    public async Task BatchedMethod_WithExistingPhotos_UpdatesCorrectly()
    {
        // Arrange
        var folder = await _context.Folders.FirstAsync();
        var photoCount = 50;
        await CreateTestImages(photoCount);

        // First scan - all new photos
        var firstScan = await _photoService.ScanPhotosInFolderBatchedAsync(
            folder.Id, 
            folder.Path, 
            _mockProgressService.Object, 
            batchSize: 25);
        
        Assert.Equal(photoCount, firstScan.Count);
        
        // Wait a bit between scans
        await Task.Delay(100);

        // Second scan - should update existing photos
        var secondScanStopwatch = Stopwatch.StartNew();
        var secondScan = await _photoService.ScanPhotosInFolderBatchedAsync(
            folder.Id, 
            folder.Path, 
            _mockProgressService.Object, 
            batchSize: 25);
        secondScanStopwatch.Stop();

        Assert.Equal(photoCount, secondScan.Count);
        Console.WriteLine($"\n=== Batched Update Scan for {photoCount} existing photos ===");
        Console.WriteLine($"Time: {secondScanStopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ComprehensivePerformanceTest()
    {
        // Arrange
        var folder = await _context.Folders.FirstAsync();
        var photoCounts = new[] { 10, 25, 50, 100, 200 };
        var batchSizes = new[] { 10, 25, 50, 100 };

        Console.WriteLine("\n=== COMPREHENSIVE PERFORMANCE TEST ===");
        Console.WriteLine("Photo Count | Baseline | Batch 10 | Batch 25 | Batch 50 | Batch 100 | Best");
        Console.WriteLine("------------|----------|----------|----------|-----------|-----------|-----");

        foreach (var count in photoCounts)
        {
            await CreateTestImages(count);
            var results = new Dictionary<string, long>();

            // Baseline
            _context.Photos.RemoveRange(_context.Photos);
            await _context.SaveChangesAsync();
            var baselineStopwatch = Stopwatch.StartNew();
            await _photoService.ScanPhotosInFolderAsync(folder.Id, folder.Path, _mockProgressService.Object);
            baselineStopwatch.Stop();
            results["Baseline"] = baselineStopwatch.ElapsedMilliseconds;

            // Test each batch size
            foreach (var batchSize in batchSizes)
            {
                _context.Photos.RemoveRange(_context.Photos);
                await _context.SaveChangesAsync();
                var stopwatch = Stopwatch.StartNew();
                await _photoService.ScanPhotosInFolderBatchedAsync(
                    folder.Id, 
                    folder.Path, 
                    _mockProgressService.Object, 
                    batchSize: batchSize);
                stopwatch.Stop();
                results[$"Batch {batchSize}"] = stopwatch.ElapsedMilliseconds;
            }

            var best = results.OrderBy(r => r.Value).First();
            Console.WriteLine($"{count,11} | {results["Baseline"],8} | {results["Batch 10"],8} | {results["Batch 25"],8} | {results["Batch 50"],9} | {results["Batch 100"],10} | {best.Key} ({best.Value}ms)");
        }
    }

    private async Task CreateTestImages(int count)
    {
        // Clean up existing test images
        var existingFiles = Directory.GetFiles(_testFolderPath, "*.jpg");
        foreach (var file in existingFiles)
        {
            File.Delete(file);
        }

        // Create simple test images using ImageSharp
        for (int i = 0; i < count; i++)
        {
            var filePath = Path.Combine(_testFolderPath, $"test_image_{i:D4}.jpg");
            using var image = new Image<Rgba32>(100, 100);
            await using var fileStream = File.Create(filePath);
            await image.SaveAsync(fileStream, new JpegEncoder());
        }
    }

    public void Dispose()
    {
        // Cleanup
        if (Directory.Exists(_testFolderPath))
        {
            Directory.Delete(_testFolderPath, true);
        }
        _context?.Dispose();
    }
}
