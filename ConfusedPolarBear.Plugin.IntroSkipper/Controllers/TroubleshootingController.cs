using System.Net.Mime;
using System.Text;
using MediaBrowser.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConfusedPolarBear.Plugin.IntroSkipper.Controllers;

/// <summary>
/// Troubleshooting controller.
/// </summary>
[Authorize(Policy = "RequiresElevation")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[Route("IntroSkipper")]
public class TroubleshootingController : ControllerBase
{
    private readonly IApplicationHost _applicationHost;
    private readonly ILogger<TroubleshootingController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TroubleshootingController"/> class.
    /// </summary>
    /// <param name="applicationHost">Application host.</param>
    /// <param name="logger">Logger.</param>
    public TroubleshootingController(
        IApplicationHost applicationHost,
        ILogger<TroubleshootingController> logger)
    {
        _applicationHost = applicationHost;
        _logger = logger;
    }

    /// <summary>
    /// Gets a Markdown formatted support bundle.
    /// </summary>
    /// <response code="200">Support bundle created.</response>
    /// <returns>Support bundle.</returns>
    [HttpGet("SupportBundle")]
    [Produces(MediaTypeNames.Text.Plain)]
    public ActionResult<string> GetSupportBundle()
    {
        var bundle = new StringBuilder();

        bundle.Append("* Jellyfin version: ");
        bundle.Append(_applicationHost.ApplicationVersionString);
        bundle.Append('\n');

        bundle.Append("* Plugin version: ");
        bundle.Append(Plugin.Instance!.Version.ToString(3));
        bundle.Append('\n');

        bundle.Append("* Queue contents: ");
        bundle.Append(Plugin.Instance!.TotalQueued);
        bundle.Append(" episodes, ");
        bundle.Append(Plugin.Instance!.AnalysisQueue.Count);
        bundle.Append(" seasons");
        bundle.Append('\n');

        bundle.Append("* Warnings: `");
        bundle.Append(WarningManager.GetWarnings());
        bundle.Append("`\n");

        bundle.Append(FFmpegWrapper.GetChromaprintLogs());

        return bundle.ToString();
    }
}
