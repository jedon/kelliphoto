using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KelliPhoto.Web.Data.Models;

public class Thumbnail
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int PhotoId { get; set; }

    [ForeignKey(nameof(PhotoId))]
    public Photo Photo { get; set; } = null!;

    [Required]
    public int Size { get; set; }

    [Required]
    [MaxLength(2000)]
    public string FilePath { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
