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
    /// <response code="200">Episode contains an intro.</response>
    /// <response code="404">Failed to find an intro in the provided episode.</response>
    /// <returns>Detected intro.</returns>
    [HttpGet("Episode/{id}/IntroTimestamps")]
    [HttpGet("Episode/{id}/IntroTimestamps/v1")]
    public ActionResult<Intro> GetIntroTimestamps([FromRoute] Guid id)
    {
        var intro = GetIntro(id);

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

    /// <summary>Lookup and return the intro timestamps for the provided item.</summary>
    /// <param name="id">Unique identifier of this episode.</param>
    /// <returns>Intro object if the provided item has an intro, null otherwise.</returns>
    private Intro? GetIntro(Guid id)
    {
        return Plugin.Instance!.Intros.TryGetValue(id, out var intro) ? intro : null;
    }

    /// <summary>
    /// Erases all previously discovered introduction timestamps.
    /// </summary>
    /// <response code="204">Operation successful.</response>
    /// <returns>No content.</returns>
    [Authorize(Policy = "RequiresElevation")]
    [HttpPost("Intros/EraseTimestamps")]
    public ActionResult ResetIntroTimestamps()
    {
        Plugin.Instance!.Intros.Clear();
        Plugin.Instance!.SaveTimestamps();
        return NoContent();
    }

    /// <summary>
    /// Get all introductions. Only used by the end to end testing script.
    /// </summary>
    /// <response code="200">All introductions have been returned.</response>
    /// <returns>List of IntroWithMetadata objects.</returns>
    [Authorize(Policy = "RequiresElevation")]
    [HttpGet("Intros/All")]
    public ActionResult<List<IntroWithMetadata>> GetAllIntros()
    {
        List<IntroWithMetadata> intros = new();

        // Get metadata for all intros
        foreach (var intro in Plugin.Instance!.Intros)
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
