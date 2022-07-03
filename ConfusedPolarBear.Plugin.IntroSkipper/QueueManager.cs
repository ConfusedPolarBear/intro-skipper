namespace ConfusedPolarBear.Plugin.IntroSkipper;

using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueManager"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="libraryManager">Library manager.</param>
    public QueueManager(ILogger<QueueManager> logger, ILibraryManager libraryManager)
    {
        _logger = logger;
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Iterates through all libraries on the server and queues all episodes for analysis.
    /// </summary>
    public void EnqueueAllEpisodes()
    {
        // Assert that ffmpeg with chromaprint is installed
        if (!Chromaprint.CheckFFmpegVersion())
        {
            throw new FingerprintException(
                "ffmpeg with chromaprint is not installed on this system - episodes will not be analyzed. If Jellyfin is running natively, install jellyfin-ffmpeg5. If Jellyfin is running in a container, upgrade it to the latest version of 10.8.0.");
        }

        Plugin.Instance!.AnalysisQueue.Clear();

        // If analysis settings have been changed from the default, log the modified settings.
        var config = Plugin.Instance!.Configuration;
        analysisPercent = Convert.ToDouble(config.AnalysisPercent) / 100;

        if (config.AnalysisLengthLimit != 10 || config.AnalysisPercent != 25 || config.MinimumIntroDuration != 15)
        {
            _logger.LogDebug(
                "Analysis settings have been changed to: {Percent}%/{Minutes}m and a minimum of {Minimum}s",
                config.AnalysisPercent,
                config.AnalysisLengthLimit,
                config.MinimumIntroDuration);
        }

        // For all TV show libraries, enqueue all contained items.
        foreach (var folder in _libraryManager.GetVirtualFolders())
        {
            if (folder.CollectionType != CollectionTypeOptions.TvShows)
            {
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
            _logger.LogWarning("Not queuing episode {Id} as no path was provided by Jellyfin", episode.Id);
            return;
        }

        var queue = Plugin.Instance.AnalysisQueue;

        // Allocate a new list for each new season
        if (!queue.ContainsKey(episode.SeasonId))
        {
            Plugin.Instance.AnalysisQueue[episode.SeasonId] = new List<QueuedEpisode>();
        }

        var config = Plugin.Instance!.Configuration;

        // Limit analysis to the first X% of the episode and at most Y minutes.
        // X and Y default to 25% and 10 minutes.
        var duration = TimeSpan.FromTicks(episode.RunTimeTicks ?? 0).TotalSeconds;
        if (duration >= 5 * 60)
        {
            duration *= analysisPercent;
        }

        duration = Math.Min(duration, 60 * config.AnalysisLengthLimit);

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
