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

        var queue = queueManager.EnqueueAllEpisodes();

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

                AnalyzeSeason(episodes, cancellationToken);
                Interlocked.Add(ref totalProcessed, episodes.Count);
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

            var total = Plugin.Instance!.TotalQueued;
            if (total > 0)
            {
                progress.Report((totalProcessed * 100) / total);
            }
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Analyzes all episodes in the season for end credits.
    /// </summary>
    /// <param name="episodes">Episodes in this season.</param>
    /// <param name="cancellationToken">Cancellation token provided by the scheduled task.</param>
    private void AnalyzeSeason(
        ReadOnlyCollection<QueuedEpisode> episodes,
        CancellationToken cancellationToken)
    {
        // Only analyze specials (season 0) if the user has opted in.
        if (episodes[0].SeasonNumber == 0 && !Plugin.Instance!.Configuration.AnalyzeSeasonZero)
        {
            return;
        }

        // Analyze with Chromaprint first and fall back to the black frame detector
        var analyzers = new IMediaFileAnalyzer[]
        {
            new ChromaprintAnalyzer(_loggerFactory.CreateLogger<ChromaprintAnalyzer>()),
            new BlackFrameAnalyzer(_loggerFactory.CreateLogger<BlackFrameAnalyzer>())
        };

        // Use each analyzer to find credits in all media files, removing successfully analyzed files
        // from the queue.
        var remaining = new ReadOnlyCollection<QueuedEpisode>(episodes);
        foreach (var analyzer in analyzers)
        {
            remaining = AnalyzeFiles(remaining, analyzer, cancellationToken);
        }
    }

    private ReadOnlyCollection<QueuedEpisode> AnalyzeFiles(
        ReadOnlyCollection<QueuedEpisode> episodes,
        IMediaFileAnalyzer analyzer,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Analyzing {Count} episodes from {Name} season {Season} with {Analyzer}",
            episodes.Count,
            episodes[0].SeriesName,
            episodes[0].SeasonNumber,
            analyzer.GetType().Name);

        return analyzer.AnalyzeMediaFiles(episodes, AnalysisMode.Credits, cancellationToken);
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
