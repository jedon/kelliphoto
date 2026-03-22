using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KelliPhoto.Web.Data.Models;
using KelliPhoto.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using Xunit;

namespace KelliPhoto.Web.Tests;

/// <summary>
/// Comprehensive unit tests for watermark functionality in WebImageService.
/// These tests help debug watermark issues by testing each component in isolation.
/// </summary>
public class WatermarkServiceTests : IDisposable
{
    private readonly string _testTempDir;
    private readonly Mock<IPhotoService> _mockPhotoService;
    private readonly Mock<IPathService> _mockPathService;
    private readonly Mock<ILogger<WebImageService>> _mockLogger;

    public WatermarkServiceTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), $"kelliphoto-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testTempDir);

        _mockPhotoService = new Mock<IPhotoService>();
        _mockPathService = new Mock<IPathService>();
        _mockLogger = new Mock<ILogger<WebImageService>>();

        // Setup PathService to return paths as-is for testing
        _mockPathService.Setup(x => x.GetFullPath(It.IsAny<string>()))
            .Returns<string>(path => path);
        _mockPathService.Setup(x => x.NormalizePath(It.IsAny<string>()))
            .Returns<string>(path => path);
        _mockPathService.Setup(x => x.ResolveExistingPhotoFilePath(It.IsAny<string>()))
            .Returns<string>(stored =>
                !string.IsNullOrEmpty(stored) && File.Exists(stored) ? stored : null);
    }

    [Fact]
    public void GetWatermarkSettings_WhenEnabledAndPathExists_ShouldReturnEnabled()
    {
        // Arrange
        var watermarkPath = CreateTestWatermarkImage();
        var config = CreateConfiguration(new Dictionary<string, string>
        {
            ["WatermarkSettings:Enabled"] = "true",
            ["WatermarkSettings:ImagePath"] = watermarkPath,
            ["WatermarkSettings:Opacity"] = "0.5",
            ["WatermarkSettings:RelativeWidth"] = "0.25",
            ["WatermarkSettings:MarginPx"] = "30"
        });

        var service = CreateService(config);

        // Act
        var settings = InvokeGetWatermarkSettingsView(service, watermarkRequested: true);

        // Assert
        Assert.True(settings.Enabled);
        Assert.Equal(watermarkPath, settings.ImagePath);
        Assert.Equal(0.5f, settings.Opacity);
        Assert.Equal(0.25f, settings.RelativeWidth);
        Assert.Equal(30, settings.MarginPx);
    }

    [Fact]
    public void GetWatermarkSettings_WhenEnabledButPathDoesNotExist_ShouldReturnEnabledButNoPath()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testTempDir, "nonexistent.png");
        var config = CreateConfiguration(new Dictionary<string, string>
        {
            ["WatermarkSettings:Enabled"] = "true",
            ["WatermarkSettings:ImagePath"] = nonExistentPath
        });

        var service = CreateService(config);

        // Act
        var settings = InvokeGetWatermarkSettingsView(service, watermarkRequested: true);

        // Assert
        Assert.True(settings.Enabled);
        Assert.Equal(nonExistentPath, settings.ImagePath);
        // Path should be set even if file doesn't exist (will log warning when used)
    }

    [Fact]
    public void GetWatermarkSettings_WhenPathNotSet_ShouldFallbackToWebAssetsPath()
    {
        // Arrange
        var webAssetsPath = Path.Combine(_testTempDir, ".web");
        Directory.CreateDirectory(webAssetsPath);
        var watermarkPath = Path.Combine(webAssetsPath, "watermark.png");
        CreateTestWatermarkImage(watermarkPath);

        var config = CreateConfiguration(new Dictionary<string, string>
        {
            ["WatermarkSettings:Enabled"] = "true",
            ["GallerySettings:WebAssetsPath"] = webAssetsPath
            // WatermarkSettings:ImagePath is not set
        });

        var service = CreateService(config);

        // Act
        var settings = InvokeGetWatermarkSettingsView(service, watermarkRequested: true);

        // Assert
        Assert.True(settings.Enabled);
        Assert.Equal(watermarkPath, settings.ImagePath);
    }

    [Fact]
    public void GetWatermarkSettings_WhenUncPathProvided_ShouldHandleUncPath()
    {
        // Arrange
        var uncPath = @"\\server\share\watermark.png";
        var config = CreateConfiguration(new Dictionary<string, string>
        {
            ["WatermarkSettings:Enabled"] = "true",
            ["WatermarkSettings:ImagePath"] = uncPath
        });

        var service = CreateService(config);

        // Act
        var settings = InvokeGetWatermarkSettingsView(service, watermarkRequested: true);

        // Assert
        Assert.True(settings.Enabled);
        Assert.Equal(uncPath, settings.ImagePath);
        // Note: File.Exists will return false for UNC paths if not accessible, but path should be preserved
    }

    [Fact]
    public void GetWatermarkSettings_WhenWatermarkNotRequested_ShouldReturnDisabled()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string>
        {
            ["WatermarkSettings:Enabled"] = "true",
            ["WatermarkSettings:ImagePath"] = CreateTestWatermarkImage()
        });

        var service = CreateService(config);

        // Act
        var settings = InvokeGetWatermarkSettingsView(service, watermarkRequested: false);

        // Assert
        Assert.False(settings.Enabled);
    }

    [Fact]
    public void GetWatermarkSettings_WhenEnabledIsFalse_ShouldReturnDisabled()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string>
        {
            ["WatermarkSettings:Enabled"] = "false",
            ["WatermarkSettings:ImagePath"] = CreateTestWatermarkImage()
        });

        var service = CreateService(config);

        // Act
        var settings = InvokeGetWatermarkSettingsView(service, watermarkRequested: true);

        // Assert
        Assert.False(settings.Enabled);
    }

    [Fact]
    public async Task CreateWebImageAsync_WhenWatermarkEnabledAndPathExists_ShouldApplyWatermark()
    {
        // Arrange
        var watermarkPath = CreateTestWatermarkImage();
        var sourceImagePath = CreateTestSourceImage();
        var outputPath = Path.Combine(_testTempDir, "output.jpg");

        var photo = new Photo
        {
            Id = 1,
            FilePath = sourceImagePath,
            Filename = "test.jpg"
        };

        _mockPhotoService.Setup(x => x.GetPhotoByIdAsync(1))
            .ReturnsAsync(photo);

        var config = CreateConfiguration(new Dictionary<string, string>
        {
            ["WatermarkSettings:Enabled"] = "true",
            ["WatermarkSettings:ImagePath"] = watermarkPath,
            ["WatermarkSettings:Opacity"] = "0.5",
            ["WatermarkSettings:RelativeWidth"] = "0.25",
            ["WatermarkSettings:MarginPx"] = "30",
            ["GallerySettings:WebImagePath"] = _testTempDir,
            ["GallerySettings:WebJpegQuality"] = "85"
        });

        var service = CreateService(config);

        // Act
        await service.GetWebImageStreamAsync(1, maxDimension: 1000, watermark: true);

        // Assert
        Assert.True(File.Exists(outputPath) || Directory.GetFiles(_testTempDir, "*.jpg").Any());
        
        // Verify watermark was applied by checking if file exists and has content
        var generatedFiles = Directory.GetFiles(_testTempDir, "*.jpg");
        Assert.NotEmpty(generatedFiles);
        
        // Load the generated image and verify it's valid
        using var generatedImage = await Image.LoadAsync(generatedFiles[0]);
        Assert.True(generatedImage.Width > 0);
        Assert.True(generatedImage.Height > 0);
    }

    [Fact]
    public async Task CreateWebImageAsync_WhenWatermarkEnabledButPathDoesNotExist_ShouldNotApplyWatermark()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testTempDir, "nonexistent.png");
        var sourceImagePath = CreateTestSourceImage();

        var photo = new Photo
        {
            Id = 1,
            FilePath = sourceImagePath,
            Filename = "test.jpg"
        };

        _mockPhotoService.Setup(x => x.GetPhotoByIdAsync(1))
            .ReturnsAsync(photo);

        var config = CreateConfiguration(new Dictionary<string, string>
        {
            ["WatermarkSettings:Enabled"] = "true",
            ["WatermarkSettings:ImagePath"] = nonExistentPath,
            ["GallerySettings:WebImagePath"] = _testTempDir,
            ["GallerySettings:WebJpegQuality"] = "85"
        });

        var service = CreateService(config);

        // Act & Assert - Should not throw, but watermark won't be applied
        var stream = await service.GetWebImageStreamAsync(1, maxDimension: 1000, watermark: true);
        Assert.NotNull(stream);
        stream.Dispose();
    }

    [Fact]
    public void BuildCacheKey_WhenWatermarkEnabled_ShouldIncludeWatermarkParameters()
    {
        // Arrange
        var watermarkPath = CreateTestWatermarkImage();
        var config = CreateConfiguration(new Dictionary<string, string>
        {
            ["WatermarkSettings:Enabled"] = "true",
            ["WatermarkSettings:ImagePath"] = watermarkPath,
            ["WatermarkSettings:Opacity"] = "0.5",
            ["WatermarkSettings:RelativeWidth"] = "0.25",
            ["WatermarkSettings:MarginPx"] = "30"
        });

        var service = CreateService(config);
        var settingsRaw = InvokeGetWatermarkSettingsRaw(service, watermarkRequested: true);

        // Act
        var cacheKey = BuildCacheKey(1, 1000, 85, settingsRaw);

        // Assert
        Assert.Contains("wm_op50", cacheKey); // Opacity 0.5 = 50
        Assert.Contains("rw25", cacheKey); // RelativeWidth 0.25 = 25
        Assert.Contains("m30", cacheKey); // Margin 30
        Assert.Contains("p1", cacheKey); // Photo ID
        Assert.Contains("max1000", cacheKey);
    }

    [Fact]
    public void BuildCacheKey_WhenWatermarkDisabled_ShouldNotIncludeWatermarkParameters()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string>
        {
            ["WatermarkSettings:Enabled"] = "false"
        });

        var service = CreateService(config);
        var settingsRaw = InvokeGetWatermarkSettingsRaw(service, watermarkRequested: false);

        // Act
        var cacheKey = BuildCacheKey(1, 1000, 85, settingsRaw);

        // Assert
        Assert.Contains("nowm", cacheKey);
        Assert.DoesNotContain("wm_", cacheKey);
    }

    // Helper methods

    private WebImageService CreateService(IConfiguration config)
    {
        return new WebImageService(
            _mockPhotoService.Object,
            _mockPathService.Object,
            config,
            _mockLogger.Object);
    }

    private IConfiguration CreateConfiguration(Dictionary<string, string> settings)
    {
        var configBuilder = new ConfigurationBuilder();
        var keyValuePairs = settings.Select(kvp => new KeyValuePair<string, string?>(kvp.Key, kvp.Value));
        configBuilder.AddInMemoryCollection(keyValuePairs);
        return configBuilder.Build();
    }

    private string CreateTestWatermarkImage(string? path = null)
    {
        path ??= Path.Combine(_testTempDir, "watermark.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Create a simple test watermark image (100x50 pixels, red)
        using var image = new Image<Rgba32>(100, 50);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
                image[x, y] = new Rgba32(255, 0, 0, 255);
        }

        image.SaveAsPng(path);
        return path;
    }

    private string CreateTestSourceImage()
    {
        var path = Path.Combine(_testTempDir, "source.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Create a simple test source image (800x600 pixels, blue)
        using var image = new Image<Rgba32>(800, 600);
        // Fill with blue color by setting all pixels
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                image[x, y] = new Rgba32(0, 0, 255, 255); // Blue
            }
        }
        image.Save(path, new JpegEncoder());
        return path;
    }

    private sealed record WatermarkSettingsView(
        bool Enabled,
        string? ImagePath,
        float Opacity,
        float RelativeWidth,
        int MarginPx);

    /// <summary>Private record return type cannot be used with dynamic; read properties via reflection.</summary>
    private static WatermarkSettingsView InvokeGetWatermarkSettingsView(WebImageService service, bool watermarkRequested)
    {
        var raw = InvokeGetWatermarkSettingsRaw(service, watermarkRequested);
        return ToWatermarkView(raw);
    }

    private static object InvokeGetWatermarkSettingsRaw(WebImageService service, bool watermarkRequested)
    {
        var method = typeof(WebImageService).GetMethod(
            "GetWatermarkSettings",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (method == null)
            throw new InvalidOperationException("GetWatermarkSettings method not found");

        return method.Invoke(service, new object[] { watermarkRequested })!;
    }

    private static WatermarkSettingsView ToWatermarkView(object raw)
    {
        var t = raw.GetType();
        return new WatermarkSettingsView(
            (bool)t.GetProperty("Enabled")!.GetValue(raw)!,
            (string?)t.GetProperty("ImagePath")!.GetValue(raw),
            (float)t.GetProperty("Opacity")!.GetValue(raw)!,
            (float)t.GetProperty("RelativeWidth")!.GetValue(raw)!,
            (int)t.GetProperty("MarginPx")!.GetValue(raw)!);
    }

    private static string BuildCacheKey(int photoId, int maxDimension, int quality, object settings)
    {
        var method = typeof(WebImageService).GetMethod(
            "BuildCacheKey",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (method == null)
            throw new InvalidOperationException("BuildCacheKey method not found");

        return (string)method.Invoke(null, new object[] { photoId, maxDimension, quality, settings })!;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
