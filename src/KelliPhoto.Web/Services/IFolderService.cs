using KelliPhoto.Web.Data.Models;

namespace KelliPhoto.Web.Services;

public interface IFolderService
{
    Task<List<Folder>> GetRootFoldersAsync();
    Task<Folder?> GetFolderByIdAsync(int id);
    Task<List<Folder>> GetChildFoldersAsync(int parentId);
    Task<Folder> CreateOrUpdateFolderAsync(string path, string name, int? parentId = null);
    Task<List<Folder>> ScanFoldersAsync(string rootPath);
}
