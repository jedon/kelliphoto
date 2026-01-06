using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KelliPhoto.Web.Data.Models;

public class Folder
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    public int? ParentId { get; set; }

    [ForeignKey(nameof(ParentId))]
    public Folder? Parent { get; set; }

    public List<Folder> Children { get; set; } = new();

    [Required]
    [MaxLength(2000)]
    public string Path { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Photo> Photos { get; set; } = new();
}
