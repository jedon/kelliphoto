using KelliPhoto.Web.Data.Models;

namespace KelliPhoto.Web.Services;

public interface ITagService
{
    Task<Tag> EnsureTagAsync(string name, string? group = null);
    Task<List<Tag>> AutocompleteAsync(string prefix, int take = 20);
    Task<List<Tag>> GetTagsForFolderAsync(int folderId);
    Task<List<Tag>> GetTagsForPhotoAsync(int photoId);
    Task AttachToFolderAsync(int folderId, int tagId);
    Task DetachFromFolderAsync(int folderId, int tagId);
    Task AttachToPhotoAsync(int photoId, int tagId);
    Task DetachFromPhotoAsync(int photoId, int tagId);
    Task BulkAttachToFoldersAsync(IReadOnlyList<int> folderIds, IReadOnlyList<string> tagNames);
    Task BulkDetachFromFoldersAsync(IReadOnlyList<int> folderIds, IReadOnlyList<string> tagNames);
    IReadOnlyList<string> ListSuggestedGroups();
}
