using System;
using KelliPhoto.Web.Models;
using Xunit;

namespace KelliPhoto.Web.Tests;

public class SlideshowSettingsTests
{
    [Fact]
    public void DefaultSettings_AreCorrect()
    {
        var settings = new SlideshowSettings();
        
        Assert.Equal(5, settings.SecondsPerImage);
        Assert.Equal("Fade", settings.Transition);
        Assert.True(settings.Loop);
    }

    [Theory]
    [InlineData(2, 3)]
    [InlineData(3, 3)]
    [InlineData(10, 10)]
    [InlineData(30, 30)]
    [InlineData(31, 30)]
    public void SecondsPerImage_IsClampedBetween3And30(int input, int expected)
    {
        var settings = new SlideshowSettings { SecondsPerImage = input };
        Assert.Equal(expected, settings.SecondsPerImage);
    }

    [Theory]
    [InlineData("None", "None")]
    [InlineData("Fade", "Fade")]
    [InlineData("Slide", "Slide")]
    [InlineData("Zoom", "Fade")]
    [InlineData(null, "Fade")]
    [InlineData("", "Fade")]
    public void Transition_FallsBackToFade_WhenInvalid(string? input, string expected)
    {
        var settings = new SlideshowSettings { Transition = input! };
        Assert.Equal(expected, settings.Transition);
    }
}
