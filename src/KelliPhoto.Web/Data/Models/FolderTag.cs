using System.ComponentModel.DataAnnotations.Schema;

namespace KelliPhoto.Web.Data.Models;

public class FolderTag
{
    public int FolderId { get; set; }

    [ForeignKey(nameof(FolderId))]
    public Folder Folder { get; set; } = null!;

    public int TagId { get; set; }

    [ForeignKey(nameof(TagId))]
    public Tag Tag { get; set; } = null!;
}
