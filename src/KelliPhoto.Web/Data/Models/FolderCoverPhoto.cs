using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KelliPhoto.Web.Data.Models;

/// <summary>
/// Up to four curated cover images for a folder tile (sort order 0–3).
/// </summary>
public class FolderCoverPhoto
{
    public int FolderId { get; set; }

    [ForeignKey(nameof(FolderId))]
    public Folder Folder { get; set; } = null!;

    public int PhotoId { get; set; }

    [ForeignKey(nameof(PhotoId))]
    public Photo Photo { get; set; } = null!;

    /// <summary>Display order on the folder card (0–3).</summary>
    public int SortOrder { get; set; }
}
