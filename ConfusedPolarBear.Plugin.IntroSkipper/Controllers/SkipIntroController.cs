using System;
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
    /// Constructor.
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
}
