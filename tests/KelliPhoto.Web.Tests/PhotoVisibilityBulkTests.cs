using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using KelliPhoto.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace KelliPhoto.Web.Tests;

public class PhotoVisibilityBulkTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IPhotoService _photoService;

    public PhotoVisibilityBulkTests()
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

        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<PhotoService>();
        _photoService = new PhotoService(contextFactory, new PathService(config), logger);
    }

    [Fact]
    public async Task SetPhotosVisibilityAsync_UpdatesMultiplePhotos()
    {
        var folder = new Folder { Name = "BulkVis", Path = "BulkVis", IsVisible = true };
        _context.Folders.Add(folder);
        await _context.SaveChangesAsync();

        var a = await AddPhotoAsync("a.jpg", folder.Id, isVisible: true);
        var b = await AddPhotoAsync("b.jpg", folder.Id, isVisible: true);
        var c = await AddPhotoAsync("c.jpg", folder.Id, isVisible: true);

        await _photoService.SetPhotosVisibilityAsync(new[] { a.Id, b.Id }, isVisible: false);

        Assert.False((await _context.Photos.AsNoTracking().SingleAsync(p => p.Id == a.Id)).IsVisible);
        Assert.False((await _context.Photos.AsNoTracking().SingleAsync(p => p.Id == b.Id)).IsVisible);
        Assert.True((await _context.Photos.AsNoTracking().SingleAsync(p => p.Id == c.Id)).IsVisible);
    }

    private async Task<Photo> AddPhotoAsync(string filename, int folderId, bool isVisible)
    {
        var photo = new Photo
        {
            Filename = filename,
            FilePath = $"{folderId}/{filename}",
            FolderId = folderId,
            TakenAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsVisible = isVisible
        };
        _context.Photos.Add(photo);
        await _context.SaveChangesAsync();
        return photo;
    }

    public void Dispose() => _context.Dispose();
}
