using System;
using System.Collections.Generic;
using System.IO;
using ConfusedPolarBear.Plugin.IntroSkipper.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace ConfusedPolarBear.Plugin.IntroSkipper;

/// <summary>
/// Intro skipper plugin. Uses audio analysis to find common sequences of audio shared between episodes.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly object _serializationLock = new();
    private readonly object _introsLock = new();
    private IXmlSerializer _xmlSerializer;
    private ILibraryManager _libraryManager;
    private ILogger<Plugin> _logger;
    private string _introPath;
    private string _creditsPath;  // TODO: FIXME: remove this

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    /// <param name="serverConfiguration">Server configuration manager.</param>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="logger">Logger.</param>
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        IServerConfigurationManager serverConfiguration,
        ILibraryManager libraryManager,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;

        _xmlSerializer = xmlSerializer;
        _libraryManager = libraryManager;
        _logger = logger;

        FingerprintCachePath = Path.Join(applicationPaths.PluginConfigurationsPath, "intros", "cache");
        FFmpegPath = serverConfiguration.GetEncodingOptions().EncoderAppPathDisplay;
        _introPath = Path.Join(applicationPaths.PluginConfigurationsPath, "intros", "intros.xml");

        // TODO: FIXME: remove this
        _creditsPath = Path.Join(applicationPaths.PluginConfigurationsPath, "intros", "credits.csv");

        // Create the base & cache directories (if needed).
        if (!Directory.Exists(FingerprintCachePath))
        {
            Directory.CreateDirectory(FingerprintCachePath);
        }

        ConfigurationChanged += OnConfigurationChanged;

        // TODO: remove when https://github.com/jellyfin/jellyfin-meta/discussions/30 is complete
        try
        {
            RestoreTimestamps();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Unable to load introduction timestamps: {Exception}", ex);
        }

        // TODO: FIXME: remove this
        if (File.Exists(_creditsPath))
        {
            File.Delete(_creditsPath);
        }
    }

    /// <summary>
    /// Fired after configuration has been saved so the auto skip timer can be stopped or started.
    /// </summary>
    public event EventHandler? AutoSkipChanged;

    /// <summary>
    /// Gets the results of fingerprinting all episodes.
    /// </summary>
    public Dictionary<Guid, Intro> Intros { get; } = new();

    /// <summary>
    /// Gets the mapping of season ids to episodes that have been queued for fingerprinting.
    /// </summary>
    public Dictionary<Guid, List<QueuedEpisode>> AnalysisQueue { get; } = new();

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
        lock (_serializationLock)
        {
            var introList = new List<Intro>();

            foreach (var intro in Plugin.Instance!.Intros)
            {
                introList.Add(intro.Value);
            }

            _xmlSerializer.SerializeToFile(introList, _introPath);
        }
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

    internal BaseItem GetItem(Guid id)
    {
        return _libraryManager.GetItemById(id);
    }

    /// <summary>
    /// Gets the full path for an item.
    /// </summary>
    /// <param name="id">Item id.</param>
    /// <returns>Full path to item.</returns>
    internal string GetItemPath(Guid id)
    {
        return GetItem(id).Path;
    }

    internal void UpdateTimestamps(Dictionary<Guid, Intro> newIntros, AnalysisMode mode)
    {
        switch (mode)
        {
            case AnalysisMode.Introduction:
                lock (_introsLock)
                {
                    foreach (var intro in newIntros)
                    {
                        Plugin.Instance!.Intros[intro.Key] = intro.Value;
                    }

                    Plugin.Instance!.SaveTimestamps();
                }

                break;

            case AnalysisMode.Credits:
                // TODO: FIXME: implement properly

                lock (_introsLock)
                {
                    foreach (var credit in newIntros)
                    {
                        var item = GetItem(credit.Value.EpisodeId) as Episode;
                        if (item is null)
                        {
                            continue;
                        }

                        // Format: series, season number, episode number, title, start, end
                        var contents = string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "{0},{1},{2},{3},{4},{5}\n",
                            item.SeriesName.Replace(",", string.Empty, StringComparison.Ordinal),
                            item.AiredSeasonNumber ?? 0,
                            item.IndexNumber ?? 0,
                            item.Name.Replace(",", string.Empty, StringComparison.Ordinal),
                            Math.Round(credit.Value.IntroStart, 2),
                            Math.Round(credit.Value.IntroEnd, 2));

                        File.AppendAllText(_creditsPath, contents);
                    }
                }

                break;
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
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
            },
            new PluginPageInfo
            {
                Name = "visualizer.js",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.visualizer.js"
            }
        };
    }

    private void OnConfigurationChanged(object? sender, BasePluginConfiguration e)
    {
        AutoSkipChanged?.Invoke(this, EventArgs.Empty);
    }
}
