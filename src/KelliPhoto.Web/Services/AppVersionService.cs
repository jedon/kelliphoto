using System.Reflection;

namespace KelliPhoto.Web.Services;

public sealed class AppVersionService : IAppVersionService
{
    public AppVersionService()
    {
        var assembly = typeof(AppVersionService).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        DisplayVersion = string.IsNullOrWhiteSpace(informational)
            ? assembly.GetName().Version?.ToString() ?? "0.0.0"
            : informational;
        SemVer = DisplayVersion.Split('+', StringSplitOptions.RemoveEmptyEntries)[0];
    }

    public string SemVer { get; }
    public string DisplayVersion { get; }
}
