using System.Net;
using System.Net.Http.Json;
using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using Xunit;

namespace KelliPhoto.Web.Tests;

// Custom WebApplicationFactory that can work with top-level Program
public class KelliPhotoWebApplicationFactory : WebApplicationFactory<Program>
{
    static KelliPhotoWebApplicationFactory()
    {
        // CreateBuilder runs before ConfigureWebHost can apply UseEnvironment("Testing"); launchSettings may leave
        // Development. Program keys off this for in-memory EF + skipping HTTPS redirect for TestServer.
        Environment.SetEnvironmentVariable("KELLIPHOTO_INTEGRATION_TEST", "1");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        base.ConfigureWebHost(builder);
    }
}

public class ImageEndpointIntegrationTests : IClassFixture<KelliPhotoWebApplicationFactory>, IDisposable
{
    /// <summary>Configured per test via <see cref="WebApplicationFactory{TEntryPoint}.WithWebHostBuilder"/> — not the fixture subclass type.</summary>
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testGalleryPath;
    private readonly string _testWebAssetsPath;
    private readonly string _testWebImagePath;
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public ImageEndpointIntegrationTests(KelliPhotoWebApplicationFactory factory)
    {
        Environment.SetEnvironmentVariable("KELLIPHOTO_INTEGRATION_TEST", "1");

        // Create temporary test directories
        var baseTestDir = Path.Combine(Path.GetTempPath(), "kelliphoto-tests", Guid.NewGuid().ToString());
        _testGalleryPath = Path.Combine(baseTestDir, "gallery");
        _testWebAssetsPath = Path.Combine(_testGalleryPath, ".web");
        _testWebImagePath = Path.Combine(baseTestDir, "webimages");
        
        Directory.CreateDirectory(_testGalleryPath);
        Directory.CreateDirectory(_testWebAssetsPath);
        Directory.CreateDirectory(_testWebImagePath);

        // Isolate in-memory DB per host — must be in the environment before CreateClient builds the app.
        var testDatabaseName = "TestDb_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable("KELLIPHOTO_INMEMORY_DB", testDatabaseName);

        // WithWebHostBuilder returns WebApplicationFactory<Program>, not KelliPhotoWebApplicationFactory — do not cast; a failed cast
        // would leave the shared fixture without gallery overrides and every image test would get NotFound.
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Testing:InMemoryDatabaseName", testDatabaseName },
                    { "GallerySettings:GalleryPath", _testGalleryPath },
                    { "GallerySettings:ThumbnailPath", Path.Combine(_testGalleryPath, ".thumbnails") },
                    { "GallerySettings:WebImagePath", _testWebImagePath },
                    { "GallerySettings:WebAssetsPath", _testWebAssetsPath },
                    { "GallerySettings:WebJpegQuality", "85" },
                    { "WatermarkSettings:Enabled", "false" }
                });
            });
        });
        _client = _factory.CreateClient();

        // Same factory the app uses for requests — avoids a separate scoped context seeing a different in-memory store.
        _dbFactory = _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        using (var ctx = _dbFactory.CreateDbContext())
            ctx.Database.EnsureCreated();
    }

    [Fact]
    public async Task GetThumbnail_Returns200_WhenPhotoExists()
    {
        // Arrange
        var folder = await CreateTestFolderAsync("TestFolder");
        var photo = await CreateTestPhotoAsync(folder.Id, "test.jpg");

        // Act
        var response = await _client.GetAsync($"/api/images/thumbnail/{photo.Id}?size=300");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetThumbnail_Returns404_WhenPhotoNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/images/thumbnail/99999?size=300");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetWebPhoto_Returns200_WhenPhotoExists()
    {
        // Arrange
        var folder = await CreateTestFolderAsync("TestFolder");
        var photo = await CreateTestPhotoAsync(folder.Id, "test.jpg");

        // Act — default watermark applies for anonymous callers
        var response = await _client.GetAsync($"/api/images/web/{photo.Id}?max=2000");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetThumbnail_Returns404_WhenPhotoIsHidden()
    {
        var folder = await CreateTestFolderAsync("TestFolder");
        var photo = await CreateTestPhotoAsync(folder.Id, "hidden.jpg", isVisible: false);

        var response = await _client.GetAsync($"/api/images/thumbnail/{photo.Id}?size=300");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetWebPhoto_Returns404_WhenPhotoIsHidden()
    {
        var folder = await CreateTestFolderAsync("TestFolder");
        var photo = await CreateTestPhotoAsync(folder.Id, "hidden.jpg", isVisible: false);

        var response = await _client.GetAsync($"/api/images/web/{photo.Id}?max=2000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetThumbnail_Returns404_WhenFolderIsHidden()
    {
        var folder = await CreateTestFolderAsync("HiddenFolder", isVisible: false);
        var photo = await CreateTestPhotoAsync(folder.Id, "test.jpg");

        var response = await _client.GetAsync($"/api/images/thumbnail/{photo.Id}?size=300");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetWebPhoto_Returns404_WhenFolderIsHidden()
    {
        var folder = await CreateTestFolderAsync("HiddenFolder", isVisible: false);
        var photo = await CreateTestPhotoAsync(folder.Id, "test.jpg");

        var response = await _client.GetAsync($"/api/images/web/{photo.Id}?max=2000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetWebPhoto_Returns200_WhenAnonymousRequestsWatermarkFalse()
    {
        var folder = await CreateTestFolderAsync("TestFolder");
        var photo = await CreateTestPhotoAsync(folder.Id, "test.jpg");

        var response = await _client.GetAsync($"/api/images/web/{photo.Id}?max=2000&watermark=false");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetThumbnail_Returns200_WhenSizeIsAbsurdlyLarge()
    {
        var folder = await CreateTestFolderAsync("TestFolder");
        var photo = await CreateTestPhotoAsync(folder.Id, "test.jpg");

        var response = await _client.GetAsync($"/api/images/thumbnail/{photo.Id}?size=50000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetWebPhoto_Returns200_WhenMaxIsAbsurdlyLarge()
    {
        var folder = await CreateTestFolderAsync("TestFolder");
        var photo = await CreateTestPhotoAsync(folder.Id, "test.jpg");

        var response = await _client.GetAsync($"/api/images/web/{photo.Id}?max=50000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetWebPhoto_Returns200_ForHomePageHighlightsFolderEvenWhenHidden()
    {
        var folder = await CreateTestFolderAsync("Home Page Highlights", isVisible: false);
        var photo = await CreateTestPhotoAsync(folder.Id, "highlight.jpg");

        var response = await _client.GetAsync($"/api/images/web/{photo.Id}?max=2000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWebPhoto_Returns404_WhenPhotoNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/images/web/99999?max=2000");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetWebAsset_Returns200_WhenAssetExists()
    {
        // Arrange
        var testLogoPath = Path.Combine(_testWebAssetsPath, "logo.png");
        CreateTestPngFile(testLogoPath, 100, 100);

        // Act
        var response = await _client.GetAsync("/api/images/webasset/logo.png");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetWebAsset_Returns404_WhenAssetNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/images/webasset/nonexistent.png");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetWebAsset_RejectsPathTraversalAttempt()
    {
        // Act — encoded slash can decode to an extra path segment so the single-segment route never matches (404),
        // or reach the action with a multi-segment value so Path.GetFileName sanitizes and returns BadRequest.
        var response = await _client.GetAsync("/api/images/webasset/nested%2Flogo.png");

        // Assert — must not serve a file; either outcome is acceptable across hosts.
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 400 or 404 for traversal probe, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetPhoto_Returns401_WhenNotAuthenticated()
    {
        // Arrange
        var folder = await CreateTestFolderAsync("TestFolder");
        var photo = await CreateTestPhotoAsync(folder.Id, "test.jpg");

        // Act - Original photo endpoint requires AdminOnly
        var response = await _client.GetAsync($"/api/images/photo/{photo.Id}");

        // Assert - Should be 401 (Unauthorized) or 403 (Forbidden) since not authenticated as admin
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || 
                   response.StatusCode == HttpStatusCode.Forbidden || 
                   response.StatusCode == HttpStatusCode.Redirect);
    }

    private async Task<Folder> CreateTestFolderAsync(string name, bool isVisible = true, int? parentId = null)
    {
        await using var context = await _dbFactory.CreateDbContextAsync();
        var folder = new Folder
        {
            Name = name,
            Path = name,
            ParentId = parentId,
            IsVisible = isVisible,
            CreatedAt = DateTime.UtcNow
        };
        context.Folders.Add(folder);
        await context.SaveChangesAsync();
        return folder;
    }

    private async Task<Photo> CreateTestPhotoAsync(int folderId, string filename, bool isVisible = true)
    {
        // Create actual image file
        var imagePath = Path.Combine(_testGalleryPath, filename);
        CreateTestImageFile(imagePath, 800, 600);

        await using var context = await _dbFactory.CreateDbContextAsync();
        var photo = new Photo
        {
            Filename = filename,
            FilePath = filename,
            FolderId = folderId,
            FileSize = 1024,
            Width = 800,
            Height = 600,
            IsVisible = isVisible,
            CreatedAt = DateTime.UtcNow
        };
        context.Photos.Add(photo);
        await context.SaveChangesAsync();
        return photo;
    }

    private void CreateTestImageFile(string path, int width, int height)
    {
        using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
        image.Save(path, new JpegEncoder());
    }

    private static void CreateTestPngFile(string path, int width, int height)
    {
        using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
        image.SaveAsPng(path);
    }

    public void Dispose()
    {
        _client?.Dispose();
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
