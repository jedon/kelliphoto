using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using KelliPhoto.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace KelliPhoto.Web.Tests;

public class FolderSortOrderTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IFolderService _folderService;

    public FolderSortOrderTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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

        _folderService = new FolderService(
            contextFactory,
            new PathService(config),
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<FolderService>());
    }

    [Fact]
    public async Task UpdateFolderSettings_ReordersSiblings()
    {
        var parent = await AddFolderAsync("Parent");
        var a = await AddFolderAsync("Alpha", parent.Id, sortOrder: 0);
        var b = await AddFolderAsync("Beta", parent.Id, sortOrder: 1);
        var c = await AddFolderAsync("Charlie", parent.Id, sortOrder: 2);

        await _folderService.UpdateFolderSettingsAsync(c.Id, "Charlie", sortOrder: 0, isVisible: true, description: null);

        var children = await _folderService.GetChildFoldersAsync(parent.Id, includeHidden: true);
        Assert.Equal(new[] { c.Id, a.Id, b.Id }, children.Select(f => f.Id));
    }

    private async Task<Folder> AddFolderAsync(string name, int? parentId = null, int sortOrder = 0)
    {
        var folder = new Folder
        {
            Name = name,
            Path = parentId.HasValue ? $"p{parentId}/{name}" : name,
            ParentId = parentId,
            SortOrder = sortOrder,
            IsVisible = true
        };
        _context.Folders.Add(folder);
        await _context.SaveChangesAsync();
        return folder;
    }

    public void Dispose() => _context.Dispose();
}
