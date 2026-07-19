using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using KelliPhoto.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace KelliPhoto.Web.Tests;

public class AdjacentPhotoTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IPhotoService _photoService;

    public AdjacentPhotoTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _contextFactory = new TestDbContextFactory(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GallerySettings:GalleryPath", Path.GetTempPath() }
            })
            .Build();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<PhotoService>();

        _photoService = new PhotoService(_contextFactory, new PathService(config), logger);
    }

    [Fact]
    public async Task GetAdjacentPhotoIdsAsync_MissingPhoto_ReturnsNull()
    {
        var result = await _photoService.GetAdjacentPhotoIdsAsync(999);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAdjacentPhotoIdsAsync_MiddlePhoto_ReturnsPrevAndNext()
    {
        var folder = await AddFolderAsync("Folder1");
        
        var photo3 = await AddPhotoAsync("photo3.jpg", folder.Id, takenAt: new DateTime(2026, 1, 1));
        var photo2 = await AddPhotoAsync("photo2.jpg", folder.Id, takenAt: new DateTime(2026, 1, 2));
        var photo1 = await AddPhotoAsync("photo1.jpg", folder.Id, takenAt: new DateTime(2026, 1, 3));

        var result = await _photoService.GetAdjacentPhotoIdsAsync(photo2.Id);

        Assert.NotNull(result);
        Assert.Equal(photo1.Id, result.PrevId);
        Assert.Equal(photo3.Id, result.NextId);
        Assert.Equal(2, result.Index);
        Assert.Equal(3, result.Total);
    }

    [Fact]
    public async Task GetAdjacentPhotoIdsAsync_Ends_ReturnsCorrectEnds()
    {
        var folder = await AddFolderAsync("Folder2");
        
        var photo3 = await AddPhotoAsync("photo3.jpg", folder.Id, takenAt: new DateTime(2026, 1, 1));
        var photo2 = await AddPhotoAsync("photo2.jpg", folder.Id, takenAt: new DateTime(2026, 1, 2));
        var photo1 = await AddPhotoAsync("photo1.jpg", folder.Id, takenAt: new DateTime(2026, 1, 3));

        var firstResult = await _photoService.GetAdjacentPhotoIdsAsync(photo1.Id);
        Assert.NotNull(firstResult);
        Assert.Null(firstResult.PrevId);
        Assert.Equal(photo2.Id, firstResult.NextId);
        Assert.Equal(1, firstResult.Index);
        Assert.Equal(3, firstResult.Total);

        var lastResult = await _photoService.GetAdjacentPhotoIdsAsync(photo3.Id);
        Assert.NotNull(lastResult);
        Assert.Equal(photo2.Id, lastResult.PrevId);
        Assert.Null(lastResult.NextId);
        Assert.Equal(3, lastResult.Index);
        Assert.Equal(3, lastResult.Total);
    }

    [Fact]
    public async Task GetAdjacentPhotoIdsAsync_SinglePhotoFolder_ReturnsBothNull()
    {
        var folder = await AddFolderAsync("Folder3");
        var photo = await AddPhotoAsync("photo.jpg", folder.Id, takenAt: new DateTime(2026, 1, 1));

        var result = await _photoService.GetAdjacentPhotoIdsAsync(photo.Id);

        Assert.NotNull(result);
        Assert.Null(result.PrevId);
        Assert.Null(result.NextId);
        Assert.Equal(1, result.Index);
        Assert.Equal(1, result.Total);
    }

    [Fact]
    public async Task GetAdjacentPhotoIdsAsync_HiddenFiltering_FiltersHiddenCorrectly()
    {
        var folder = await AddFolderAsync("Folder4");
        
        var photo3 = await AddPhotoAsync("photo3.jpg", folder.Id, takenAt: new DateTime(2026, 1, 1));
        var photo2 = await AddPhotoAsync("photo2.jpg", folder.Id, takenAt: new DateTime(2026, 1, 2), isVisible: false);
        var photo1 = await AddPhotoAsync("photo1.jpg", folder.Id, takenAt: new DateTime(2026, 1, 3));

        var publicResult1 = await _photoService.GetAdjacentPhotoIdsAsync(photo1.Id, includeHidden: false);
        Assert.NotNull(publicResult1);
        Assert.Null(publicResult1.PrevId);
        Assert.Equal(photo3.Id, publicResult1.NextId);
        Assert.Equal(1, publicResult1.Index);
        Assert.Equal(2, publicResult1.Total);

        var publicResult2 = await _photoService.GetAdjacentPhotoIdsAsync(photo2.Id, includeHidden: false);
        Assert.Null(publicResult2);

        var adminResult2 = await _photoService.GetAdjacentPhotoIdsAsync(photo2.Id, includeHidden: true);
        Assert.NotNull(adminResult2);
        Assert.Equal(photo1.Id, adminResult2.PrevId);
        Assert.Equal(photo3.Id, adminResult2.NextId);
        Assert.Equal(2, adminResult2.Index);
        Assert.Equal(3, adminResult2.Total);
    }

    private async Task<Folder> AddFolderAsync(string name)
    {
        var folder = new Folder
        {
            Name = name,
            Path = name,
            IsVisible = true
        };
        _context.Folders.Add(folder);
        await _context.SaveChangesAsync();
        return folder;
    }

    private async Task<Photo> AddPhotoAsync(string filename, int folderId, DateTime takenAt, bool isVisible = true)
    {
        var photo = new Photo
        {
            Filename = filename,
            FilePath = $"{folderId}/{filename}",
            FolderId = folderId,
            TakenAt = takenAt,
            CreatedAt = takenAt,
            IsVisible = isVisible
        };
        _context.Photos.Add(photo);
        await _context.SaveChangesAsync();
        return photo;
    }

    public void Dispose() => _context.Dispose();
}
