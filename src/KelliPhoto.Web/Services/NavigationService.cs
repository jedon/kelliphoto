using KelliPhoto.Web.Data.Models;

namespace KelliPhoto.Web.Services;

public interface INavigationService
{
    Task<string> GetFolderUrlAsync(string folderName);
    Task<int?> GetFolderIdAsync(string folderName);
}

public class NavigationService : INavigationService
{
    private readonly IFolderService _folderService;
    private readonly ILogger<NavigationService> _logger;
    private readonly Dictionary<string, int?> _folderCache = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public NavigationService(IFolderService folderService, ILogger<NavigationService> logger)
    {
        _folderService = folderService;
        _logger = logger;
    }

    public async Task<string> GetFolderUrlAsync(string folderName)
    {
        var folderId = await GetFolderIdAsync(folderName);
        if (folderId.HasValue)
        {
            return $"/gallery/{folderId.Value}";
        }
        return "/";
    }

    public async Task<int?> GetFolderIdAsync(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return null;
        }

        // Check cache first
        await _cacheLock.WaitAsync();
        try
        {
            if (_folderCache.TryGetValue(folderName, out var cachedId))
            {
                return cachedId;
            }
        }
        finally
        {
            _cacheLock.Release();
        }

        // Load from database - use case-insensitive search
        // For navigation, we allow hidden folders to be found (they might be intentionally hidden from browsing but still need nav links)
        var folder = await _folderService.GetFolderByNameAsync(folderName);
        var folderId = folder?.Id;

        if (folder == null)
        {
            _logger.LogWarning("Folder not found: '{FolderName}'. Make sure the folder exists and has been scanned. Checking top-level folders for similar names...", folderName);
            
            // Try to find similar folder names in top-level folders for debugging
            var topLevelFolders = await _folderService.GetTopLevelFoldersAsync(includeHidden: true);
            var similarFolders = topLevelFolders
                .Where(f => f.Name.Contains(folderName, StringComparison.OrdinalIgnoreCase) || 
                           folderName.Contains(f.Name, StringComparison.OrdinalIgnoreCase))
                .Select(f => $"'{f.Name}' (Id: {f.Id}, Visible: {f.IsVisible})")
                .ToList();
            
            if (similarFolders.Any())
            {
                _logger.LogWarning("Similar folder names found in top-level folders: {SimilarFolders}", string.Join(", ", similarFolders));
            }
            else
            {
                // Also check all folders if nothing found in top-level
                var allFolders = await _folderService.GetAllFoldersAsync();
                var allSimilar = allFolders
                    .Where(f => f.Name.Contains(folderName, StringComparison.OrdinalIgnoreCase) || 
                               folderName.Contains(f.Name, StringComparison.OrdinalIgnoreCase))
                    .Take(10)
                    .Select(f => $"'{f.Name}' (Id: {f.Id}, Visible: {f.IsVisible}, ParentId: {f.ParentId})")
                    .ToList();
                
                if (allSimilar.Any())
                {
                    _logger.LogWarning("Similar folder names found in all folders: {SimilarFolders}", string.Join(", ", allSimilar));
                }
            }
        }
        else
        {
            // Allow navigation to hidden folders (they might be intentionally hidden from browsing but still need nav links)
            _logger.LogDebug("Found folder: '{FolderName}' (Id: {FolderId}, Visible: {IsVisible})", folderName, folderId, folder.IsVisible);
        }

        // Cache the result (including null to avoid repeated lookups)
        await _cacheLock.WaitAsync();
        try
        {
            _folderCache[folderName] = folderId;
        }
        finally
        {
            _cacheLock.Release();
        }

        return folderId;
    }
}
