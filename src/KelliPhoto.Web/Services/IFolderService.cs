using KelliPhoto.Web.Data.Models;

namespace KelliPhoto.Web.Services;

public interface IFolderService
{
    Task<List<Folder>> GetRootFoldersAsync(bool includeHidden = false);
    Task<Folder?> GetFolderByIdAsync(int id);
    Task<List<Folder>> GetChildFoldersAsync(int parentId, bool includeHidden = false);
    Task<Folder> CreateOrUpdateFolderAsync(string path, string name, int? parentId = null);
    Task<List<Folder>> ScanFoldersAsync(string rootPath);
    Task<Photo?> GetFolderThumbnailAsync(int folderId);
    Task SetFolderThumbnailAsync(int folderId, int photoId);
    Task<Folder?> GetFolderByNameAsync(string name, int? parentId = null);
    Task<List<Folder>> GetTopLevelFoldersAsync(bool includeHidden = false);
    Task UpdateFolderVisibilityAsync(int folderId, bool isVisible);
    Task UpdateFolderDescriptionAsync(int folderId, string? description);
    Task<List<Folder>> GetAllFoldersAsync();
    Task<List<Folder>> GetBreadcrumbPathAsync(int folderId);
}
