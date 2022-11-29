using System;
using System.Collections.Generic;
using System.Net.Mime;
using MediaBrowser.Controller.Entities.TV;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConfusedPolarBear.Plugin.IntroSkipper.Controllers;

/// <summary>
/// Skip intro controller.
/// </summary>
[Authorize]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
public class SkipIntroController : ControllerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SkipIntroController"/> class.
    /// </summary>
    public SkipIntroController()
    {
    }

    /// <summary>
    /// Returns the timestamps of the introduction in a television episode. Responses are in API version 1 format.
    /// </summary>
    /// <param name="id">ID of the episode. Required.</param>
    /// <param name="mode">Timestamps to return. Optional. Defaults to Introduction for backwards compatibility.</param>
    /// <response code="200">Episode contains an intro.</response>
    /// <response code="404">Failed to find an intro in the provided episode.</response>
    /// <returns>Detected intro.</returns>
    [HttpGet("Episode/{id}/IntroTimestamps")]
    [HttpGet("Episode/{id}/IntroTimestamps/v1")]
    public ActionResult<Intro> GetIntroTimestamps(
        [FromRoute] Guid id,
        [FromQuery] AnalysisMode mode = AnalysisMode.Introduction)
    {
        var intro = GetIntro(id, mode);

        if (intro is null || !intro.Valid)
        {
            return NotFound();
        }

        // Populate the prompt show/hide times.
        var config = Plugin.Instance!.Configuration;
        intro.ShowSkipPromptAt = Math.Max(0, intro.IntroStart - config.ShowPromptAdjustment);
        intro.HideSkipPromptAt = intro.IntroStart + config.HidePromptAdjustment;
        intro.IntroEnd -= config.SecondsOfIntroToPlay;

        return intro;
    }

    /// <summary>Lookup and return the skippable timestamps for the provided item.</summary>
    /// <param name="id">Unique identifier of this episode.</param>
    /// <param name="mode">Mode.</param>
    /// <returns>Intro object if the provided item has an intro, null otherwise.</returns>
    private Intro? GetIntro(Guid id, AnalysisMode mode)
    {
        try
        {
            var timestamp = mode == AnalysisMode.Introduction ?
                Plugin.Instance!.Intros[id] :
                Plugin.Instance!.Credits[id];

            // A copy is returned to avoid mutating the original Intro object stored in the dictionary.
            return new(timestamp);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// Erases all previously discovered introduction timestamps.
    /// </summary>
    /// <param name="mode">Mode.</param>
    /// <response code="204">Operation successful.</response>
    /// <returns>No content.</returns>
    [Authorize(Policy = "RequiresElevation")]
    [HttpPost("Intros/EraseTimestamps")]
    public ActionResult ResetIntroTimestamps([FromQuery] AnalysisMode mode)
    {
        if (mode == AnalysisMode.Introduction)
        {
            Plugin.Instance!.Intros.Clear();
        }
        else if (mode == AnalysisMode.Credits)
        {
            Plugin.Instance!.Credits.Clear();
        }

        Plugin.Instance!.SaveTimestamps();
        return NoContent();
    }

    /// <summary>
    /// Get all introductions or credits. Only used by the end to end testing script.
    /// </summary>
    /// <param name="mode">Mode.</param>
    /// <response code="200">All timestamps have been returned.</response>
    /// <returns>List of IntroWithMetadata objects.</returns>
    [Authorize(Policy = "RequiresElevation")]
    [HttpGet("Intros/All")]
    public ActionResult<List<IntroWithMetadata>> GetAllTimestamps(
        [FromQuery] AnalysisMode mode = AnalysisMode.Introduction)
    {
        List<IntroWithMetadata> intros = new();

        var timestamps = mode == AnalysisMode.Introduction ?
            Plugin.Instance!.Intros :
            Plugin.Instance!.Credits;

        // Get metadata for all intros
        foreach (var intro in timestamps)
        {
            // Get the details of the item from Jellyfin
            var rawItem = Plugin.Instance!.GetItem(intro.Key);
            if (rawItem is not Episode episode)
            {
                throw new InvalidCastException("Unable to cast item id " + intro.Key + " to an Episode");
            }

            // Associate the metadata with the intro
            intros.Add(
                new IntroWithMetadata(
                episode.SeriesName,
                episode.AiredSeasonNumber ?? 0,
                episode.Name,
                intro.Value));
        }

        return intros;
    }
}
