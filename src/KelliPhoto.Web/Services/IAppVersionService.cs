namespace KelliPhoto.Web.Services;

public interface IAppVersionService
{
    /// <summary>Semantic version from VERSION (e.g. 1.0.0).</summary>
    string SemVer { get; }

    /// <summary>Short build id when present (e.g. CI git SHA prefix).</summary>
    string? BuildId { get; }

    /// <summary>Primary label for UI (e.g. v1.0.0).</summary>
    string DisplayVersion { get; }
}
