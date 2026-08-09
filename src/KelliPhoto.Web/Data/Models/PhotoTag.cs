using System.ComponentModel.DataAnnotations.Schema;

namespace KelliPhoto.Web.Data.Models;

public class PhotoTag
{
    public int PhotoId { get; set; }

    [ForeignKey(nameof(PhotoId))]
    public Photo Photo { get; set; } = null!;

    public int TagId { get; set; }

    [ForeignKey(nameof(TagId))]
    public Tag Tag { get; set; } = null!;
}
