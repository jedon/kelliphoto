using KelliPhoto.Web.Data.Models;

namespace KelliPhoto.Web.Services;

public record AdjacentPhotos(int? PrevId, int? NextId, int Index, int Total);

public interface IPhotoService
{
    Task<List<Photo>> GetPhotosByFolderIdAsync(int folderId, int skip = 0, int take = 50, bool includeHidden = false);
    Task<Photo?> GetPhotoByIdAsync(int id);
    Task<bool> CanPublicViewPhotoAsync(int photoId);
    Task<bool> CanPublicViewFolderAsync(int folderId);
    Task<int> GetPhotoCountByFolderIdAsync(int folderId, bool includeHidden = false);
    Task<Photo> CreateOrUpdatePhotoAsync(string filePath, int folderId, string filename);
    Task<List<Photo>> ScanPhotosInFolderAsync(int folderId, string folderPath, IScanProgressService? progressService = null);
    Task<List<Photo>> ScanPhotosInFolderBatchedAsync(int folderId, string folderPath, IScanProgressService? progressService = null, int batchSize = 50);
    Task<bool> NeedsFolderScanAsync(int folderId, string folderPath);
    Task UpdatePhotoVisibilityAsync(int photoId, bool isVisible);
    Task SetPhotosVisibilityAsync(IReadOnlyList<int> photoIds, bool isVisible);
    Task UpdatePhotoDisplayNameAsync(int photoId, string? displayName);
    Task UpdatePhotoDescriptionAsync(int photoId, string? description);
    Task<List<Photo>> GetAllPhotosByFolderIdAsync(int folderId);
    Task<AdjacentPhotos?> GetAdjacentPhotoIdsAsync(int photoId, bool includeHidden = false);
}
