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
    }

    public string SemVer { get; }
    public string DisplayVersion { get; }

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

    [GeneratedRegex("^[0-9a-f]{40}$", RegexOptions.IgnoreCase)]
    private static partial Regex FullGitSha();
}
