using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ConfusedPolarBear.Plugin.IntroSkipper.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
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

        FFmpegPath = serverConfiguration.GetEncodingOptions().EncoderAppPathDisplay;

        var introsDirectory = Path.Join(applicationPaths.PluginConfigurationsPath, "intros");
        FingerprintCachePath = Path.Join(introsDirectory, "cache");
        _introPath = Path.Join(introsDirectory, "intros.xml");

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

        // Inject the skip intro button code into the web interface.
        var indexPath = Path.Join(applicationPaths.WebPath, "index.html");
        try
        {
            InjectSkipButton(indexPath, Path.Join(introsDirectory, "index-pre-skip-button.html"));
        }
        catch (Exception ex)
        {
            // WarningManager.SetFlag(PluginWarning.UnableToAddSkipButton);

            if (ex is UnauthorizedAccessException)
            {
                var suggestion = OperatingSystem.IsLinux() ?
                    "running `sudo chown jellyfin PATH` (if this is a native installation)" :
                    "changing the permissions of PATH";

                suggestion = suggestion.Replace("PATH", indexPath, StringComparison.Ordinal);

                _logger.LogError(
                    "Failed to add skip button to web interface. Try {Suggestion} and restarting the server. Error: {Error}",
                    suggestion,
                    ex);
            }
            else
            {
                _logger.LogError("Unknown error encountered while adding skip button: {Error}", ex);
            }
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
            },
            new PluginPageInfo
            {
                Name = "skip-intro-button.js",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.inject.js"
            }
        };
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

    internal void UpdateTimestamps(Dictionary<Guid, Intro> newIntros)
    {
        lock (_introsLock)
        {
            foreach (var intro in newIntros)
            {
                Plugin.Instance!.Intros[intro.Key] = intro.Value;
            }

            Plugin.Instance!.SaveTimestamps();
        }
    }

    private void OnConfigurationChanged(object? sender, BasePluginConfiguration e)
    {
        AutoSkipChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Inject the skip button script into the web interface.
    /// </summary>
    /// <param name="indexPath">Full path to index.html.</param>
    /// <param name="backupPath">Full path to create a backup of index.html at.</param>
    private void InjectSkipButton(string indexPath, string backupPath)
    {
        // Parts of this code are based off of JellyScrub's script injection code.
        // https://github.com/nicknsy/jellyscrub/blob/4ce806f602988a662cfe3cdbaac35ee8046b7ec4/Nick.Plugin.Jellyscrub/JellyscrubPlugin.cs

        _logger.LogInformation("Adding skip button to {Path}", indexPath);

        _logger.LogDebug("Reading index.html from {Path}", indexPath);
        var contents = File.ReadAllText(indexPath);
        _logger.LogDebug("Successfully read index.html");

        var scriptTag = "<script src=\"configurationpage?name=skip-intro-button.js\"></script>";

        // Only inject the script tag once
        if (contents.Contains(scriptTag, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Skip button already added");
            return;
        }

        // Backup the original version of the web interface
        _logger.LogInformation("Backing up index.html to {Backup}", backupPath);
        File.WriteAllText(backupPath, contents);

        // Inject a link to the script at the end of the <head> section.
        // A regex is used here to ensure the replacement is only done once.
        _logger.LogDebug("Injecting script tag");
        var headEnd = new Regex("</head>", RegexOptions.IgnoreCase);
        contents = headEnd.Replace(contents, scriptTag + "</head>", 1);

        // Write the modified file contents
        _logger.LogDebug("Saving modified file");
        File.WriteAllText(indexPath, contents);

        _logger.LogInformation("Skip intro button successfully added to web interface");
    }
}
