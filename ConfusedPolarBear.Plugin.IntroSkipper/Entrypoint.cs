using System;
using System.IO;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
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
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="Entrypoint"/> class.
    /// </summary>
    /// <param name="userManager">User manager.</param>
    /// <param name="userViewManager">User view manager.</param>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    public Entrypoint(
        IUserManager userManager,
        IUserViewManager userViewManager,
        ILibraryManager libraryManager,
        ILogger<Entrypoint> logger,
        ILoggerFactory loggerFactory)
    {
        _userManager = userManager;
        _userViewManager = userViewManager;
        _libraryManager = libraryManager;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Registers event handler.
    /// </summary>
    /// <returns>Task.</returns>
    public Task RunAsync()
    {
        FFmpegWrapper.Logger = _logger;

#if DEBUG
        LogVersion();
#endif

        // TODO: when a new item is added to the server, immediately analyze the season it belongs to
        // instead of waiting for the next task interval. The task start should be debounced by a few seconds.

        try
        {
            // Enqueue all episodes at startup to ensure any FFmpeg errors appear as early as possible
            _logger.LogInformation("Running startup enqueue");
            var queueManager = new QueueManager(_loggerFactory.CreateLogger<QueueManager>(), _libraryManager);
            queueManager.GetMediaItems();
        }
        catch (Exception ex)
        {
            _logger.LogError("Unable to run startup enqueue: {Exception}", ex);
        }

        return Task.CompletedTask;
    }

#if DEBUG
    /// <summary>
    /// Logs the exact commit that created this version of the plugin. Only used in unstable builds.
    /// </summary>
    private void LogVersion()
    {
        var assembly = GetType().Assembly;
        var path = GetType().Namespace + ".Configuration.version.txt";

        using (var stream = assembly.GetManifestResourceStream(path))
        {
            if (stream is null)
            {
                _logger.LogWarning("Unable to read embedded version information");
                return;
            }

            var version = string.Empty;
            using (var reader = new StreamReader(stream))
            {
                version = reader.ReadToEnd().TrimEnd();
            }

            if (version == "unknown")
            {
                _logger.LogTrace("Embedded version information was not valid, ignoring");
                return;
            }

            _logger.LogInformation("Unstable version built from commit {Version}", version);
        }
    }
#endif

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
    }
}
