using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Library;
using Microsoft.Extensions.Logging;

namespace ConfusedPolarBear.Plugin.IntroSkipper;

/// <summary>
/// Server entrypoint.
/// </summary>
public class Entrypoint : IServerEntryPoint
{
    private readonly IUserManager _userManager;
    private readonly IUserViewManager _userViewManager;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<Entrypoint> _logger;

    private readonly object _queueLock = new object();

    /// <summary>
    /// Initializes a new instance of the <see cref="Entrypoint"/> class.
    /// </summary>
    /// <param name="userManager">User manager.</param>
    /// <param name="userViewManager">User view manager.</param>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="logger">Logger.</param>
    public Entrypoint(
        IUserManager userManager,
        IUserViewManager userViewManager,
        ILibraryManager libraryManager,
        ILogger<Entrypoint> logger)
    {
        _userManager = userManager;
        _userViewManager = userViewManager;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <summary>
    /// Registers event handler.
    /// </summary>
    /// <returns>Task.</returns>
    public Task RunAsync()
    {
        FPCalc.Logger = _logger;

        // Assert that fpcalc is installed
        if (!FPCalc.CheckFPCalcInstalled())
        {
            _logger.LogError("fpcalc is not installed on this system - episodes will not be analyzed");
            return Task.CompletedTask;
        }

        // As soon as a new episode is added, queue it for later analysis.
        _libraryManager.ItemAdded += ItemAdded;

        // For all TV show libraries, enqueue all contained items.
        foreach (var folder in _libraryManager.GetVirtualFolders())
        {
            if (folder.CollectionType != CollectionTypeOptions.TvShows)
            {
                continue;
            }

            _logger.LogInformation(
                "Running startup enqueue of items in library {Name} ({ItemId})",
                folder.Name,
                folder.ItemId);

            QueueLibraryContents(folder.ItemId);
        }

        return Task.CompletedTask;
    }

    private void QueueLibraryContents(string rawId)
    {
        // FIXME: don't do this

        var query = new UserViewQuery()
        {
            UserId = GetAdministrator(),
        };

        // Get all items from this library. Since intros may change within a season, sort the items before adding them.
        var folder = _userViewManager.GetUserViews(query)[0];
        var items = folder.GetItems(new InternalItemsQuery()
        {
            ParentId = Guid.Parse(rawId),
            OrderBy = new[] { ("SortName", SortOrder.Ascending) },
            IncludeItemTypes = new BaseItemKind[] { BaseItemKind.Episode },
            Recursive = true,
        });

        // Queue all episodes on the server for fingerprinting.
        foreach (var item in items.Items)
        {
            if (item is not Episode episode)
            {
                _logger.LogError("Item {Name} is not an episode", item.Name);
                continue;
            }

            QueueEpisode(episode);
        }
    }

    /// <summary>
    /// Called when an item is added to the server.
    /// </summary>
    /// <param name="sender">Sender.</param>
    /// <param name="e">ItemChangeEventArgs.</param>
    private void ItemAdded(object? sender, ItemChangeEventArgs e)
    {
        if (e.Item is not Episode episode)
        {
            return;
        }

        _logger.LogDebug("Queuing fingerprint of new episode {Name}", episode.Name);

        QueueEpisode(episode);
    }

    private void QueueEpisode(Episode episode)
    {
        if (Plugin.Instance is null)
        {
            throw new InvalidOperationException("plugin instance was null");
        }

        lock (_queueLock)
        {
            var queue = Plugin.Instance.AnalysisQueue;

            // Allocate a new list for each new season
            if (!queue.ContainsKey(episode.SeasonId))
            {
                Plugin.Instance.AnalysisQueue[episode.SeasonId] = new List<QueuedEpisode>();
            }

            // Only fingerprint up to 25% of the episode and at most 10 minutes.
            var duration = TimeSpan.FromTicks(episode.RunTimeTicks ?? 0).TotalSeconds;
            if (duration >= 5 * 60)
            {
                duration /= 4;
            }

            duration = Math.Min(duration, 10 * 60);

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

    /// <summary>
    /// FIXME: don't do this.
    /// </summary>
    private Guid GetAdministrator()
    {
        foreach (var user in _userManager.Users)
        {
            if (!user.HasPermission(Jellyfin.Data.Enums.PermissionKind.IsAdministrator))
            {
                continue;
            }

            _logger.LogDebug("Accessing libraries as {Username}", user.Username);
            return user.Id;
        }

        throw new FingerprintException("Unable to find an administrator on this server.");
    }

    /// <summary>
    /// Dispose.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected dispose.
    /// </summary>
    /// <param name="dispose">Dispose.</param>
    protected virtual void Dispose(bool dispose)
    {
        if (!dispose)
        {
            return;
        }

        _libraryManager.ItemAdded -= ItemAdded;
    }
}
