using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using KelliPhoto.Web.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KelliPhoto.Web.Tests;

public class TagServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ITagService _tagService;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public TagServiceTests()
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(_options);
        _context.Database.EnsureCreated();
        _tagService = new TagService(new TestDbContextFactory(_options));
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task EnsureTagAsync_CreatesTag_AndIsCaseInsensitiveUnique()
    {
        var first = await _tagService.EnsureTagAsync("  Monarch  ", "Butterflies");
        var second = await _tagService.EnsureTagAsync("monarch");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Monarch", first.Name);
        Assert.Equal("Butterflies", first.Group);
        Assert.Equal(1, await _context.Tags.CountAsync());
    }

    [Fact]
    public async Task AttachAndDetach_FolderTag_Works()
    {
        var folder = await SeedFolderAsync("Album");
        var tag = await _tagService.EnsureTagAsync("Family", "People");

        await _tagService.AttachToFolderAsync(folder.Id, tag.Id);
        var attached = await _tagService.GetTagsForFolderAsync(folder.Id);
        Assert.Single(attached);
        Assert.Equal("Family", attached[0].Name);

        await _tagService.DetachFromFolderAsync(folder.Id, tag.Id);
        Assert.Empty(await _tagService.GetTagsForFolderAsync(folder.Id));
    }

    [Fact]
    public async Task AttachAndDetach_PhotoTag_Works()
    {
        var folder = await SeedFolderAsync("Album");
        var photo = await SeedPhotoAsync(folder.Id, "a.jpg");
        var tag = await _tagService.EnsureTagAsync("Park", "Places");

        await _tagService.AttachToPhotoAsync(photo.Id, tag.Id);
        var attached = await _tagService.GetTagsForPhotoAsync(photo.Id);
        Assert.Single(attached);
        Assert.Equal("Park", attached[0].Name);

        await _tagService.DetachFromPhotoAsync(photo.Id, tag.Id);
        Assert.Empty(await _tagService.GetTagsForPhotoAsync(photo.Id));
    }

    [Fact]
    public async Task AutocompleteAsync_MatchesPrefix_CaseInsensitive()
    {
        await _tagService.EnsureTagAsync("Alice", "People");
        await _tagService.EnsureTagAsync("Alex", "People");
        await _tagService.EnsureTagAsync("Bob", "People");

        var results = await _tagService.AutocompleteAsync("al", take: 10);

        Assert.Equal(2, results.Count);
        Assert.All(results, t => Assert.StartsWith("Al", t.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BulkAttachToFoldersAsync_AttachesTagsToAllFolders()
    {
        var a = await SeedFolderAsync("A");
        var b = await SeedFolderAsync("B");

        await _tagService.BulkAttachToFoldersAsync([a.Id, b.Id], ["Wedding", "2024"]);

        var tagsA = (await _tagService.GetTagsForFolderAsync(a.Id)).Select(t => t.Name).OrderBy(n => n).ToList();
        var tagsB = (await _tagService.GetTagsForFolderAsync(b.Id)).Select(t => t.Name).OrderBy(n => n).ToList();

        Assert.Equal(["2024", "Wedding"], tagsA);
        Assert.Equal(["2024", "Wedding"], tagsB);
        Assert.Equal(2, await _context.Tags.CountAsync());
    }

    [Fact]
    public async Task BulkDetachFromFoldersAsync_RemovesTagsFromFolders()
    {
        var a = await SeedFolderAsync("A");
        var b = await SeedFolderAsync("B");
        await _tagService.BulkAttachToFoldersAsync([a.Id, b.Id], ["Keep", "Drop"]);

        await _tagService.BulkDetachFromFoldersAsync([a.Id, b.Id], ["Drop"]);

        Assert.Equal(["Keep"], (await _tagService.GetTagsForFolderAsync(a.Id)).Select(t => t.Name).ToList());
        Assert.Equal(["Keep"], (await _tagService.GetTagsForFolderAsync(b.Id)).Select(t => t.Name).ToList());
    }

    [Fact]
    public void ListSuggestedGroups_ReturnsPreferredGroups()
    {
        var groups = _tagService.ListSuggestedGroups();
        Assert.Equal(["People", "Butterflies", "Places", "Events"], groups);
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
        return folder;
    }

    private async Task<Photo> SeedPhotoAsync(int folderId, string filename)
    {
        var photo = new Photo
        {
            Filename = filename,
            FolderId = folderId,
            FilePath = $"{folderId}/{filename}",
            FileSize = 1,
            IsVisible = true
        };
        _context.Photos.Add(photo);
        await _context.SaveChangesAsync();
        return photo;
    }
}
