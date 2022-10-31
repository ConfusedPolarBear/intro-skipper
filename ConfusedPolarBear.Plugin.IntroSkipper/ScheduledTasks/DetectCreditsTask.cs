using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace ConfusedPolarBear.Plugin.IntroSkipper;

#if !DEBUG
#error Fix all FIXMEs introduced during initial credit implementation before release
#endif

/// <summary>
/// Analyze all television episodes for credits.
/// </summary>
public class DetectCreditsTask : IScheduledTask
{
    private readonly ILogger<DetectCreditsTask> _logger;

    private readonly ILoggerFactory _loggerFactory;

    private readonly ILibraryManager? _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="DetectCreditsTask"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory.</param>
    /// <param name="libraryManager">Library manager.</param>
    public DetectCreditsTask(
        ILoggerFactory loggerFactory,
        ILibraryManager libraryManager) : this(loggerFactory)
    {
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DetectCreditsTask"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory.</param>
    public DetectCreditsTask(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<DetectCreditsTask>();
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Gets the task name.
    /// </summary>
    public string Name => "Detect Credits";

    /// <summary>
    /// Gets the task category.
    /// </summary>
    public string Category => "Intro Skipper";

    /// <summary>
    /// Gets the task description.
    /// </summary>
    public string Description => "Analyzes the audio and video of all television episodes to find credits.";

    /// <summary>
    /// Gets the task key.
    /// </summary>
    public string Key => "CPBIntroSkipperDetectCredits";

    /// <summary>
    /// Analyze all episodes in the queue. Only one instance of this task should be run at a time.
    /// </summary>
    /// <param name="progress">Task progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task.</returns>
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (_libraryManager is null)
        {
            throw new InvalidOperationException("Library manager must not be null");
        }

        // Make sure the analysis queue matches what's currently in Jellyfin.
        var queueManager = new QueueManager(
            _loggerFactory.CreateLogger<QueueManager>(),
            _libraryManager);

        queueManager.EnqueueAllEpisodes();

        var queue = Plugin.Instance!.AnalysisQueue;

        if (queue.Count == 0)
        {
            throw new FingerprintException(
                "No episodes to analyze. If you are limiting the list of libraries to analyze, check that all library names have been spelled correctly.");
        }

        var totalProcessed = 0;
        var options = new ParallelOptions()
        {
            MaxDegreeOfParallelism = Plugin.Instance!.Configuration.MaxParallelism
        };

        // TODO: FIXME: if the queue is modified while the task is running, the task will fail.
        // clone the queue before running the task to prevent this.

        // Analyze all episodes in the queue using the degrees of parallelism the user specified.
        Parallel.ForEach(queue, options, (season) =>
        {
            // TODO: FIXME: use VerifyEpisodes
            var episodes = season.Value.AsReadOnly();
            if (episodes.Count == 0)
            {
                return;
            }

            var first = episodes[0];

            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // Increment totalProcessed by the number of episodes in this season that were actually analyzed
                // (instead of just using the number of episodes in the current season).
                var analyzed = AnalyzeSeason(episodes, cancellationToken);
                Interlocked.Add(ref totalProcessed, analyzed);
            }
            catch (FingerprintException ex)
            {
                _logger.LogWarning(
                    "Unable to analyze {Series} season {Season}: unable to fingerprint: {Ex}",
                    first.SeriesName,
                    first.SeasonNumber,
                    ex);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(
                    "Unable to analyze {Series} season {Season}: cache miss: {Ex}",
                    first.SeriesName,
                    first.SeasonNumber,
                    ex);
            }

            progress.Report((totalProcessed * 100) / Plugin.Instance!.TotalQueued);
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Fingerprints all episodes in the provided season and stores the timestamps of all introductions.
    /// </summary>
    /// <param name="episodes">Episodes in this season.</param>
    /// <param name="cancellationToken">Cancellation token provided by the scheduled task.</param>
    /// <returns>Number of episodes from the provided season that were analyzed.</returns>
    private int AnalyzeSeason(
        ReadOnlyCollection<QueuedEpisode> episodes,
        CancellationToken cancellationToken)
    {
        // Skip seasons with an insufficient number of episodes.
        if (episodes.Count <= 1)
        {
            return episodes.Count;
        }

        // Only analyze specials (season 0) if the user has opted in.
        var first = episodes[0];
        if (first.SeasonNumber == 0 && !Plugin.Instance!.Configuration.AnalyzeSeasonZero)
        {
            return 0;
        }

        _logger.LogInformation(
            "Analyzing {Count} episodes from {Name} season {Season}",
            episodes.Count,
            first.SeriesName,
            first.SeasonNumber);

        // Analyze the season with Chromaprint
        var chromaprint = new ChromaprintAnalyzer(_loggerFactory.CreateLogger<ChromaprintAnalyzer>());
        chromaprint.AnalyzeMediaFiles(episodes, AnalysisMode.Credits, cancellationToken);

        return episodes.Count;
    }

    /// <summary>
    /// Get task triggers.
    /// </summary>
    /// <returns>Task triggers.</returns>
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return Array.Empty<TaskTriggerInfo>();
    }
}
