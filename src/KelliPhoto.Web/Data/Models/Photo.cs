using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KelliPhoto.Web.Data.Models;

public class Photo
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Filename { get; set; } = string.Empty;

    [Required]
    public int FolderId { get; set; }

    [ForeignKey(nameof(FolderId))]
    public Folder Folder { get; set; } = null!;

    [Required]
    [MaxLength(2000)]
    public string FilePath { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public DateTime? TakenAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Thumbnail> Thumbnails { get; set; } = new();
}
