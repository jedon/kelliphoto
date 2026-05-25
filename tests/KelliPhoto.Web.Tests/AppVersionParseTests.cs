using KelliPhoto.Web.Services;

namespace KelliPhoto.Web.Tests;

public class AppVersionParseTests
{
    [Fact]
    public void ParseSemVer_StripsBuildMetadataAndSdkGitSuffix()
    {
        Assert.Equal("1.0.0", AppVersionService.ParseSemVer(
            "1.0.0+f9bf525.f9bf525ab0b703f7a13fa8e042b2c0162c63fb3e"));
    }

    [Fact]
    public void ParseSemVer_PlainSemVer()
    {
        Assert.Equal("1.0.0", AppVersionService.ParseSemVer("1.0.0"));
    }

    [Fact]
    public void FormatBuildTooltip_IncludesCommitAndPublishedTime()
    {
        var tooltip = AppVersionService.FormatBuildTooltip(
            "59ab4d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7",
            "2026-05-25T17:30:00Z");

        Assert.Equal(
            "Commit 59ab4d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7 · Published 2026-05-25 17:30 UTC",
            tooltip);
    }

    [Fact]
    public void FormatBuildTooltip_CommitOnly()
    {
        var tooltip = AppVersionService.FormatBuildTooltip("abc123", null);
        Assert.Equal("Commit abc123", tooltip);
    }

    [Fact]
    public void FormatBuildTooltip_EmptyWhenNoMetadata()
    {
        Assert.Null(AppVersionService.FormatBuildTooltip(null, null));
    }
}
