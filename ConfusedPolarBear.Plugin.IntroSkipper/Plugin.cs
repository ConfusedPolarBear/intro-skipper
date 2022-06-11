using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ConfusedPolarBear.Plugin.IntroSkipper.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace ConfusedPolarBear.Plugin.IntroSkipper;

/// <summary>
/// Intro skipper plugin. Uses audio analysis to find common sequences of audio shared between episodes.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private IXmlSerializer _xmlSerializer;
    private string _introPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    /// <param name="serverConfiguration">Server configuration manager.</param>
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        IServerConfigurationManager serverConfiguration)
        : base(applicationPaths, xmlSerializer)
    {
        _xmlSerializer = xmlSerializer;

        // Create the base & cache directories (if needed).
        FingerprintCachePath = Path.Join(applicationPaths.PluginConfigurationsPath, "intros", "cache");
        if (!Directory.Exists(FingerprintCachePath))
        {
            Directory.CreateDirectory(FingerprintCachePath);
        }

        _introPath = Path.Join(applicationPaths.PluginConfigurationsPath, "intros", "intros.xml");

        // Get the path to FFmpeg.
        FFmpegPath = serverConfiguration.GetEncodingOptions().EncoderAppPathDisplay;

        Intros = new Dictionary<Guid, Intro>();
        AnalysisQueue = new Dictionary<Guid, List<QueuedEpisode>>();
        Instance = this;

        RestoreTimestamps();
    }

    /// <summary>
    /// Gets the results of fingerprinting all episodes.
    /// </summary>
    public Dictionary<Guid, Intro> Intros { get; }

    /// <summary>
    /// Gets the mapping of season ids to episodes that have been queued for fingerprinting.
    /// </summary>
    public Dictionary<Guid, List<QueuedEpisode>> AnalysisQueue { get; }

    /// <summary>
    /// Gets or sets the total number of episodes in the queue.
    /// </summary>
    public int TotalQueued { get; set; }

    /// <summary>
    /// Gets the directory to cache fingerprints in.
    /// </summary>
    public string FingerprintCachePath { get; private set; }

    /// <summary>
    /// Gets the full path to FFmpeg.
    /// </summary>
    public string FFmpegPath { get; private set; }

    /// <inheritdoc />
    public override string Name => "Intro Skipper";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("c83d86bb-a1e0-4c35-a113-e2101cf4ee6b");

    /// <summary>
    /// Gets the plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Save timestamps to disk.
    /// </summary>
    public void SaveTimestamps()
    {
        var introList = new List<Intro>();

        foreach (var intro in Plugin.Instance!.Intros)
        {
            introList.Add(intro.Value);
        }

        _xmlSerializer.SerializeToFile(introList, _introPath);
    }

    /// <summary>
    /// Restore previous analysis results from disk.
    /// </summary>
    public void RestoreTimestamps()
    {
        if (!File.Exists(_introPath))
        {
            return;
        }

        // Since dictionaries can't be easily serialized, analysis results are stored on disk as a list.
        var introList = (List<Intro>)_xmlSerializer.DeserializeFromFile(typeof(List<Intro>), _introPath);

        foreach (var intro in introList)
        {
            Plugin.Instance!.Intros[intro.EpisodeId] = intro;
        }
    }

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
