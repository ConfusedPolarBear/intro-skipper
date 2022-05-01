using System;

namespace ConfusedPolarBear.Plugin.IntroSkipper;

/// <summary>
/// Result of fingerprinting and analyzing two episodes in a season.
/// All times are measured in seconds relative to the beginning of the media file.
/// </summary>
[Serializable]
public class Intro {
    /// <summary>
    /// If this introduction is valid or not. Invalid results should not be returned through the API.
    /// </summary>
    public bool Valid { get; set; }

    /// <summary>
    /// Introduction sequence start time.
    /// </summary>
    public double IntroStart { get; set; }

    /// <summary>
    /// Introduction sequence end time.
    /// </summary>
    public double IntroEnd { get; set; }

    /// <summary>
    /// Recommended time to display the skip intro prompt.
    /// </summary>
    public double ShowSkipPromptAt { get; set; }

    /// <summary>
    /// Recommended time to hide the skip intro prompt.
    /// </summary>
    public double HideSkipPromptAt { get; set; }
}
