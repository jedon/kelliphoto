using System;

namespace KelliPhoto.Web.Models;

public class SlideshowSettings
{
    private int _secondsPerImage = 5;
    private string _transition = "Fade";

    public int SecondsPerImage
    {
        get => _secondsPerImage;
        set => _secondsPerImage = Math.Clamp(value, 3, 30);
    }

    public string Transition
    {
        get => _transition;
        set => _transition = IsValidTransition(value) ? value : "Fade";
    }

    public bool Loop { get; set; } = true;

    private static bool IsValidTransition(string? transition)
    {
        return transition is "None" or "Fade" or "Slide";
    }
}
