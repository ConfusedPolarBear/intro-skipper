using System;
using System.Collections.Generic;
using System.Globalization;
using ConfusedPolarBear.Plugin.IntroSkipper.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace ConfusedPolarBear.Plugin.IntroSkipper;

/// <summary>
/// Intro skipper plugin. Uses audio analysis to find common sequences of audio shared between episodes.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Intros = new Dictionary<Guid, Intro>();
        AnalysisQueue = new Dictionary<Guid, List<QueuedEpisode>>();
        Instance = this;

        Configuration.RestoreTimestamps();
    }

    /// <summary>
    /// Results of fingerprinting all episodes.
    /// </summary>
    public Dictionary<Guid, Intro> Intros { get; }

    /// <summary>
    /// Map of season ids to episodes that have been queued for fingerprinting.
    /// </summary>
    public Dictionary<Guid, List<QueuedEpisode>> AnalysisQueue { get; }

    /// <summary>
    /// Total number of episodes in the queue.
    /// </summary>
    public int TotalQueued { get; set; }

    /// <inheritdoc />
    public override string Name => "Intro Skipper";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("c83d86bb-a1e0-4c35-a113-e2101cf4ee6b");

    /// <inheritdoc />
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = this.Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            }
        };
    }
}
