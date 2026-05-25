using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using KelliPhoto.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace KelliPhoto.Web.Tests;

public class FolderThumbnailTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IFolderService _folderService;

    public FolderThumbnailTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        var contextFactory = new TestDbContextFactory(options);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GallerySettings:GalleryPath", Path.GetTempPath() }
            })
            .Build();
        var pathService = new PathService(config);
        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<FolderService>();
        _folderService = new FolderService(contextFactory, pathService, logger);
    }

    [Fact]
    public async Task GetFolderThumbnailPhotos_UsesExplicitThumbnail_WhenSet()
    {
        var album = await AddFolderAsync("Album");
        var chosen = await AddPhotoAsync(album.Id, "chosen.jpg", DateTime.UtcNow);
        await AddPhotoAsync(album.Id, "other.jpg", DateTime.UtcNow.AddMinutes(1));

        album.ThumbnailPhotoId = chosen.Id;
        await _context.SaveChangesAsync();

        var thumbnails = await _folderService.GetFolderThumbnailPhotosAsync(album.Id);

        Assert.Single(thumbnails);
        Assert.Equal(chosen.Id, thumbnails[0].Id);
    }

    [Fact]
    public async Task GetFolderThumbnailPhotos_ReturnsChildCovers_WhenAlbumHasChildGalleries()
    {
        var album = await AddFolderAsync("Album");
        var childA = await AddFolderAsync("ChildA", album.Id);
        var childB = await AddFolderAsync("ChildB", album.Id);
        var childC = await AddFolderAsync("ChildC", album.Id);
        var childD = await AddFolderAsync("ChildD", album.Id);
        var childE = await AddFolderAsync("ChildE", album.Id);

        var photoA = await AddPhotoAsync(childA.Id, "a.jpg", DateTime.UtcNow);
        var photoB = await AddPhotoAsync(childB.Id, "b.jpg", DateTime.UtcNow);
        var photoC = await AddPhotoAsync(childC.Id, "c.jpg", DateTime.UtcNow);
        var photoD = await AddPhotoAsync(childD.Id, "d.jpg", DateTime.UtcNow);
        await AddPhotoAsync(childE.Id, "e.jpg", DateTime.UtcNow);

        var thumbnails = await _folderService.GetFolderThumbnailPhotosAsync(album.Id);

        Assert.Equal(4, thumbnails.Count);
        Assert.Equal(new[] { photoA.Id, photoB.Id, photoC.Id, photoD.Id }, thumbnails.Select(p => p.Id));
    }

    [Fact]
    public async Task GetFolderThumbnailPhotos_FallsBackToOwnPhotos_WhenNoChildren()
    {
        var album = await AddFolderAsync("LeafAlbum");
        var photo1 = await AddPhotoAsync(album.Id, "one.jpg", DateTime.UtcNow);
        var photo2 = await AddPhotoAsync(album.Id, "two.jpg", DateTime.UtcNow.AddMinutes(1));

        var thumbnails = await _folderService.GetFolderThumbnailPhotosAsync(album.Id);

        Assert.Single(thumbnails);
        Assert.Equal(photo1.Id, thumbnails[0].Id);
    }

    [Fact]
    public async Task SetFolderCoverPhotos_UsesCuratedCoversUpToFour()
    {
        var album = await AddFolderAsync("Album");
        var p1 = await AddPhotoAsync(album.Id, "one.jpg", DateTime.UtcNow);
        var p2 = await AddPhotoAsync(album.Id, "two.jpg", DateTime.UtcNow.AddMinutes(1));
        var p3 = await AddPhotoAsync(album.Id, "three.jpg", DateTime.UtcNow.AddMinutes(2));

        await _folderService.SetFolderCoverPhotosAsync(album.Id, new[] { p2.Id, p1.Id, p3.Id });

        var thumbnails = await _folderService.GetFolderThumbnailPhotosAsync(album.Id);

        Assert.Equal(3, thumbnails.Count);
        Assert.Equal(new[] { p2.Id, p1.Id, p3.Id }, thumbnails.Select(p => p.Id));
    }

    [Fact]
    public async Task GetFolderThumbnailAsync_ReturnsFirstFromPhotoList()
    {
        var album = await AddFolderAsync("Album");
        var child = await AddFolderAsync("Child", album.Id);
        var photo = await AddPhotoAsync(child.Id, "cover.jpg", DateTime.UtcNow);

        var thumbnail = await _folderService.GetFolderThumbnailAsync(album.Id);

        Assert.NotNull(thumbnail);
        Assert.Equal(photo.Id, thumbnail!.Id);
    }

    [Fact]
    public async Task SetFolderCoverPhotos_AllowsPhotoFromChildFolder()
    {
        var album = await AddFolderAsync("Album");
        var child = await AddFolderAsync("Child", album.Id);
        var childPhoto = await AddPhotoAsync(child.Id, "child.jpg", DateTime.UtcNow);

        await _folderService.SetFolderCoverPhotosAsync(album.Id, new[] { childPhoto.Id });

        var covers = await _folderService.GetFolderCoverPhotosAsync(album.Id);
        Assert.Single(covers);
        Assert.Equal(childPhoto.Id, covers[0].Id);
    }

    [Fact]
    public async Task SetFolderCoverPhotos_RejectsPhotoFromUnrelatedFolder()
    {
        var album = await AddFolderAsync("Album");
        var other = await AddFolderAsync("Other");
        var otherPhoto = await AddPhotoAsync(other.Id, "other.jpg", DateTime.UtcNow);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _folderService.SetFolderCoverPhotosAsync(album.Id, new[] { otherPhoto.Id }));
    }

    [Fact]
    public async Task GetPhotosForCoverPicker_IncludesChildFolderPhotos()
    {
        var album = await AddFolderAsync("Album");
        var childB = await AddFolderAsync("Beta", album.Id);
        var childA = await AddFolderAsync("Alpha", album.Id);
        var albumPhoto = await AddPhotoAsync(album.Id, "album.jpg", DateTime.UtcNow);
        var alphaPhoto = await AddPhotoAsync(childA.Id, "alpha.jpg", DateTime.UtcNow);
        var betaPhoto = await AddPhotoAsync(childB.Id, "beta.jpg", DateTime.UtcNow);

        var pickerPhotos = await _folderService.GetPhotosForCoverPickerAsync(album.Id);

        Assert.Equal(3, pickerPhotos.Count);
        Assert.Equal(albumPhoto.Id, pickerPhotos[0].Id);
        Assert.Equal(alphaPhoto.Id, pickerPhotos[1].Id);
        Assert.Equal(betaPhoto.Id, pickerPhotos[2].Id);
    }

    private async Task<Folder> AddFolderAsync(string name, int? parentId = null)
    {
        var folder = new Folder
        {
            Name = name,
            Path = parentId.HasValue ? $"parent{parentId}/{name}" : name,
            ParentId = parentId,
            IsVisible = true
        };
        _context.Folders.Add(folder);
        await _context.SaveChangesAsync();
        return folder;
    }

    private async Task<Photo> AddPhotoAsync(int folderId, string filename, DateTime takenAt)
    {
        var photo = new Photo
        {
            Filename = filename,
            FolderId = folderId,
            FilePath = $"photos/{folderId}/{filename}",
            FileSize = 1024,
            TakenAt = takenAt
        };
        _context.Photos.Add(photo);
        await _context.SaveChangesAsync();
        return photo;
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
