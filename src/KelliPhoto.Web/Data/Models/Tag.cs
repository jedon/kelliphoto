using System.ComponentModel.DataAnnotations;

namespace KelliPhoto.Web.Data.Models;

public class Tag
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = "";

    /// <summary>
    /// Lowercased, trimmed form of <see cref="Name"/> for case-insensitive uniqueness
    /// across PostgreSQL and EF InMemory providers.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string NameNormalized { get; set; } = "";

    [MaxLength(100)]
    public string? Group { get; set; }

    public List<FolderTag> FolderTags { get; set; } = new();
    public List<PhotoTag> PhotoTags { get; set; } = new();
}
