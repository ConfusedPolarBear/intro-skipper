using System;
using System.Collections.Generic;
using System.Net.Mime;
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
    /// Returns the timestamps of the introduction in a television episode.
    /// </summary>
    /// <param name="id">ID of the episode. Required.</param>
    /// <response code="200">Episode contains an intro.</response>
    /// <response code="404">Failed to find an intro in the provided episode.</response>
    /// <returns>Detected intro.</returns>
    [HttpGet("Episode/{id}/IntroTimestamps")]
    public ActionResult<Intro> GetIntroTimestamps([FromRoute] Guid id)
    {
        if (!Plugin.Instance!.Intros.ContainsKey(id))
        {
            return NotFound();
        }

        var intro = Plugin.Instance!.Intros[id];

        // Check that the episode was analyzed successfully.
        if (!intro.Valid)
        {
            return NotFound();
        }

        // Populate the prompt show/hide times.
        var config = Plugin.Instance!.Configuration;
        intro.ShowSkipPromptAt = Math.Max(0, intro.IntroStart - config.ShowPromptAdjustment);
        intro.HideSkipPromptAt = intro.IntroStart + config.HidePromptAdjustment;

        return intro;
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
    /// <returns>Dictionary of Intro objects.</returns>
    [Authorize(Policy = "RequiresElevation")]
    [HttpGet("Intros/All")]
    public ActionResult<Dictionary<Guid, Intro>> GetAllIntros()
    {
        return Plugin.Instance!.Intros;
    }
}
