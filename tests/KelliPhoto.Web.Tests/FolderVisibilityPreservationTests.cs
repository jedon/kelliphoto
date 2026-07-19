using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using KelliPhoto.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace KelliPhoto.Web.Tests;

public class FolderVisibilityPreservationTests : IDisposable
{
    private readonly string _testGalleryPath;
    private readonly ApplicationDbContext _context;
    private readonly IFolderService _folderService;

    public FolderVisibilityPreservationTests()
    {
        _testGalleryPath = Path.Combine(Path.GetTempPath(), "kelliphoto-visibility-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testGalleryPath);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        var contextFactory = new TestDbContextFactory(options);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GallerySettings:GalleryPath", _testGalleryPath }
            })
            .Build();

        _folderService = new FolderService(
            contextFactory,
            new PathService(config),
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<FolderService>());
    }

    [Fact]
    public async Task CreateOrUpdateFolderAsync_PreservesAdminHiddenVisibility_OnRescan()
    {
        var folderName = "CuratedAlbum";
        var folderPath = Path.Combine(_testGalleryPath, folderName);
        Directory.CreateDirectory(folderPath);

        var folder = await _folderService.CreateOrUpdateFolderAsync(folderPath, folderName);
        Assert.True(folder.IsVisible);

        await _folderService.UpdateFolderSettingsAsync(folder.Id, folderName, sortOrder: 0, isVisible: false, description: null);

        var rescanned = await _folderService.CreateOrUpdateFolderAsync(folderPath, folderName);

        Assert.False(rescanned.IsVisible);

        var fromDb = await _context.Folders.FindAsync(folder.Id);
        Assert.NotNull(fromDb);
        Assert.False(fromDb!.IsVisible);
    }

    [Fact]
    public async Task CreateOrUpdateFolderAsync_ForcesSystemFoldersHidden_OnUpdate()
    {
        var folderName = ".thumbnails";
        var folderPath = Path.Combine(_testGalleryPath, folderName);
        Directory.CreateDirectory(folderPath);

        var folder = await _folderService.CreateOrUpdateFolderAsync(folderPath, folderName);
        Assert.False(folder.IsVisible);

        await _folderService.UpdateFolderVisibilityAsync(folder.Id, isVisible: true);

        var rescanned = await _folderService.CreateOrUpdateFolderAsync(folderPath, folderName);

        Assert.False(rescanned.IsVisible);
    }

    [Fact]
    public async Task CreateOrUpdateFolderAsync_HidesDotPrefixedFolders_OnInsert()
    {
        var folderName = ".hidden-album";
        var folderPath = Path.Combine(_testGalleryPath, folderName);
        Directory.CreateDirectory(folderPath);

        var folder = await _folderService.CreateOrUpdateFolderAsync(folderPath, folderName);

        Assert.False(folder.IsVisible);
    }

    public void Dispose()
    {
        _context.Dispose();
        if (Directory.Exists(_testGalleryPath))
        {
            Directory.Delete(_testGalleryPath, recursive: true);
        }
    }
}
