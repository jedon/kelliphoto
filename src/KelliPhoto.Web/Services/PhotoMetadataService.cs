using System.Globalization;
using System.Text.Json;
using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace KelliPhoto.Web.Services;

public class PhotoMetadataService : IPhotoMetadataService
{
    private static readonly HashSet<string> KnownTagNames = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(ExifTag.DateTimeOriginal),
        nameof(ExifTag.DateTime),
        nameof(ExifTag.Make),
        nameof(ExifTag.Model),
        nameof(ExifTag.LensModel),
        nameof(ExifTag.LensMake),
        nameof(ExifTag.FocalLength),
        nameof(ExifTag.FNumber),
        nameof(ExifTag.ExposureTime),
        nameof(ExifTag.ISOSpeedRatings),
        nameof(ExifTag.ISOSpeed),
        nameof(ExifTag.GPSLatitude),
        nameof(ExifTag.GPSLatitudeRef),
        nameof(ExifTag.GPSLongitude),
        nameof(ExifTag.GPSLongitudeRef),
        nameof(ExifTag.Artist),
        nameof(ExifTag.Copyright),
        nameof(ExifTag.ImageDescription)
    };

    private static readonly HashSet<string> WritableStringExtraTags = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(ExifTag.Software),
        nameof(ExifTag.LensMake),
        nameof(ExifTag.LensSerialNumber),
        nameof(ExifTag.GPSMapDatum),
        nameof(ExifTag.DateTimeDigitized)
    };

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IPathService _pathService;
    private readonly ILogger<PhotoMetadataService> _logger;
    private readonly IHomePageCache? _homePageCache;

    public PhotoMetadataService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IPathService pathService,
        ILogger<PhotoMetadataService> logger,
        IHomePageCache? homePageCache = null)
    {
        _contextFactory = contextFactory;
        _pathService = pathService;
        _logger = logger;
        _homePageCache = homePageCache;
    }

    public async Task<PhotoExif?> GetAsync(int photoId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PhotoExifs.AsNoTracking()
            .FirstOrDefaultAsync(e => e.PhotoId == photoId);
    }

    public async Task RefreshFromFileAsync(int photoId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var photo = await context.Photos.FirstOrDefaultAsync(p => p.Id == photoId)
            ?? throw new InvalidOperationException($"Photo {photoId} was not found.");

        var fullPath = ResolvePhotoPath(photo.FilePath);
        var parsed = ReadExifFromFile(fullPath);

        var mirror = await context.PhotoExifs.FirstOrDefaultAsync(e => e.PhotoId == photoId);
        if (mirror is null)
        {
            mirror = new PhotoExif { PhotoId = photoId };
            context.PhotoExifs.Add(mirror);
        }

        ApplyParsedToMirror(mirror, parsed);

        if (parsed.DateTaken.HasValue)
            photo.TakenAt = EnsureUtc(parsed.DateTaken.Value);

        await context.SaveChangesAsync();
        _homePageCache?.Invalidate();
    }

    public async Task UpdateAsync(int photoId, PhotoExifUpdate dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var photo = await context.Photos.FirstOrDefaultAsync(p => p.Id == photoId)
            ?? throw new InvalidOperationException($"Photo {photoId} was not found.");

        var fullPath = ResolvePhotoPath(photo.FilePath);

        // Write EXIF to disk first. On failure do not touch DB.
        try
        {
            WriteExifToFile(fullPath, dto);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Failed to write EXIF metadata to image file '{fullPath}'. Database was not updated.", ex);
        }

        // Refresh mirror from the written file so DB matches disk.
        var parsed = ReadExifFromFile(fullPath);
        var mirror = await context.PhotoExifs.FirstOrDefaultAsync(e => e.PhotoId == photoId);
        if (mirror is null)
        {
            mirror = new PhotoExif { PhotoId = photoId };
            context.PhotoExifs.Add(mirror);
        }

        ApplyParsedToMirror(mirror, parsed);

        if (dto.DateTaken.HasValue || parsed.DateTaken.HasValue)
        {
            var taken = dto.DateTaken ?? parsed.DateTaken;
            if (taken.HasValue)
                photo.TakenAt = EnsureUtc(taken.Value);
        }

        await context.SaveChangesAsync();
        _homePageCache?.Invalidate();
    }

    private string ResolvePhotoPath(string storedPath)
    {
        var fullPath = _pathService.ResolveExistingPhotoFilePath(storedPath);
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            throw new InvalidOperationException($"Photo file was not found for path '{storedPath}'.");
        return fullPath;
    }

    private ParsedExif ReadExifFromFile(string fullPath)
    {
        Image image;
        try
        {
            image = Image.Load(fullPath);
        }
        catch (UnknownImageFormatException ex)
        {
            throw new InvalidOperationException(
                $"Unsupported image format for EXIF read: '{Path.GetFileName(fullPath)}'.", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Failed to load image for EXIF read: '{Path.GetFileName(fullPath)}'.", ex);
        }

        using (image)
        {
            var profile = image.Metadata.ExifProfile;
            if (profile is null)
                return new ParsedExif();

            return ParseProfile(profile);
        }
    }

    private static void WriteExifToFile(string fullPath, PhotoExifUpdate dto)
    {
        Image image;
        try
        {
            image = Image.Load(fullPath);
        }
        catch (UnknownImageFormatException ex)
        {
            throw new InvalidOperationException(
                $"Unsupported image format for EXIF write: '{Path.GetFileName(fullPath)}'.", ex);
        }

        using (image)
        {
            var profile = image.Metadata.ExifProfile ?? new ExifProfile();

            if (dto.DateTaken.HasValue)
            {
                var formatted = FormatExifDate(EnsureUtc(dto.DateTaken.Value));
                profile.SetValue(ExifTag.DateTimeOriginal, formatted);
                profile.SetValue(ExifTag.DateTime, formatted);
            }

            SetStringIfPresent(profile, ExifTag.Artist, dto.Artist);
            SetStringIfPresent(profile, ExifTag.Copyright, dto.Copyright);
            SetStringIfPresent(profile, ExifTag.ImageDescription, dto.ImageDescription);
            SetStringIfPresent(profile, ExifTag.Make, dto.CameraMake);
            SetStringIfPresent(profile, ExifTag.Model, dto.CameraModel);
            SetStringIfPresent(profile, ExifTag.LensModel, dto.Lens);

            if (!string.IsNullOrWhiteSpace(dto.FocalLength) &&
                TryParseDouble(dto.FocalLength, out var focal))
            {
                profile.SetValue(ExifTag.FocalLength, ToRational(focal));
            }

            if (!string.IsNullOrWhiteSpace(dto.Aperture) &&
                TryParseAperture(dto.Aperture, out var fnumber))
            {
                profile.SetValue(ExifTag.FNumber, ToRational(fnumber));
            }

            if (!string.IsNullOrWhiteSpace(dto.ShutterSpeed) &&
                TryParseShutter(dto.ShutterSpeed, out var exposure))
            {
                profile.SetValue(ExifTag.ExposureTime, ToRational(exposure));
            }

            if (dto.Iso.HasValue)
                profile.SetValue(ExifTag.ISOSpeedRatings, [(ushort)Math.Clamp(dto.Iso.Value, 0, ushort.MaxValue)]);

            if (dto.GpsLatitude.HasValue && dto.GpsLongitude.HasValue)
                SetGps(profile, dto.GpsLatitude.Value, dto.GpsLongitude.Value);

            if (dto.ExtraTags is { Count: > 0 })
            {
                foreach (var (name, value) in dto.ExtraTags)
                {
                    if (string.IsNullOrWhiteSpace(name) || value is null)
                        continue;
                    if (!WritableStringExtraTags.Contains(name.Trim()))
                        continue;
                    ApplyWritableStringTag(profile, name.Trim(), value);
                }
            }

            image.Metadata.ExifProfile = profile;
            image.Save(fullPath);
        }
    }

    private static ParsedExif ParseProfile(ExifProfile profile)
    {
        var result = new ParsedExif();
        var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (TryGetString(profile, ExifTag.DateTimeOriginal, out var dto) ||
            TryGetString(profile, ExifTag.DateTime, out dto))
        {
            if (TryParseExifDate(dto!, out var dateTaken))
                result.DateTaken = dateTaken;
        }

        if (TryGetString(profile, ExifTag.Make, out var make))
            result.CameraMake = make;
        if (TryGetString(profile, ExifTag.Model, out var model))
            result.CameraModel = model;

        if (TryGetString(profile, ExifTag.LensModel, out var lens) ||
            TryGetString(profile, ExifTag.LensMake, out lens))
            result.Lens = lens;

        if (profile.TryGetValue(ExifTag.FocalLength, out IExifValue<Rational>? focal) && focal.Value.Denominator != 0)
            result.FocalLength = FormatNumber(focal.Value.ToDouble()) + " mm";

        if (profile.TryGetValue(ExifTag.FNumber, out IExifValue<Rational>? fnumber) && fnumber.Value.Denominator != 0)
            result.Aperture = "f/" + FormatNumber(fnumber.Value.ToDouble());

        if (profile.TryGetValue(ExifTag.ExposureTime, out IExifValue<Rational>? exposure) && exposure.Value.Denominator != 0)
            result.ShutterSpeed = FormatShutter(exposure.Value);

        if (profile.TryGetValue(ExifTag.ISOSpeedRatings, out IExifValue<ushort[]>? isoArr) &&
            isoArr.Value is { Length: > 0 })
            result.Iso = isoArr.Value[0];
        else if (profile.TryGetValue(ExifTag.ISOSpeed, out IExifValue<uint>? isoSpeed))
            result.Iso = (int)isoSpeed.Value;

        if (TryGetGps(profile, out var lat, out var lon))
        {
            result.GpsLatitude = lat;
            result.GpsLongitude = lon;
        }

        if (TryGetString(profile, ExifTag.Artist, out var artist))
            result.Artist = artist;
        if (TryGetString(profile, ExifTag.Copyright, out var copyright))
            result.Copyright = copyright;
        if (TryGetString(profile, ExifTag.ImageDescription, out var desc))
            result.ImageDescription = desc;

        foreach (var value in profile.Values)
        {
            var name = value.Tag.ToString();
            if (KnownTagNames.Contains(name))
                continue;
            var text = value.GetValue()?.ToString();
            if (string.IsNullOrWhiteSpace(text))
                continue;
            extras[name] = text;
        }

        if (extras.Count > 0)
            result.ExtraJson = JsonSerializer.Serialize(extras);

        return result;
    }

    private static void ApplyParsedToMirror(PhotoExif mirror, ParsedExif parsed)
    {
        mirror.DateTaken = parsed.DateTaken.HasValue ? EnsureUtc(parsed.DateTaken.Value) : null;
        mirror.CameraMake = Truncate(parsed.CameraMake, 200);
        mirror.CameraModel = Truncate(parsed.CameraModel, 200);
        mirror.Lens = Truncate(parsed.Lens, 200);
        mirror.FocalLength = Truncate(parsed.FocalLength, 100);
        mirror.Aperture = Truncate(parsed.Aperture, 100);
        mirror.ShutterSpeed = Truncate(parsed.ShutterSpeed, 100);
        mirror.Iso = parsed.Iso;
        mirror.GpsLatitude = parsed.GpsLatitude;
        mirror.GpsLongitude = parsed.GpsLongitude;
        mirror.Artist = Truncate(parsed.Artist, 500);
        mirror.Copyright = Truncate(parsed.Copyright, 500);
        mirror.ImageDescription = Truncate(parsed.ImageDescription, 2000);
        mirror.ExtraJson = parsed.ExtraJson;
    }

    private static void SetStringIfPresent(ExifProfile profile, ExifTag<string> tag, string? value)
    {
        if (value is null)
            return;
        profile.SetValue(tag, value);
    }

    private static void ApplyWritableStringTag(ExifProfile profile, string name, string value)
    {
        if (name.Equals(nameof(ExifTag.Software), StringComparison.OrdinalIgnoreCase))
            profile.SetValue(ExifTag.Software, value);
        else if (name.Equals(nameof(ExifTag.LensMake), StringComparison.OrdinalIgnoreCase))
            profile.SetValue(ExifTag.LensMake, value);
        else if (name.Equals(nameof(ExifTag.LensSerialNumber), StringComparison.OrdinalIgnoreCase))
            profile.SetValue(ExifTag.LensSerialNumber, value);
        else if (name.Equals(nameof(ExifTag.GPSMapDatum), StringComparison.OrdinalIgnoreCase))
            profile.SetValue(ExifTag.GPSMapDatum, value);
        else if (name.Equals(nameof(ExifTag.DateTimeDigitized), StringComparison.OrdinalIgnoreCase))
            profile.SetValue(ExifTag.DateTimeDigitized, value);
    }

    private static bool TryGetString(ExifProfile profile, ExifTag<string> tag, out string? value)
    {
        if (profile.TryGetValue(tag, out IExifValue<string>? v) && !string.IsNullOrWhiteSpace(v.Value))
        {
            value = v.Value;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryParseExifDate(string text, out DateTime date)
    {
        // EXIF: "yyyy:MM:dd HH:mm:ss"
        if (DateTime.TryParseExact(
                text.Trim(),
                "yyyy:MM:dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out date))
            return true;

        if (DateTime.TryParse(
                text.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out date))
            return true;

        date = default;
        return false;
    }

    private static string FormatExifDate(DateTime utc) =>
        utc.ToUniversalTime().ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static bool TryGetGps(ExifProfile profile, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;

        if (!profile.TryGetValue(ExifTag.GPSLatitude, out IExifValue<Rational[]>? latVals) ||
            !profile.TryGetValue(ExifTag.GPSLongitude, out IExifValue<Rational[]>? lonVals) ||
            !profile.TryGetValue(ExifTag.GPSLatitudeRef, out IExifValue<string>? latRef) ||
            !profile.TryGetValue(ExifTag.GPSLongitudeRef, out IExifValue<string>? lonRef))
            return false;

        if (!TryRationalArrayToDegrees(latVals.Value, out var lat) ||
            !TryRationalArrayToDegrees(lonVals.Value, out var lon))
            return false;

        if (string.Equals(latRef.Value, "S", StringComparison.OrdinalIgnoreCase))
            lat = -lat;
        if (string.Equals(lonRef.Value, "W", StringComparison.OrdinalIgnoreCase))
            lon = -lon;

        latitude = lat;
        longitude = lon;
        return true;
    }

    private static void SetGps(ExifProfile profile, double latitude, double longitude)
    {
        profile.SetValue(ExifTag.GPSLatitudeRef, latitude >= 0 ? "N" : "S");
        profile.SetValue(ExifTag.GPSLongitudeRef, longitude >= 0 ? "E" : "W");
        profile.SetValue(ExifTag.GPSLatitude, DegreesToRationalArray(Math.Abs(latitude)));
        profile.SetValue(ExifTag.GPSLongitude, DegreesToRationalArray(Math.Abs(longitude)));
    }

    private static bool TryRationalArrayToDegrees(Rational[]? values, out double degrees)
    {
        degrees = 0;
        if (values is null || values.Length < 3)
            return false;

        degrees = values[0].ToDouble()
                  + values[1].ToDouble() / 60.0
                  + values[2].ToDouble() / 3600.0;
        return true;
    }

    private static Rational[] DegreesToRationalArray(double degrees)
    {
        var d = Math.Floor(degrees);
        var mFloat = (degrees - d) * 60.0;
        var m = Math.Floor(mFloat);
        var s = (mFloat - m) * 60.0;
        return
        [
            new Rational((uint)d, 1),
            new Rational((uint)m, 1),
            ToRational(s)
        ];
    }

    private static Rational ToRational(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return new Rational(0, 1);

        const uint scale = 10000;
        var num = (uint)Math.Round(Math.Abs(value) * scale);
        return new Rational(num, scale);
    }

    private static bool TryParseDouble(string text, out double value)
    {
        var cleaned = text.Replace("mm", "", StringComparison.OrdinalIgnoreCase).Trim();
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseAperture(string text, out double value)
    {
        var cleaned = text.Trim();
        if (cleaned.StartsWith("f/", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[2..];
        else if (cleaned.StartsWith("f", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[1..];
        return double.TryParse(cleaned.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseShutter(string text, out double seconds)
    {
        var cleaned = text.Trim().TrimEnd('s', 'S');
        if (cleaned.Contains('/'))
        {
            var parts = cleaned.Split('/', 2);
            if (parts.Length == 2
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var num)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var den)
                && den != 0)
            {
                seconds = num / den;
                return true;
            }
        }

        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
    }

    private static string FormatNumber(double value)
    {
        if (Math.Abs(value - Math.Round(value)) < 0.0001)
            return ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatShutter(Rational exposure)
    {
        var seconds = exposure.ToDouble();
        if (seconds <= 0)
            return exposure.ToString();
        if (seconds >= 1)
            return FormatNumber(seconds) + "s";
        var denom = (int)Math.Round(1.0 / seconds);
        if (denom > 1)
            return "1/" + denom;
        return FormatNumber(seconds) + "s";
    }

    private static string? Truncate(string? value, int max)
    {
        if (value is null)
            return null;
        return value.Length <= max ? value : value[..max];
    }

    private sealed class ParsedExif
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
        public string? ExtraJson { get; set; }
    }
}
