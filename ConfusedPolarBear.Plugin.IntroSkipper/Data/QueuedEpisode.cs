using System;

namespace ConfusedPolarBear.Plugin.IntroSkipper;

/// <summary>
/// Episode queued for analysis.
/// </summary>
public class QueuedEpisode {
    /// <summary>
    /// Series name.
    /// </summary>
    public string SeriesName { get; set; } = "";

    /// <summary>
    /// Season number.
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    /// Episode id.
    /// </summary>
    public Guid EpisodeId { get; set; }

    /// <summary>
    /// Full path to episode.
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// Seconds of media file to fingerprint.
    /// </summary>
    public int FingerprintDuration { get; set; }
}
