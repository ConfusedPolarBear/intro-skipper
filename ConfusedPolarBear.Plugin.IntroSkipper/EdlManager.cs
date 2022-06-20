namespace ConfusedPolarBear.Plugin.IntroSkipper;

using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Extensions.Logging;

/// <summary>
/// Update EDL files associated with a list of episodes.
/// </summary>
public static class EdlManager
{
    private static ILogger? _logger;

    /// <summary>
    /// Initialize EDLManager with a logger.
    /// </summary>
    /// <param name="logger">ILogger.</param>
    public static void Initialize(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// If the EDL action is set to a value other than None, update EDL files for the provided episodes.
    /// </summary>
    /// <param name="episodes">Episodes to update EDL files for.</param>
    public static void UpdateEDLFiles(ReadOnlyCollection<QueuedEpisode> episodes)
    {
        var overwrite = Plugin.Instance!.Configuration.OverwriteExistingEdlFiles;
        var action = Plugin.Instance!.Configuration.EdlAction;
        if (action == EdlAction.None)
        {
            _logger?.LogDebug("EDL action is set to none, not updating EDL files");
            return;
        }

        _logger?.LogDebug("Updating EDL files with action {Action}", action);

        foreach (var episode in episodes)
        {
            var id = episode.EpisodeId;
            var intro = Plugin.Instance!.Intros[id];
            var edlPath = GetEdlPath(Plugin.Instance!.GetItemPath(id));

            _logger?.LogTrace("Episode {Id} has EDL path {Path}", id, edlPath);

            if (!overwrite && File.Exists(edlPath))
            {
                _logger?.LogTrace("Refusing to overwrite existing EDL file {Path}", edlPath);
                continue;
            }

            File.WriteAllText(edlPath, intro.ToEdl(action));
        }
    }

    /// <summary>
    /// Given the path to an episode, return the path to the associated EDL file.
    /// </summary>
    /// <param name="mediaPath">Full path to episode.</param>
    /// <returns>Full path to EDL file.</returns>
    public static string GetEdlPath(string mediaPath)
    {
        return Path.ChangeExtension(mediaPath, "edl");
    }
}
