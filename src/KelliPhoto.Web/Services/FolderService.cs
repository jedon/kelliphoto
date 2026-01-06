using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace KelliPhoto.Web.Services;

public class FolderService : IFolderService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FolderService> _logger;

    public FolderService(ApplicationDbContext context, ILogger<FolderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Folder>> GetRootFoldersAsync()
    {
        return await _context.Folders
            .Where(f => f.ParentId == null)
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    public async Task<Folder?> GetFolderByIdAsync(int id)
    {
        return await _context.Folders
            .Include(f => f.Parent)
            .Include(f => f.Children)
            .Include(f => f.Photos)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<List<Folder>> GetChildFoldersAsync(int parentId)
    {
        return await _context.Folders
            .Where(f => f.ParentId == parentId)
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    public async Task<Folder> CreateOrUpdateFolderAsync(string path, string name, int? parentId = null)
    {
        var folder = await _context.Folders
            .FirstOrDefaultAsync(f => f.Path == path);

        if (folder == null)
        {
            folder = new Folder
            {
                Path = path,
                Name = name,
                ParentId = parentId,
                CreatedAt = DateTime.UtcNow
            };
            _context.Folders.Add(folder);
        }
        else
        {
            folder.Name = name;
            folder.ParentId = parentId;
        }

        await _context.SaveChangesAsync();
        return folder;
    }

    public async Task<List<Folder>> ScanFoldersAsync(string rootPath)
    {
        var folders = new List<Folder>();
        
        if (!Directory.Exists(rootPath))
        {
            _logger.LogWarning("Gallery path does not exist: {Path}", rootPath);
            return folders;
        }

        await ScanFoldersRecursiveAsync(rootPath, null, folders);
        return folders;
    }

    private async Task ScanFoldersRecursiveAsync(string currentPath, int? parentId, List<Folder> folders)
    {
        try
        {
            var folderName = Path.GetFileName(currentPath);
            if (string.IsNullOrEmpty(folderName))
            {
                folderName = currentPath;
            }

            var folder = await CreateOrUpdateFolderAsync(currentPath, folderName, parentId);
            folders.Add(folder);

            var subdirectories = Directory.GetDirectories(currentPath);
            foreach (var subdirectory in subdirectories)
            {
                await ScanFoldersRecursiveAsync(subdirectory, folder.Id, folders);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning folder: {Path}", currentPath);
        }
    }
}
