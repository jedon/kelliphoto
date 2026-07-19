using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KelliPhoto.Web.Data.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace KelliPhoto.Web.Services;

public class HomePageCache : IHomePageCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<HomePageCache> _logger;
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public HomePageCache(IMemoryCache cache, ILogger<HomePageCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<Folder?> GetHighlightsFolderAsync(Func<Task<Folder?>> factory)
    {
        var key = "home:highlights-folder";
        bool isMiss = false;

        var result = await _cache.GetOrCreateAsync(key, async entry =>
        {
            isMiss = true;
            _keys.TryAdd(key, 0);
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            entry.RegisterPostEvictionCallback((evictedKey, value, reason, state) =>
            {
                if (evictedKey is string k)
                {
                    _keys.TryRemove(k, out _);
                }
            });
            _logger.LogDebug("Cache miss for key {Key}. Fetching from factory.", key);
            return await factory();
        });

        if (!isMiss)
        {
            _logger.LogDebug("Cache hit for key {Key}.", key);
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
            _keys.TryAdd(key, 0);
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            entry.RegisterPostEvictionCallback((evictedKey, value, reason, state) =>
            {
                if (evictedKey is string k)
                {
                    _keys.TryRemove(k, out _);
                }
            });
            _logger.LogDebug("Cache miss for key {Key}. Fetching from factory.", key);
            var photos = await factory();
            return photos?.ToList();
        });

        if (!isMiss)
        {
            _logger.LogDebug("Cache hit for key {Key}.", key);
        }

        return result?.ToList() ?? new List<Photo>();
    }

    public void Invalidate()
    {
        var keysToInvalidate = _keys.Keys.ToList();
        _logger.LogInformation("Invalidating home page cache. Clearing {Count} keys.", keysToInvalidate.Count);
        foreach (var key in keysToInvalidate)
        {
            if (_keys.TryRemove(key, out _))
            {
                _cache.Remove(key);
            }
        }
    }
}
