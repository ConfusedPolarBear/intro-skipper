namespace ConfusedPolarBear.Plugin.IntroSkipper;

using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

/// <summary>
/// Manages enqueuing library items for analysis.
/// </summary>
public class QueueManager
{
    private ILibraryManager _libraryManager;
    private ILogger<QueueManager> _logger;

    private double analysisPercent;
    private IList<string> selectedLibraries;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueManager"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="libraryManager">Library manager.</param>
    public QueueManager(ILogger<QueueManager> logger, ILibraryManager libraryManager)
    {
        _logger = logger;
        _libraryManager = libraryManager;

        selectedLibraries = new List<string>();
    }

    /// <summary>
    /// Iterates through all libraries on the server and queues all episodes for analysis.
    /// </summary>
    public void EnqueueAllEpisodes()
    {
        // Assert that ffmpeg with chromaprint is installed
        if (!FFmpegWrapper.CheckFFmpegVersion())
        {
            throw new FingerprintException(
                "ffmpeg with chromaprint is not installed on this system - episodes will not be analyzed. If Jellyfin is running natively, install jellyfin-ffmpeg5. If Jellyfin is running in a container, upgrade it to the latest version of 10.8.0.");
        }

        Plugin.Instance!.AnalysisQueue.Clear();
        Plugin.Instance!.TotalQueued = 0;

        LoadAnalysisSettings();

        // For all selected TV show libraries, enqueue all contained items.
        foreach (var folder in _libraryManager.GetVirtualFolders())
        {
            if (folder.CollectionType != CollectionTypeOptions.TvShows)
            {
                _logger.LogDebug("Not analyzing library \"{Name}\": not a TV show library", folder.Name);
                continue;
            }

            // If libraries have been selected for analysis, ensure this library was selected.
            if (selectedLibraries.Count > 0 && !selectedLibraries.Contains(folder.Name))
            {
                _logger.LogDebug("Not analyzing library \"{Name}\": not selected by user", folder.Name);
                continue;
            }

            _logger.LogInformation(
                "Running enqueue of items in library {Name} ({ItemId})",
                folder.Name,
                folder.ItemId);

            try
            {
                QueueLibraryContents(folder.ItemId);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to enqueue items from library {Name}: {Exception}", folder.Name, ex);
            }
        }
    }

    /// <summary>
    /// Loads the list of libraries which have been selected for analysis and the minimum intro duration.
    /// Settings which have been modified from the defaults are logged.
    /// </summary>
    private void LoadAnalysisSettings()
    {
        var config = Plugin.Instance!.Configuration;

        // Store the analysis percent
        analysisPercent = Convert.ToDouble(config.AnalysisPercent) / 100;

        // Get the list of library names which have been selected for analysis, ignoring whitespace and empty entries.
        selectedLibraries = config.SelectedLibraries
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        // If any libraries have been selected for analysis, log their names.
        if (selectedLibraries.Count > 0)
        {
            _logger.LogInformation("Limiting analysis to the following libraries: {Selected}", selectedLibraries);
        }
        else
        {
            _logger.LogDebug("Not limiting analysis by library name");
        }

        // If analysis settings have been changed from the default, log the modified settings.
        if (config.AnalysisLengthLimit != 10 || config.AnalysisPercent != 25 || config.MinimumIntroDuration != 15)
        {
            _logger.LogInformation(
                "Analysis settings have been changed to: {Percent}%/{Minutes}m and a minimum of {Minimum}s",
                config.AnalysisPercent,
                config.AnalysisLengthLimit,
                config.MinimumIntroDuration);
        }
    }

    private void QueueLibraryContents(string rawId)
    {
        _logger.LogDebug("Constructing anonymous internal query");

        var query = new InternalItemsQuery()
        {
            // Order by series name, season, and then episode number so that status updates are logged in order
            ParentId = Guid.Parse(rawId),
            OrderBy = new[]
            {
                ("SeriesSortName", SortOrder.Ascending),
                ("ParentIndexNumber", SortOrder.Ascending),
                ("IndexNumber", SortOrder.Ascending),
            },
            IncludeItemTypes = new BaseItemKind[] { BaseItemKind.Episode },
            Recursive = true,
            IsVirtualItem = false
        };

        _logger.LogDebug("Getting items");

        var items = _libraryManager.GetItemList(query, false);

        if (items is null)
        {
            _logger.LogError("Library query result is null");
            return;
        }

        // Queue all episodes on the server for fingerprinting.
        _logger.LogDebug("Iterating through library items");

        foreach (var item in items)
        {
            if (item is not Episode episode)
            {
                _logger.LogError("Item {Name} is not an episode", item.Name);
                continue;
            }

            QueueEpisode(episode);
        }

        _logger.LogDebug("Queued {Count} episodes", items.Count);
    }

    private void QueueEpisode(Episode episode)
    {
        if (Plugin.Instance is null)
        {
            throw new InvalidOperationException("plugin instance was null");
        }

        if (string.IsNullOrEmpty(episode.Path))
        {
            _logger.LogWarning(
                "Not queuing episode \"{Name}\" from series \"{Series}\" ({Id}) as no path was provided by Jellyfin",
                episode.Name,
                episode.SeriesName,
                episode.Id);
            return;
        }

        // Limit analysis to the first X% of the episode and at most Y minutes.
        // X and Y default to 25% and 10 minutes.
        var duration = TimeSpan.FromTicks(episode.RunTimeTicks ?? 0).TotalSeconds;
        if (duration >= 5 * 60)
        {
            duration *= analysisPercent;
        }

        duration = Math.Min(duration, 60 * Plugin.Instance!.Configuration.AnalysisLengthLimit);

        // Allocate a new list for each new season
        Plugin.Instance!.AnalysisQueue.TryAdd(episode.SeasonId, new List<QueuedEpisode>());

        // Queue the episode for analysis
        Plugin.Instance.AnalysisQueue[episode.SeasonId].Add(new QueuedEpisode()
        {
            SeriesName = episode.SeriesName,
            SeasonNumber = episode.AiredSeasonNumber ?? 0,
            EpisodeId = episode.Id,
            Name = episode.Name,
            Path = episode.Path,
            FingerprintDuration = Convert.ToInt32(duration)
        });

        Plugin.Instance!.TotalQueued++;
    }
}
