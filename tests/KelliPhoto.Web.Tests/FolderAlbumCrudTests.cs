using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using KelliPhoto.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace KelliPhoto.Web.Tests;

public class FolderAlbumCrudTests : IDisposable
{
    private readonly string _galleryRoot;
    private readonly ApplicationDbContext _context;
    private readonly IFolderService _folderService;
    private readonly IPathService _pathService;

    public FolderAlbumCrudTests()
    {
        _galleryRoot = Path.Combine(Path.GetTempPath(), "kelli-album-crud-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_galleryRoot);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GallerySettings:GalleryPath", _galleryRoot }
            })
            .Build();

        _pathService = new PathService(config);
        _folderService = new FolderService(
            new TestDbContextFactory(options),
            _pathService,
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<FolderService>());
    }

    [Fact]
    public async Task CreateAlbumAsync_CreatesDirectoryAndDbRowWithNextSortOrder()
    {
        var mount = await SeedMountRootAsync();
        var parentDir = Path.Combine(_galleryRoot, "Parent");
        Directory.CreateDirectory(parentDir);
        var parent = await AddFolderAsync("Parent", "Parent", mount.Id, sortOrder: 0);
        await AddFolderAsync("Existing", "Parent/Existing", parent.Id, sortOrder: 0);
        Directory.CreateDirectory(Path.Combine(parentDir, "Existing"));

        var created = await _folderService.CreateAlbumAsync(parent.Id, "  New Album  ");

        Assert.Equal("New Album", created.Name);
        Assert.Equal(parent.Id, created.ParentId);
        Assert.Equal(1, created.SortOrder);
        Assert.Equal(Path.Combine("Parent", "New Album"), created.Path.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(Directory.Exists(Path.Combine(parentDir, "New Album")));
    }

    [Fact]
    public async Task RenameAlbumAsync_RenamesDirectoryAndRewritesDescendantPaths()
    {
        var mount = await SeedMountRootAsync();
        var albumDir = Path.Combine(_galleryRoot, "OldName");
        var childDir = Path.Combine(albumDir, "Child");
        Directory.CreateDirectory(childDir);
        await File.WriteAllBytesAsync(Path.Combine(childDir, "pic.jpg"), new byte[] { 1, 2, 3 });

        var album = await AddFolderAsync("OldName", "OldName", mount.Id);
        var child = await AddFolderAsync("Child", "OldName/Child", album.Id);
        var photo = new Photo
        {
            Filename = "pic.jpg",
            FolderId = child.Id,
            FilePath = "OldName/Child/pic.jpg",
            FileSize = 3,
            IsVisible = true
        };
        _context.Photos.Add(photo);
        await _context.SaveChangesAsync();

        await _folderService.RenameAlbumAsync(album.Id, "NewName");

        Assert.False(Directory.Exists(albumDir));
        Assert.True(Directory.Exists(Path.Combine(_galleryRoot, "NewName", "Child")));

        var renamed = await _context.Folders.AsNoTracking().SingleAsync(f => f.Id == album.Id);
        var renamedChild = await _context.Folders.AsNoTracking().SingleAsync(f => f.Id == child.Id);
        var renamedPhoto = await _context.Photos.AsNoTracking().SingleAsync(p => p.Id == photo.Id);

        Assert.Equal("NewName", renamed.Name);
        Assert.Equal("NewName", NormalizeRel(renamed.Path));
        Assert.Equal(NormalizeRel("NewName/Child"), NormalizeRel(renamedChild.Path));
        Assert.Equal(NormalizeRel("NewName/Child/pic.jpg"), NormalizeRel(renamedPhoto.FilePath));
    }

    [Fact]
    public async Task DeleteAlbumRecursiveAsync_RemovesDiskAndDbSubtree()
    {
        var mount = await SeedMountRootAsync();
        var albumDir = Path.Combine(_galleryRoot, "ToDelete");
        var childDir = Path.Combine(albumDir, "Child");
        Directory.CreateDirectory(childDir);
        await File.WriteAllBytesAsync(Path.Combine(childDir, "pic.jpg"), new byte[] { 9 });

        var album = await AddFolderAsync("ToDelete", "ToDelete", mount.Id);
        var child = await AddFolderAsync("Child", "ToDelete/Child", album.Id);
        _context.Photos.Add(new Photo
        {
            Filename = "pic.jpg",
            FolderId = child.Id,
            FilePath = "ToDelete/Child/pic.jpg",
            FileSize = 1,
            IsVisible = true
        });
        await _context.SaveChangesAsync();

        await _folderService.DeleteAlbumRecursiveAsync(album.Id);

        Assert.False(Directory.Exists(albumDir));
        Assert.False(await _context.Folders.AnyAsync(f => f.Id == album.Id || f.Id == child.Id));
        Assert.False(await _context.Photos.AnyAsync());
    }

    [Fact]
    public async Task DeleteAlbumRecursiveAsync_ProtectedFolder_Throws()
    {
        var highlights = await AddFolderAsync("Home Page Highlights", "Home Page Highlights", parentId: null);
        Directory.CreateDirectory(Path.Combine(_galleryRoot, "Home Page Highlights"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _folderService.DeleteAlbumRecursiveAsync(highlights.Id));
        Assert.Contains("protected", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(await _context.Folders.AnyAsync(f => f.Id == highlights.Id));
    }

    [Fact]
    public async Task ReorderSiblingsAsync_SetsSortOrderToMatchIdList()
    {
        var mount = await SeedMountRootAsync();
        var a = await AddFolderAsync("A", "A", mount.Id, sortOrder: 0);
        var b = await AddFolderAsync("B", "B", mount.Id, sortOrder: 1);
        var c = await AddFolderAsync("C", "C", mount.Id, sortOrder: 2);

        await _folderService.ReorderSiblingsAsync(mount.Id, new[] { c.Id, a.Id, b.Id });

        var ordered = await _context.Folders.AsNoTracking()
            .Where(f => f.ParentId == mount.Id)
            .OrderBy(f => f.SortOrder)
            .Select(f => f.Id)
            .ToListAsync();
        Assert.Equal(new[] { c.Id, a.Id, b.Id }, ordered);
    }

    [Fact]
    public async Task SetFoldersVisibilityAsync_UpdatesMultipleFolders()
    {
        var mount = await SeedMountRootAsync();
        var a = await AddFolderAsync("A", "A", mount.Id);
        var b = await AddFolderAsync("B", "B", mount.Id);

        await _folderService.SetFoldersVisibilityAsync(new[] { a.Id, b.Id }, isVisible: false);

        Assert.False((await _context.Folders.AsNoTracking().SingleAsync(f => f.Id == a.Id)).IsVisible);
        Assert.False((await _context.Folders.AsNoTracking().SingleAsync(f => f.Id == b.Id)).IsVisible);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData(".")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAlbumAsync_RejectsMaliciousOrInvalidNames(string name)
    {
        var mount = await SeedMountRootAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => _folderService.CreateAlbumAsync(mount.Id, name));
    }

    [Fact]
    public void IsProtectedFolder_HomePageHighlights_IsProtected()
    {
        var folder = new Folder { Name = "Home Page Highlights", Path = "Home Page Highlights", ParentId = 5 };
        Assert.True(_folderService.IsProtectedFolder(folder));
    }

    [Fact]
    public void IsProtectedFolder_MountRoot_IsProtected()
    {
        var mount = new Folder { Name = "kelli.photo", Path = "", ParentId = null };
        Assert.True(_folderService.IsProtectedFolder(mount));
    }

    [Fact]
    public void IsProtectedFolder_OrdinaryTopLevelUserAlbum_IsNotProtected()
    {
        // User album under mount root (ParentId set) — not protected
        var album = new Folder { Name = "Weddings", Path = "Weddings", ParentId = 1 };
        Assert.False(_folderService.IsProtectedFolder(album));

        // Even a null-parent user album whose path is under the gallery (not the mount itself) is not protected
        var orphanTopLevel = new Folder { Name = "Family", Path = "Family", ParentId = null };
        Assert.False(_folderService.IsProtectedFolder(orphanTopLevel));
    }

    [Fact]
    public async Task GetAlbumSubtreeCountsAsync_CountsDescendantFoldersAndPhotos()
    {
        var mount = await SeedMountRootAsync();
        var album = await AddFolderAsync("Album", "Album", mount.Id);
        var child = await AddFolderAsync("Child", "Album/Child", album.Id);
        var grand = await AddFolderAsync("Grand", "Album/Child/Grand", child.Id);
        _context.Photos.AddRange(
            new Photo { Filename = "a.jpg", FolderId = album.Id, FilePath = "Album/a.jpg", FileSize = 1 },
            new Photo { Filename = "b.jpg", FolderId = child.Id, FilePath = "Album/Child/b.jpg", FileSize = 1 },
            new Photo { Filename = "c.jpg", FolderId = grand.Id, FilePath = "Album/Child/Grand/c.jpg", FileSize = 1 });
        await _context.SaveChangesAsync();

        var (childAlbumCount, photoCount) = await _folderService.GetAlbumSubtreeCountsAsync(album.Id);

        Assert.Equal(2, childAlbumCount);
        Assert.Equal(3, photoCount);
    }

    private async Task<Folder> SeedMountRootAsync()
    {
        // Mount root represents the gallery directory itself (empty relative path)
        return await AddFolderAsync("kelli.photo", "", parentId: null, sortOrder: 0);
    }

    private async Task<Folder> AddFolderAsync(string name, string path, int? parentId, int sortOrder = 0)
    {
        var folder = new Folder
        {
            Name = name,
            Path = path.Replace('/', Path.DirectorySeparatorChar),
            ParentId = parentId,
            SortOrder = sortOrder,
            IsVisible = true
        };
        _context.Folders.Add(folder);
        await _context.SaveChangesAsync();
        return folder;
    }

    private static string NormalizeRel(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

    public void Dispose()
    {
        _context.Dispose();
        try
        {
            if (Directory.Exists(_galleryRoot))
                Directory.Delete(_galleryRoot, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
