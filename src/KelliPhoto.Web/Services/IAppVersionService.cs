namespace KelliPhoto.Web.Services;

public interface IAppVersionService
{
    /// <summary>Semantic version from VERSION (e.g. 1.0.0).</summary>
    string SemVer { get; }

    /// <summary>Full informational version, may include build metadata (e.g. 1.0.0+abc1234).</summary>
    string DisplayVersion { get; }
}
