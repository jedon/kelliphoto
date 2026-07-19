using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KelliPhoto.Web.Data.Models;
using KelliPhoto.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KelliPhoto.Web.Tests;

public class HomePageCacheTests : IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly HomePageCache _cache;

    public HomePageCacheTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _cache = new HomePageCache(_memoryCache, NullLogger<HomePageCache>.Instance);
    }

    [Fact]
    public async Task GetHighlightsFolderAsync_SecondCallUsesCache()
    {
        // Arrange
        int factoryCalls = 0;
        var folder = new Folder { Id = 1, Name = "Highlights" };
        Func<Task<Folder?>> factory = () =>
        {
            factoryCalls++;
            return Task.FromResult<Folder?>(folder);
        };

        // Act
        var result1 = await _cache.GetHighlightsFolderAsync(factory);
        var result2 = await _cache.GetHighlightsFolderAsync(factory);

        // Assert
        Assert.Equal(1, factoryCalls);
        Assert.Same(folder, result1);
        Assert.Same(folder, result2);
    }

    [Fact]
    public async Task GetFirstPagePhotosAsync_SecondCallUsesCache()
    {
        // Arrange
        int factoryCalls = 0;
        var photos = new List<Photo> { new Photo { Id = 101, Filename = "photo1.jpg" } };
        Func<Task<List<Photo>>> factory = () =>
        {
            factoryCalls++;
            return Task.FromResult(photos);
        };

        // Act
        var result1 = await _cache.GetFirstPagePhotosAsync(1, 10, false, factory);
        var result2 = await _cache.GetFirstPagePhotosAsync(1, 10, false, factory);

        // Assert
        Assert.Equal(1, factoryCalls);
        Assert.NotSame(photos, result1);
        Assert.NotSame(result1, result2);
        Assert.Equal(photos, result1);
        Assert.Equal(photos, result2);
    }

    [Fact]
    public async Task Invalidate_ForcesHighlightsFolderFactoryToRunAgain()
    {
        // Arrange
        int factoryCalls = 0;
        var folder = new Folder { Id = 1, Name = "Highlights" };
        Func<Task<Folder?>> factory = () =>
        {
            factoryCalls++;
            return Task.FromResult<Folder?>(folder);
        };

        // Act
        await _cache.GetHighlightsFolderAsync(factory);
        _cache.Invalidate();
        await _cache.GetHighlightsFolderAsync(factory);

        // Assert
        Assert.Equal(2, factoryCalls);
    }

    [Fact]
    public async Task Invalidate_ForcesFirstPagePhotosFactoryToRunAgain()
    {
        // Arrange
        int factoryCalls = 0;
        var photos = new List<Photo> { new Photo { Id = 101, Filename = "photo1.jpg" } };
        Func<Task<List<Photo>>> factory = () =>
        {
            factoryCalls++;
            return Task.FromResult(photos);
        };

        // Act
        await _cache.GetFirstPagePhotosAsync(1, 10, false, factory);
        _cache.Invalidate();
        await _cache.GetFirstPagePhotosAsync(1, 10, false, factory);

        // Assert
        Assert.Equal(2, factoryCalls);
    }

    [Fact]
    public async Task GetFirstPagePhotosAsync_DifferentIncludeHiddenKeysAreSeparate()
    {
        // Arrange
        int falseCalls = 0;
        int trueCalls = 0;

        var falsePhotos = new List<Photo> { new Photo { Id = 101, Filename = "visible.jpg" } };
        var truePhotos = new List<Photo> { new Photo { Id = 101, Filename = "visible.jpg" }, new Photo { Id = 102, Filename = "hidden.jpg" } };

        Func<Task<List<Photo>>> falseFactory = () =>
        {
            falseCalls++;
            return Task.FromResult(falsePhotos);
        };

        Func<Task<List<Photo>>> trueFactory = () =>
        {
            trueCalls++;
            return Task.FromResult(truePhotos);
        };

        // Act & Assert
        // First call with includeHidden = false
        var res1 = await _cache.GetFirstPagePhotosAsync(1, 10, includeHidden: false, falseFactory);
        Assert.Equal(1, falseCalls);
        Assert.NotSame(falsePhotos, res1);
        Assert.Equal(falsePhotos, res1);

        // Second call with includeHidden = true - should NOT hit cache from the false call
        var res2 = await _cache.GetFirstPagePhotosAsync(1, 10, includeHidden: true, trueFactory);
        Assert.Equal(1, trueCalls);
        Assert.NotSame(truePhotos, res2);
        Assert.Equal(truePhotos, res2);

        // Third call with includeHidden = false - should hit cache
        var res3 = await _cache.GetFirstPagePhotosAsync(1, 10, includeHidden: false, falseFactory);
        Assert.Equal(1, falseCalls); // Still 1
        Assert.NotSame(falsePhotos, res3);
        Assert.Equal(falsePhotos, res3);

        // Fourth call with includeHidden = true - should hit cache
        var res4 = await _cache.GetFirstPagePhotosAsync(1, 10, includeHidden: true, trueFactory);
        Assert.Equal(1, trueCalls); // Still 1
        Assert.NotSame(truePhotos, res4);
        Assert.Equal(truePhotos, res4);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }
}
