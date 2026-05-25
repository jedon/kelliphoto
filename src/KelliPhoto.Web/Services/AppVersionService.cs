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

        var parsed = Parse(informational);
        SemVer = parsed.SemVer;
        BuildId = parsed.BuildId;
        DisplayVersion = parsed.BuildId is null
            ? $"v{parsed.SemVer}"
            : $"v{parsed.SemVer}";
    }

    public string SemVer { get; }
    public string? BuildId { get; }
    public string DisplayVersion { get; }

    internal static (string SemVer, string? BuildId) Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ("0.0.0", null);

        var plus = raw.IndexOf('+', StringComparison.Ordinal);
        var semver = plus >= 0 ? raw[..plus] : raw;

        // SDK may append ".{fullGitSha}" after our short build id.
        var dot = semver.IndexOf('.');
        if (dot > 0 && FullGitSha().IsMatch(semver[(dot + 1)..]))
            semver = semver[..dot];

        if (plus < 0)
            return (semver, null);

        var metadata = raw[(plus + 1)..];
        var metaDot = metadata.IndexOf('.');
        if (metaDot > 0 && FullGitSha().IsMatch(metadata[(metaDot + 1)..]))
            metadata = metadata[..metaDot];

        var build = metadata.Trim();
        return (semver, string.IsNullOrEmpty(build) ? null : build);
    }

    [GeneratedRegex("^[0-9a-f]{40}$", RegexOptions.IgnoreCase)]
    private static partial Regex FullGitSha();
}
