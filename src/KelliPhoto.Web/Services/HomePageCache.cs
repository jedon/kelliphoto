using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using KelliPhoto.Web.Data.Models;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace KelliPhoto.Web.Services;

public class HomePageCache : IHomePageCache
{
    private readonly IMemoryCache _cache;
    private readonly Serilog.ILogger _logger;
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public HomePageCache(IMemoryCache cache)
    {
        _cache = cache;
        _logger = Serilog.Log.ForContext<HomePageCache>();
    }

    public async Task<Folder?> GetHighlightsFolderAsync(Func<Task<Folder?>> factory)
    {
        var key = "home:highlights-folder";
        bool isMiss = false;

        var result = await _cache.GetOrCreateAsync(key, async entry =>
        {
            isMiss = true;
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            _keys.TryAdd(key, 0);
            _logger.Information("Cache miss for key {Key}. Fetching from factory.", key);
            return await factory();
        });

        if (!isMiss)
        {
            _logger.Information("Cache hit for key {Key}.", key);
        }

        return result;
    }

    public async Task<List<Photo>> GetFirstPagePhotosAsync(int folderId, int take, bool includeHidden, Func<Task<List<Photo>>> factory)
    {
        var key = $"home:photos:{folderId}:{take}:{includeHidden}";
        bool isMiss = false;

        var result = await _cache.GetOrCreateAsync(key, async entry =>
        {
            isMiss = true;
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            _keys.TryAdd(key, 0);
            _logger.Information("Cache miss for key {Key}. Fetching from factory.", key);
            return await factory();
        });

        if (!isMiss)
        {
            _logger.Information("Cache hit for key {Key}.", key);
        }

        return result ?? new List<Photo>();
    }

    public void Invalidate()
    {
        _logger.Information("Invalidating home page cache. Clearing {Count} keys.", _keys.Count);
        foreach (var key in _keys.Keys)
        {
            _cache.Remove(key);
        }
        _keys.Clear();
    }
}
