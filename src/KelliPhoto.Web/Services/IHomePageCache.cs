using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KelliPhoto.Web.Data.Models;

namespace KelliPhoto.Web.Services;

public interface IHomePageCache
{
    Task<Folder?> GetHighlightsFolderAsync(Func<Task<Folder?>> factory);
    Task<List<Photo>> GetFirstPagePhotosAsync(int folderId, int take, bool includeHidden, Func<Task<List<Photo>>> factory);
    void Invalidate();
}
