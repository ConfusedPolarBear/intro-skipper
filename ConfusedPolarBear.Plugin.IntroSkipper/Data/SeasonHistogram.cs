#pragma warning disable CA1815 // Override equals and operator equals on value types

using System;
using System.Collections.ObjectModel;

namespace ConfusedPolarBear.Plugin.IntroSkipper;

/// <summary>
/// Histogram entry for episodes in a season.
/// </summary>
public struct SeasonHistogram
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonHistogram"/> struct.
    /// </summary>
    /// <param name="firstEpisode">First episode seen with this duration.</param>
    public SeasonHistogram(Guid firstEpisode)
    {
        Episodes.Add(firstEpisode);
    }

    /// <summary>
    /// Gets episodes with this duration.
    /// </summary>
    public Collection<Guid> Episodes { get; } = new Collection<Guid>();

    /// <summary>
    /// Gets the number of times an episode with an intro of this duration has been seen.
    /// </summary>
    public int Count => Episodes?.Count ?? 0;
}
