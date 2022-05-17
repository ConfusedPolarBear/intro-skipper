using System;

namespace ConfusedPolarBear.Plugin.IntroSkipper;

/// <summary>
/// Result of fingerprinting and analyzing two episodes in a season.
/// All times are measured in seconds relative to the beginning of the media file.
/// </summary>
public class Intro
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Intro"/> class.
    /// </summary>
    /// <param name="episode">Episode.</param>
    /// <param name="intro">Introduction time range.</param>
    public Intro(Guid episode, TimeRange intro)
    {
        EpisodeId = episode;
        IntroStart = intro.Start;
        IntroEnd = intro.End;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Intro"/> class.
    /// </summary>
    /// <param name="episode">Episode.</param>
    public Intro(Guid episode)
    {
        EpisodeId = episode;
        IntroStart = 0;
        IntroEnd = 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Intro"/> class.
    /// </summary>
    public Intro()
    {
    }

    /// <summary>
    /// Gets or sets the Episode ID.
    /// </summary>
    public Guid EpisodeId { get; set; }

    /// <summary>
    /// Gets a value indicating whether this introduction is valid or not.
    /// Invalid results must not be returned through the API.
    /// </summary>
    public bool Valid => IntroEnd > 0;

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
