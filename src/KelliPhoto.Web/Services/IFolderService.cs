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
    Task<IReadOnlyList<Photo>> GetFolderThumbnailPhotosAsync(int folderId, int maxCount = 4);
    Task SetFolderThumbnailAsync(int folderId, int photoId);
    Task<bool> IsPhotoValidForFolderCoverAsync(int folderId, int photoId);
    Task<IReadOnlyList<Photo>> GetPhotosForCoverPickerAsync(int folderId, int maxCount = 120);
    Task<IReadOnlyList<Photo>> GetFolderCoverPhotosAsync(int folderId);
    Task SetFolderCoverPhotosAsync(int folderId, IReadOnlyList<int> photoIds);
    Task ClearFolderCoverPhotosAsync(int folderId);
    Task<bool> FolderHasChildrenAsync(int folderId, bool includeHidden = true);
    Task<Folder?> GetFolderByNameAsync(string name, int? parentId = null);
    Task<List<Folder>> GetTopLevelFoldersAsync(bool includeHidden = false);
    Task UpdateFolderVisibilityAsync(int folderId, bool isVisible);
    Task UpdateFolderDescriptionAsync(int folderId, string? description);
    Task UpdateFolderSettingsAsync(int folderId, string name, int sortOrder, bool isVisible, string? description);
    Task<IReadOnlyList<Photo>> GetPhotosInFolderForCoverPickerAsync(int folderId, int maxCount = 120);
    Task<IReadOnlyList<Folder>> GetSiblingFoldersAsync(int folderId, bool includeHidden = true);
    Task<List<Folder>> GetAllFoldersAsync();
    Task<List<Folder>> GetBreadcrumbPathAsync(int folderId);

    Task<Folder> CreateAlbumAsync(int? parentFolderId, string name);
    Task RenameAlbumAsync(int folderId, string newName);
    Task DeleteAlbumRecursiveAsync(int folderId);
    Task ReorderSiblingsAsync(int? parentFolderId, IReadOnlyList<int> orderedFolderIds);
    Task SetFoldersVisibilityAsync(IReadOnlyList<int> folderIds, bool isVisible);
    Task<(int ChildAlbumCount, int PhotoCount)> GetAlbumSubtreeCountsAsync(int folderId);
    bool IsProtectedFolder(Folder folder);
}
