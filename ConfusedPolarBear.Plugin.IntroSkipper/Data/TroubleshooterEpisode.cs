using System;

namespace ConfusedPolarBear.Plugin.IntroSkipper;

/// <summary>
/// Episode name and internal ID as returned by the troubleshooter.
/// </summary>
public class TroubleshooterEpisode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TroubleshooterEpisode"/> class.
    /// </summary>
    /// <param name="id">Episode id.</param>
    /// <param name="name">Episode name.</param>
    public TroubleshooterEpisode(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>
    /// Gets the id.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;
}
