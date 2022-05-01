using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using Jellyfin.Data.Entities;
using Jellyfin.Extensions.Json;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
    /// <param name="episodeId">ID of the episode. Required.</param>
    /// <response code="200">Episode contains an intro.</response>
    /// <response code="404">Failed to find an intro in the provided episode.</response>
    [HttpGet("Episode/{id}/IntroTimestamps")]
    public ActionResult<Intro> GetIntroTimestamps([FromRoute] Guid episodeId)
    {
        if (!Plugin.Instance!.Intros.ContainsKey(episodeId))
        {
            return NotFound();
        }

        var intro = Plugin.Instance!.Intros[episodeId];

        // Check that the episode was analyzed successfully.
        if (!intro.Valid)
        {
            return NotFound();
        }

        return intro;
    }
}
