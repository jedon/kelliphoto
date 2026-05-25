using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace KelliPhoto.Web.Services;

public sealed partial class AppVersionService : IAppVersionService
{
    public AppVersionService()
    {
        var assembly = typeof(AppVersionService).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";

        SemVer = ParseSemVer(informational);
        DisplayVersion = $"v{SemVer}";

        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
        var gitCommit = metadata.FirstOrDefault(m => m.Key == "GitCommit")?.Value;
        var buildTimestamp = metadata.FirstOrDefault(m => m.Key == "BuildTimestamp")?.Value;
        BuildTooltip = FormatBuildTooltip(gitCommit, buildTimestamp);
    }

    public string SemVer { get; }
    public string DisplayVersion { get; }
    public string? BuildTooltip { get; }

    internal static string ParseSemVer(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "0.0.0";

        var plus = raw.IndexOf('+', StringComparison.Ordinal);
        var semver = plus >= 0 ? raw[..plus] : raw;

        var dot = semver.IndexOf('.');
        if (dot > 0 && FullGitSha().IsMatch(semver[(dot + 1)..]))
            semver = semver[..dot];

        return semver;
    }

    internal static string? FormatBuildTooltip(string? gitCommit, string? buildTimestamp)
    {
        var parts = new List<string>(2);

        if (!string.IsNullOrWhiteSpace(gitCommit))
            parts.Add($"Commit {gitCommit.Trim()}");

        if (!string.IsNullOrWhiteSpace(buildTimestamp))
        {
            var published = DateTime.TryParse(
                buildTimestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var utc)
                ? utc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC"
                : buildTimestamp.Trim();
            parts.Add($"Published {published}");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    [GeneratedRegex("^[0-9a-f]{40}$", RegexOptions.IgnoreCase)]
    private static partial Regex FullGitSha();
}
