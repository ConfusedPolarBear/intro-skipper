using System;

namespace ConfusedPolarBear.Plugin.IntroSkipper;

/// <summary>
/// Result of fingerprinting and analyzing two episodes in a season.
/// All times are measured in seconds relative to the beginning of the media file.
/// </summary>
public class Intro
{
    /// <summary>
    /// Gets or sets the Episode ID.
    /// </summary>
    public Guid EpisodeId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this introduction is valid or not.
    /// Invalid results must not be returned through the API.
    /// </summary>
    public bool Valid { get; set; }

    /// <summary>
    /// Gets or sets the introduction sequence start time.
    /// </summary>
    public double IntroStart { get; set; }

    /// <summary>
    /// Gets or sets the introduction sequence end time.
    /// </summary>
    public double IntroEnd { get; set; }

    /// <summary>
    /// Gets or sets the recommended time to display the skip intro prompt.
    /// </summary>
    public double ShowSkipPromptAt { get; set; }

    /// <summary>
    /// Gets or sets the recommended time to hide the skip intro prompt.
    /// </summary>
    public double HideSkipPromptAt { get; set; }
}
