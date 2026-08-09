using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using KelliPhoto.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace KelliPhoto.Web.Tests;

public class PhotoMetadataServiceTests : IDisposable
{
    private readonly string _galleryRoot;
    private readonly ApplicationDbContext _context;
    private readonly IPhotoMetadataService _service;
    private readonly IPathService _pathService;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public PhotoMetadataServiceTests()
    {
        _galleryRoot = Path.Combine(Path.GetTempPath(), "kelli-exif-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_galleryRoot);

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(_options);
        _context.Database.EnsureCreated();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GallerySettings:GalleryPath"] = _galleryRoot
            })
            .Build();

        _pathService = new PathService(config);
        _service = new PhotoMetadataService(
            new TestDbContextFactory(_options),
            _pathService,
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<PhotoMetadataService>());
    }

    public void Dispose()
    {
        _context.Dispose();
        if (Directory.Exists(_galleryRoot))
            Directory.Delete(_galleryRoot, true);
    }

    [Fact]
    public async Task RefreshFromFileAsync_ReadsDateTakenAndArtistIntoMirror()
    {
        var dateTaken = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc);
        var photo = await SeedPhotoWithJpegAsync("album", "shot.jpg", exif: profile =>
        {
            profile.SetValue(ExifTag.DateTimeOriginal, "2024:06:15 14:30:00");
            profile.SetValue(ExifTag.Artist, "Kelli");
        });

        await _service.RefreshFromFileAsync(photo.Id);

        var mirror = await _service.GetAsync(photo.Id);
        Assert.NotNull(mirror);
        Assert.Equal("Kelli", mirror!.Artist);
        Assert.NotNull(mirror.DateTaken);
        Assert.Equal(dateTaken, DateTime.SpecifyKind(mirror.DateTaken!.Value, DateTimeKind.Utc));
    }

    [Fact]
    public async Task UpdateAsync_WritesExifToFile_ThenRefreshesMirror_AndSyncsTakenAt()
    {
        var photo = await SeedPhotoWithJpegAsync("album", "edit.jpg", exif: profile =>
        {
            profile.SetValue(ExifTag.DateTimeOriginal, "2020:01:01 00:00:00");
            profile.SetValue(ExifTag.Artist, "Old Artist");
        });

        await _service.RefreshFromFileAsync(photo.Id);

        var newTaken = new DateTime(2025, 3, 10, 9, 15, 0, DateTimeKind.Utc);
        await _service.UpdateAsync(photo.Id, new PhotoExifUpdate
        {
            DateTaken = newTaken,
            Artist = "New Artist"
        });

        var mirror = await _service.GetAsync(photo.Id);
        Assert.NotNull(mirror);
        Assert.Equal("New Artist", mirror!.Artist);
        Assert.NotNull(mirror.DateTaken);
        Assert.Equal(newTaken, DateTime.SpecifyKind(mirror.DateTaken!.Value, DateTimeKind.Utc));

        await using var fresh = new ApplicationDbContext(_options);
        var updatedPhoto = await fresh.Photos.SingleAsync(p => p.Id == photo.Id);
        Assert.Equal(newTaken, DateTime.SpecifyKind(updatedPhoto.TakenAt!.Value, DateTimeKind.Utc));

        var fullPath = _pathService.ResolveExistingPhotoFilePath(photo.FilePath);
        Assert.NotNull(fullPath);
        using var image = await Image.LoadAsync(fullPath!);
        Assert.NotNull(image.Metadata.ExifProfile);
        Assert.True(image.Metadata.ExifProfile!.TryGetValue(ExifTag.Artist, out var artist));
        Assert.Equal("New Artist", artist.Value);
        Assert.True(image.Metadata.ExifProfile.TryGetValue(ExifTag.DateTimeOriginal, out var dto));
        Assert.Equal("2025:03:10 09:15:00", dto.Value);
    }

    [Fact]
    public async Task RefreshFromFileAsync_UnsupportedFormat_ThrowsClearMessage()
    {
        var folder = await SeedFolderAsync("album");
        var relative = Path.Combine("album", "note.txt");
        var full = Path.Combine(_galleryRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, "not an image");

        var photo = new Photo
        {
            Filename = "note.txt",
            FolderId = folder.Id,
            FilePath = relative.Replace('\\', '/'),
            FileSize = 11,
            IsVisible = true
        };
        _context.Photos.Add(photo);
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RefreshFromFileAsync(photo.Id));
        Assert.Contains("Unsupported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAsync_Jpeg_RewritesWithHighQualityEncoder()
    {
        var photo = await SeedPhotoWithJpegAsync("album", "hq.jpg", exif: profile =>
        {
            profile.SetValue(ExifTag.Artist, "Before");
        });

        var fullPath = _pathService.ResolveExistingPhotoFilePath(photo.FilePath)!;
        var sizeBefore = new FileInfo(fullPath).Length;

        await _service.UpdateAsync(photo.Id, new PhotoExifUpdate { Artist = "After" });

        var sizeAfter = new FileInfo(fullPath).Length;
        Assert.True(sizeAfter > 0);

        // Quality 95 rewrite should not collapse a tiny JPEG to a tiny default-quality blob.
        // With Q=95 on a 32x24 image the file stays in a similar size band (not ~half).
        Assert.True(sizeAfter >= sizeBefore / 2,
            $"Expected high-quality JPEG rewrite; before={sizeBefore}, after={sizeAfter}");

        using var image = await Image.LoadAsync(fullPath);
        Assert.True(image.Metadata.ExifProfile!.TryGetValue(ExifTag.Artist, out var artist));
        Assert.Equal("After", artist.Value);
    }

    [Fact]
    public async Task UpdateAsync_Gif_ThrowsUnsupportedWriteMessage()
    {
        var folder = await SeedFolderAsync("album");
        var relative = Path.Combine("album", "anim.gif");
        var full = Path.Combine(_galleryRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        using (var image = new Image<Rgba32>(16, 16))
        {
            await image.SaveAsGifAsync(full);
        }

        var photo = new Photo
        {
            Filename = "anim.gif",
            FolderId = folder.Id,
            FilePath = relative.Replace('\\', '/'),
            FileSize = new FileInfo(full).Length,
            IsVisible = true
        };
        _context.Photos.Add(photo);
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateAsync(photo.Id, new PhotoExifUpdate { Artist = "Nope" }));
        Assert.Contains("Unsupported image format for EXIF write", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GIF", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Folder> SeedFolderAsync(string name)
    {
        var folder = new Folder
        {
            Name = name,
            Path = name,
            IsVisible = true
        };
        _context.Folders.Add(folder);
        await _context.SaveChangesAsync();
        Directory.CreateDirectory(Path.Combine(_galleryRoot, name));
        return folder;
    }

    private async Task<Photo> SeedPhotoWithJpegAsync(
        string folderName,
        string filename,
        Action<ExifProfile>? exif = null)
    {
        var folder = await SeedFolderAsync(folderName);
        var relative = Path.Combine(folderName, filename);
        var full = Path.Combine(_galleryRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        using (var image = new Image<Rgba32>(32, 24))
        {
            var profile = new ExifProfile();
            exif?.Invoke(profile);
            image.Metadata.ExifProfile = profile;
            await image.SaveAsJpegAsync(full, new JpegEncoder { Quality = 90 });
        }

        var photo = new Photo
        {
            Filename = filename,
            FolderId = folder.Id,
            FilePath = relative.Replace('\\', '/'),
            FileSize = new FileInfo(full).Length,
            IsVisible = true
        };
        _context.Photos.Add(photo);
        await _context.SaveChangesAsync();
        return photo;
    }
}
