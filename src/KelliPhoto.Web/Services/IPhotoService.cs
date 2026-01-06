using KelliPhoto.Web.Data.Models;

namespace KelliPhoto.Web.Services;

public interface IPhotoService
{
    Task<List<Photo>> GetPhotosByFolderIdAsync(int folderId, int skip = 0, int take = 50);
    Task<Photo?> GetPhotoByIdAsync(int id);
    Task<int> GetPhotoCountByFolderIdAsync(int folderId);
    Task<Photo> CreateOrUpdatePhotoAsync(string filePath, int folderId, string filename);
    Task<List<Photo>> ScanPhotosInFolderAsync(int folderId, string folderPath);
}
