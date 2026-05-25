using KelliPhoto.Web.Services;

namespace KelliPhoto.Web.Tests;

public class AppVersionParseTests
{
    [Fact]
    public void Parse_StripsSdkAppendedFullGitSha()
    {
        var (semver, build) = AppVersionService.Parse(
            "1.0.0+f9bf525.f9bf525ab0b703f7a13fa8e042b2c0162c63fb3e");

        Assert.Equal("1.0.0", semver);
        Assert.Equal("f9bf525", build);
    }

    [Fact]
    public void Parse_PlainSemVer_HasNoBuild()
    {
        var (semver, build) = AppVersionService.Parse("1.0.0");

        Assert.Equal("1.0.0", semver);
        Assert.Null(build);
    }

    [Fact]
    public void Parse_CiStyleMetadata()
    {
        var (semver, build) = AppVersionService.Parse("1.0.0+abc1234");

        Assert.Equal("1.0.0", semver);
        Assert.Equal("abc1234", build);
    }
}
