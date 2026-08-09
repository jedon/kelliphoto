using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KelliPhoto.Web.Data.Models;

/// <summary>
/// 1:1 EXIF mirror for a <see cref="Photo"/>. Primary key is also the FK to Photos.
/// </summary>
public class PhotoExif
{
    [Key]
    [ForeignKey(nameof(Photo))]
    public int PhotoId { get; set; }

    public Photo Photo { get; set; } = null!;

    public DateTime? DateTaken { get; set; }

    [MaxLength(200)]
    public string? CameraMake { get; set; }

    [MaxLength(200)]
    public string? CameraModel { get; set; }

    [MaxLength(200)]
    public string? Lens { get; set; }

    [MaxLength(100)]
    public string? FocalLength { get; set; }

    [MaxLength(100)]
    public string? Aperture { get; set; }

    [MaxLength(100)]
    public string? ShutterSpeed { get; set; }

    public int? Iso { get; set; }

    public double? GpsLatitude { get; set; }

    public double? GpsLongitude { get; set; }

    [MaxLength(500)]
    public string? Artist { get; set; }

    [MaxLength(500)]
    public string? Copyright { get; set; }

    [MaxLength(2000)]
    public string? ImageDescription { get; set; }

    /// <summary>JSON object of remaining EXIF tag name → string value.</summary>
    public string? ExtraJson { get; set; }
}
