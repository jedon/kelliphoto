using KelliPhoto.Web.Data.Models;

namespace KelliPhoto.Web.Services;

public class PhotoExifUpdate
{
    public DateTime? DateTaken { get; set; }
    public string? CameraMake { get; set; }
    public string? CameraModel { get; set; }
    public string? Lens { get; set; }
    public string? FocalLength { get; set; }
    public string? Aperture { get; set; }
    public string? ShutterSpeed { get; set; }
    public int? Iso { get; set; }
    public double? GpsLatitude { get; set; }
    public double? GpsLongitude { get; set; }
    public string? Artist { get; set; }
    public string? Copyright { get; set; }
    public string? ImageDescription { get; set; }

    /// <summary>
    /// Optional advanced writable EXIF tags (tag name → value) merged into ExtraJson on disk.
    /// Only string-typed ExifTag names that ImageSharp exposes as string are applied.
    /// </summary>
    public Dictionary<string, string?>? ExtraTags { get; set; }
}

public interface IPhotoMetadataService
{
    Task<PhotoExif?> GetAsync(int photoId);
    Task RefreshFromFileAsync(int photoId);
    Task UpdateAsync(int photoId, PhotoExifUpdate dto);
}
