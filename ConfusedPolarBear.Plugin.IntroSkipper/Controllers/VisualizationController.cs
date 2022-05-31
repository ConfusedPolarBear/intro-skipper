using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConfusedPolarBear.Plugin.IntroSkipper.Controllers;

/// <summary>
/// Audio fingerprint visualization controller. Allows browsing fingerprints on a per episode basis.
/// </summary>
[Authorize(Policy = "RequiresElevation")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[Route("Intros")]
public class VisualizationController : ControllerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VisualizationController"/> class.
    /// </summary>
    public VisualizationController()
    {
    }

    /// <summary>
    /// Returns all show names and seasons.
    /// </summary>
    /// <returns>Dictionary of show names to a list of season names.</returns>
    [HttpGet("Shows")]
    public ActionResult<Dictionary<string, HashSet<string>>> GetShowSeasons()
    {
        var showSeasons = new Dictionary<string, HashSet<string>>();

        // Loop through all episodes in the analysis queue
        foreach (var episodes in Plugin.Instance!.AnalysisQueue)
        {
            foreach (var episode in episodes.Value)
            {
                // Add each season's name to the series hashset
                var series = episode.SeriesName;

                if (!showSeasons.ContainsKey(series))
                {
                    showSeasons[series] = new HashSet<string>();
                }

                showSeasons[series].Add(GetSeasonName(episode));
            }
        }

        return showSeasons;
    }

    /// <summary>
    /// Returns the names and unique identifiers of all episodes in the provided season.
    /// </summary>
    /// <param name="series">Show name.</param>
    /// <param name="season">Season name.</param>
    /// <returns>List of episode titles.</returns>
    [HttpGet("Show/{Series}/{Season}")]
    public ActionResult<List<EpisodeVisualization>> GetSeasonEpisodes(
        [FromRoute] string series,
        [FromRoute] string season)
    {
        var episodes = new List<EpisodeVisualization>();

        foreach (var queuedEpisodes in Plugin.Instance!.AnalysisQueue)
        {
            var first = queuedEpisodes.Value[0];
            var firstSeasonName = GetSeasonName(first);

            // Assert that the queued episode series and season are equal to what was requested
            if (
                !string.Equals(first.SeriesName, series, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(firstSeasonName, season, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var queuedEpisode in queuedEpisodes.Value)
            {
                episodes.Add(new EpisodeVisualization(queuedEpisode.EpisodeId, queuedEpisode.Name));
            }
        }

        return episodes;
    }

    /// <summary>
    /// Fingerprint the provided episode and returns the uncompressed fingerprint data points.
    /// </summary>
    /// <param name="id">Episode id.</param>
    /// <returns>Read only collection of fingerprint points.</returns>
    [HttpGet("Fingerprint/{Id}")]
    public ActionResult<ReadOnlyCollection<uint>> GetEpisodeFingerprint([FromRoute] Guid id)
    {
        var queue = Plugin.Instance!.AnalysisQueue;

        // Search through all queued episodes to find the requested id
        foreach (var season in queue)
        {
            foreach (var needle in season.Value)
            {
                if (needle.EpisodeId == id)
                {
                    return FPCalc.Fingerprint(needle);
                }
            }
        }

        return NotFound();
    }

    private string GetSeasonName(QueuedEpisode episode)
    {
        return "Season " + episode.SeasonNumber.ToString(CultureInfo.InvariantCulture);
    }
}
