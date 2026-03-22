namespace KelliPhoto.Web.Services;

public interface IPathService
{
    string GetRelativePath(string fullPath);
    string GetFullPath(string relativePath);
    string NormalizePath(string path);

    /// <summary>
    /// Resolves a stored DB path to a file that exists on disk.
    /// For legacy UNC rows, tries both gallery root and LegacyWindowsLocalMountPath with the same relative tail.
    /// If no exact path exists, walks the tree using <c>GallerySettings:FolderNameAliases</c>, then case-insensitive and fuzzy names.
    /// </summary>
    string? ResolveExistingPhotoFilePath(string storedPath);
}

public class PathService : IPathService
{
    private readonly IConfiguration _configuration;
    private Dictionary<string, string>? _folderNameAliases;

    public PathService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>Read on each use so integration tests (and late config sources) see the correct gallery root.</summary>
    private string GalleryBasePath =>
        NormalizePath(_configuration["GallerySettings:GalleryPath"]
            ?? throw new InvalidOperationException("GallerySettings:GalleryPath not configured"));

    public string GetRelativePath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath))
            return string.Empty;

        var normalizedFullPath = NormalizePath(fullPath);
        var normalizedBasePath = NormalizePath(GalleryBasePath);
        
        // Use case-insensitive comparison (important for Windows/UNC paths)
        StringComparison comparison = StringComparison.OrdinalIgnoreCase;
        
        // Check if the path is within the gallery base path
        if (normalizedFullPath.StartsWith(normalizedBasePath, comparison))
        {
            var relativePath = normalizedFullPath.Substring(normalizedBasePath.Length);
            // Remove leading separator
            relativePath = relativePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\', '/');
            return relativePath;
        }

        // If path is already relative or outside gallery, return as-is (for backward compatibility)
        return normalizedFullPath;
    }

    public string GetFullPath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return GalleryBasePath;

        var remapped = TryRemapLegacyWindowsUncToGallery(relativePath);
        if (remapped != null)
            return remapped;

        // Check if it's a UNC path (starts with \\)
        bool isUncPath = relativePath.StartsWith(@"\\", StringComparison.Ordinal);
        
        // If it's already an absolute path (rooted or UNC), return it normalized
        if (Path.IsPathRooted(relativePath) || isUncPath)
        {
            return NormalizePath(relativePath);
        }

        // Combine with base path - handle UNC paths specially
        string fullPath;
        var galleryBase = GalleryBasePath;
        if (galleryBase.StartsWith(@"\\", StringComparison.Ordinal))
        {
            // For UNC paths, use string concatenation with proper separator
            var separator = galleryBase.EndsWith(@"\") || galleryBase.EndsWith("/") ? "" : @"\";
            fullPath = galleryBase + separator + relativePath;
        }
        else
        {
            // For regular paths, use Path.Combine
            fullPath = Path.Combine(galleryBase, relativePath);
        }
        
        return NormalizePath(fullPath);
    }

    public string? ResolveExistingPhotoFilePath(string storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
            return null;

        var candidates = new List<string>();

        void TryAdd(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            var n = NormalizePath(path);
            if (candidates.Exists(c => string.Equals(c, n, StringComparison.OrdinalIgnoreCase)))
                return;
            candidates.Add(n);
        }

        TryAdd(GetFullPath(storedPath));

        if (TryGetLegacyUncRemainder(storedPath, out var remainder))
        {
            remainder = remainder.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            TryAdd(Path.Combine(GalleryBasePath, remainder));

            var localMount = _configuration["GallerySettings:LegacyWindowsLocalMountPath"];
            if (!string.IsNullOrWhiteSpace(localMount))
                TryAdd(Path.Combine(NormalizePath(localMount), remainder));
        }

        foreach (var c in candidates)
        {
            if (File.Exists(c))
                return c;
        }

        foreach (var c in candidates)
        {
            var adapted = TryAdaptPathToExistingFilesystem(c);
            if (adapted != null && File.Exists(adapted))
                return NormalizePath(adapted);
        }

        return null;
    }

    /// <summary>
    /// When <paramref name="candidatePath"/> does not exist, re-walk each segment under a configured gallery root.
    /// Each directory segment: optional <c>GallerySettings:FolderNameAliases</c> (DB/Windows name → Linux folder name),
    /// then case-insensitive match, then fuzzy alphanumeric match if unique.
    /// </summary>
    private string? TryAdaptPathToExistingFilesystem(string candidatePath)
    {
        var n = NormalizePath(candidatePath);
        foreach (var root in GetGalleryRootsLongestFirst())
        {
            if (!n.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                continue;

            var tail = n.Length > root.Length
                ? n.Substring(root.Length).Trim(Path.DirectorySeparatorChar, '/', '\\')
                : string.Empty;
            if (string.IsNullOrEmpty(tail))
                return null;

            var segments = tail.Split(new[] { Path.DirectorySeparatorChar, '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return null;

            var walked = WalkGalleryRelative(root, segments);
            if (walked != null)
                return walked;
        }

        return null;
    }

    /// <summary>Maps folder names as stored in the DB / Windows to actual directory names on Linux (from <c>ls</c>).</summary>
    private string? GetFolderNameAlias(string segmentFromDbOrWindows)
    {
        _folderNameAliases ??= LoadFolderNameAliases();
        return _folderNameAliases.TryGetValue(segmentFromDbOrWindows, out var mapped) ? mapped : null;
    }

    private Dictionary<string, string> LoadFolderNameAliases()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var section = _configuration.GetSection("GallerySettings:FolderNameAliases");
        foreach (var child in section.GetChildren())
        {
            var key = child.Key;
            var value = child.Value;
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                continue;
            d[key.Trim()] = value.Trim();
        }

        return d;
    }

    private IEnumerable<string> GetGalleryRootsLongestFirst()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();

        void AddRoot(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            var p = NormalizePath(path);
            if (!seen.Add(p))
                return;
            list.Add(p);
        }

        AddRoot(GalleryBasePath);
        AddRoot(_configuration["GallerySettings:LegacyWindowsLocalMountPath"]);
        list.Sort((a, b) => b.Length.CompareTo(a.Length));
        return list;
    }

    private string? WalkGalleryRelative(string root, string[] segments)
    {
        var current = root;
        for (var i = 0; i < segments.Length; i++)
        {
            var isLast = i == segments.Length - 1;
            if (!Directory.Exists(current))
                return null;

            if (isLast)
                return MatchFileInDirectory(current, segments[i]) is { } fileName
                    ? Path.Combine(current, fileName)
                    : null;

            var dirName = MatchDirectoryInParent(current, segments[i]);
            if (dirName == null)
                return null;
            current = Path.Combine(current, dirName);
        }

        return null;
    }

    private string? MatchDirectoryInParent(string parentDir, string desiredName)
    {
        var direct = Path.Combine(parentDir, desiredName);
        if (Directory.Exists(direct))
            return desiredName;

        if (GetFolderNameAlias(desiredName) is { } alias)
        {
            var aliased = Path.Combine(parentDir, alias);
            if (Directory.Exists(aliased))
                return alias;
        }

        foreach (var path in Directory.EnumerateDirectories(parentDir))
        {
            var name = Path.GetFileName(path);
            if (string.Equals(name, desiredName, StringComparison.OrdinalIgnoreCase))
                return name;
        }

        return UniqueFuzzyNameMatch(desiredName, Directory.EnumerateDirectories(parentDir), Path.GetFileName);
    }

    private static string? MatchFileInDirectory(string directory, string desiredFileName)
    {
        var direct = Path.Combine(directory, desiredFileName);
        if (File.Exists(direct))
            return desiredFileName;

        foreach (var path in Directory.EnumerateFiles(directory))
        {
            var name = Path.GetFileName(path);
            if (string.Equals(name, desiredFileName, StringComparison.OrdinalIgnoreCase))
                return name;
        }

        var wantBase = Path.GetFileNameWithoutExtension(desiredFileName);
        var wantExt = Path.GetExtension(desiredFileName);
        var wantKey = NormalizeFuzzyKey(wantBase);
        string? unique = null;
        foreach (var path in Directory.EnumerateFiles(directory))
        {
            var name = Path.GetFileName(path);
            if (!string.Equals(Path.GetExtension(name), wantExt, StringComparison.OrdinalIgnoreCase))
                continue;
            if (NormalizeFuzzyKey(Path.GetFileNameWithoutExtension(name)) != wantKey)
                continue;
            if (unique != null)
                return null;
            unique = name;
        }

        return unique;
    }

    private static string? UniqueFuzzyNameMatch(
        string desiredName,
        IEnumerable<string> fullPaths,
        Func<string, string> getName)
    {
        var want = NormalizeFuzzyKey(desiredName);
        string? unique = null;
        foreach (var path in fullPaths)
        {
            var name = getName(path);
            if (NormalizeFuzzyKey(name) != want)
                continue;
            if (unique != null)
                return null;
            unique = name;
        }

        return unique;
    }

    /// <summary>Lowercase letters and digits only — ignores spaces, punctuation, and case.</summary>
    private static string NormalizeFuzzyKey(string s) =>
        new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    public string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        // Preserve UNC path prefix
        bool isUncPath = path.StartsWith(@"\\", StringComparison.Ordinal);
        string normalizedPath = path;

        if (isUncPath)
        {
            // For UNC paths, normalize separators but preserve the \\ prefix
            normalizedPath = @"\\" + path.Substring(2).Replace('/', '\\');
        }
        else
        {
            // Normalize directory separators to the current platform's separator
            normalizedPath = path.Replace('\\', Path.DirectorySeparatorChar);
            normalizedPath = normalizedPath.Replace('/', Path.DirectorySeparatorChar);
        }
        
        // Remove trailing separators (except for root paths and UNC server names)
        if (normalizedPath.Length > 1 && 
            normalizedPath.Length > (isUncPath ? 3 : 1) && // Don't trim if it's just "\\" or "/"
            (normalizedPath.EndsWith(Path.DirectorySeparatorChar) || normalizedPath.EndsWith(Path.AltDirectorySeparatorChar)))
        {
            normalizedPath = normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return normalizedPath;
    }

    /// <summary>
    /// When the DB was populated on Windows, photo rows may store full UNC paths.
    /// Linux/Docker cannot open those; map the configured UNC root to <c>GallerySettings:GalleryPath</c>.
    /// </summary>
    private string? TryRemapLegacyWindowsUncToGallery(string storedPath)
    {
        if (!TryGetLegacyUncRemainder(storedPath, out var remainder))
            return null;

        if (string.IsNullOrEmpty(remainder))
            return GalleryBasePath;

        remainder = remainder.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

        // When GalleryPath is the whole mount (e.g. /mnt/gallery) but Windows files live under /mnt/gallery/source,
        // set LegacyWindowsLocalMountPath to that Linux directory. Otherwise UNC paths map next to theme-cards, etc.
        var localMount = _configuration["GallerySettings:LegacyWindowsLocalMountPath"];
        var targetBase = string.IsNullOrWhiteSpace(localMount)
            ? GalleryBasePath
            : NormalizePath(localMount);

        return NormalizePath(Path.Combine(targetBase, remainder));
    }

    /// <summary>Relative path after configured LegacyWindowsGalleryRoot for a UNC row.</summary>
    private bool TryGetLegacyUncRemainder(string storedPath, out string remainder)
    {
        remainder = string.Empty;
        var legacyRoot = _configuration["GallerySettings:LegacyWindowsGalleryRoot"];
        if (string.IsNullOrWhiteSpace(legacyRoot) || !storedPath.StartsWith(@"\\", StringComparison.Ordinal))
            return false;

        var normalizedLegacy = NormalizePath(legacyRoot.TrimEnd('\\', '/', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var normalizedStored = NormalizePath(storedPath);

        if (!normalizedStored.StartsWith(normalizedLegacy, StringComparison.OrdinalIgnoreCase))
            return false;

        remainder = normalizedStored.Length > normalizedLegacy.Length
            ? normalizedStored.Substring(normalizedLegacy.Length)
            : string.Empty;
        remainder = remainder.TrimStart('\\', '/', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return !string.IsNullOrEmpty(remainder);
    }
}
