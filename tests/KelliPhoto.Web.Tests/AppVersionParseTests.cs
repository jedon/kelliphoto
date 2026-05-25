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
}
