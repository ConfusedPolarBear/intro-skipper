using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace ConfusedPolarBear.Plugin.IntroSkipper;

/// <summary>
/// Fingerprint and analyze all queued episodes for common audio sequences.
/// </summary>
public class FingerprinterTask : IScheduledTask
{
    /// <summary>
    /// Maximum number of bits (out of 32 total) that can be different between segments before they are considered dissimilar.
    /// 6 bits means the audio must be at least 81% similar (1 - 6 / 32).
    /// </summary>
    private const double MaximumDifferences = 6;

    /// <summary>
    /// Maximum time (in seconds) permitted between timestamps before they are considered non-contiguous.
    /// </summary>
    private const double MaximumDistance = 3.5;

    /// <summary>
    /// Seconds of audio in one fingerprint point. This value is defined by the Chromaprint library and should not be changed.
    /// </summary>
    private const double SamplesToSeconds = 0.128;

    /// <summary>
    /// Bucket size used in the reanalysis histogram.
    /// </summary>
    private const int ReanalysisBucketWidth = 5;

    /// <summary>
    /// Maximum time (in seconds) that an intro's duration can be different from a typical intro's duration before marking it for reanalysis.
    /// </summary>
    private const double ReanalysisTolerance = ReanalysisBucketWidth * 1.5;

    private readonly ILogger<FingerprinterTask> _logger;

    private readonly ILogger<QueueManager> _queueLogger;

    private readonly ILibraryManager? _libraryManager;

    /// <summary>
    /// Lock which guards the fingerprint cache dictionary.
    /// </summary>
    private readonly object _fingerprintCacheLock = new object();

    /// <summary>
    /// Lock which guards the shared dictionary of intros.
    /// </summary>
    private readonly object _introsLock = new object();

    /// <summary>
    /// Temporary fingerprint cache to speed up reanalysis.
    /// Fingerprints are removed from this after a season is analyzed.
    /// </summary>
    private Dictionary<Guid, ReadOnlyCollection<uint>> _fingerprintCache;

    /// <summary>
    /// Statistics for the currently running analysis task.
    /// </summary>
    private AnalysisStatistics analysisStatistics = new AnalysisStatistics();

    /// <summary>
    /// Minimum duration of similar audio that will be considered an introduction.
    /// </summary>
    private static int minimumIntroDuration = 15;

    /// <summary>
    /// Initializes a new instance of the <see cref="FingerprinterTask"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory.</param>
    /// <param name="libraryManager">Library manager.</param>
    public FingerprinterTask(ILoggerFactory loggerFactory, ILibraryManager libraryManager) : this(loggerFactory)
    {
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FingerprinterTask"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory.</param>
    public FingerprinterTask(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<FingerprinterTask>();
        _queueLogger = loggerFactory.CreateLogger<QueueManager>();

        _fingerprintCache = new Dictionary<Guid, ReadOnlyCollection<uint>>();

        EdlManager.Initialize(_logger);
    }

    /// <summary>
    /// Gets the task name.
    /// </summary>
    public string Name => "Analyze episodes";

    /// <summary>
    /// Gets the task category.
    /// </summary>
    public string Category => "Intro Skipper";

    /// <summary>
    /// Gets the task description.
    /// </summary>
    public string Description => "Analyzes the audio of all television episodes to find introduction sequences.";

    /// <summary>
    /// Gets the task key.
    /// </summary>
    public string Key => "CPBIntroSkipperRunFingerprinter";

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
        var queueManager = new QueueManager(_queueLogger, _libraryManager);
        queueManager.EnqueueAllEpisodes();

        var queue = Plugin.Instance!.AnalysisQueue;

        if (queue.Count == 0)
        {
            throw new FingerprintException(
                "No episodes to analyze. If you are limiting the list of libraries to analyze, check that all library names have been spelled correctly.");
        }

        // Log EDL settings
        EdlManager.LogConfiguration();

        // Include the previously processed episodes in the percentage reported to the UI.
        var totalProcessed = CountProcessedEpisodes();
        var options = new ParallelOptions()
        {
            MaxDegreeOfParallelism = Plugin.Instance!.Configuration.MaxParallelism
        };

        var taskStart = DateTime.Now;
        analysisStatistics = new AnalysisStatistics();
        analysisStatistics.TotalQueuedEpisodes = Plugin.Instance!.TotalQueued;

        minimumIntroDuration = Plugin.Instance!.Configuration.MinimumIntroDuration;

        // Analyze all episodes in the queue using the degrees of parallelism the user specified.
        Parallel.ForEach(queue, options, (season) =>
        {
            var workerStart = DateTime.Now;
            var first = season.Value[0];
            var writeEdl = false;

            try
            {
                // Increment totalProcessed by the number of episodes in this season that were actually analyzed
                // (instead of just using the number of episodes in the current season).
                var analyzed = AnalyzeSeason(season, cancellationToken);
                Interlocked.Add(ref totalProcessed, analyzed);
                writeEdl = analyzed > 0 || Plugin.Instance!.Configuration.RegenerateEdlFiles;
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

            // Clear this season's episodes from the temporary fingerprint cache.
            lock (_fingerprintCacheLock)
            {
                foreach (var ep in season.Value)
                {
                    _fingerprintCache.Remove(ep.EpisodeId);
                }
            }

            if (writeEdl && Plugin.Instance!.Configuration.EdlAction != EdlAction.None)
            {
                EdlManager.UpdateEDLFiles(season.Value.AsReadOnly());
            }

            progress.Report((totalProcessed * 100) / Plugin.Instance!.TotalQueued);

            analysisStatistics.TotalCPUTime.AddDuration(workerStart);
            Plugin.Instance!.AnalysisStatistics = analysisStatistics;
        });

        // Update analysis statistics
        analysisStatistics.TotalTaskTime.AddDuration(taskStart);
        Plugin.Instance!.AnalysisStatistics = analysisStatistics;

        // Turn the regenerate EDL flag off after the scan completes.
        if (Plugin.Instance!.Configuration.RegenerateEdlFiles)
        {
            _logger.LogInformation("Turning EDL file regeneration flag off");
            Plugin.Instance!.Configuration.RegenerateEdlFiles = false;
            Plugin.Instance!.SaveConfiguration();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Count the number of previously processed episodes to ensure the reported progress is correct.
    /// </summary>
    /// <returns>Number of previously processed episodes.</returns>
    private int CountProcessedEpisodes()
    {
        var previous = 0;

        foreach (var season in Plugin.Instance!.AnalysisQueue)
        {
            foreach (var episode in season.Value)
            {
                if (!Plugin.Instance!.Intros.TryGetValue(episode.EpisodeId, out var intro) || !intro.Valid)
                {
                    continue;
                }

                previous++;
            }
        }

        return previous;
    }

    /// <summary>
    /// Fingerprints all episodes in the provided season and stores the timestamps of all introductions.
    /// </summary>
    /// <param name="season">Pairing of season GUID to a list of QueuedEpisode objects.</param>
    /// <param name="cancellationToken">Cancellation token provided by the scheduled task.</param>
    /// <returns>Number of episodes from the provided season that were analyzed.</returns>
    private int AnalyzeSeason(
        KeyValuePair<Guid, List<QueuedEpisode>> season,
        CancellationToken cancellationToken)
    {
        var seasonIntros = new Dictionary<Guid, Intro>();
        var episodes = season.Value;
        var first = episodes[0];

        /* Don't analyze specials or seasons with an insufficient number of episodes.
         * A season with only 1 episode can't be analyzed as it would compare the episode to itself,
         * which would result in the entire episode being marked as an introduction, as the audio is identical.
         */
        if (season.Value.Count < 2 || first.SeasonNumber == 0)
        {
            return episodes.Count;
        }

        var unanalyzed = false;

        // Only log an analysis message if there are unanalyzed episodes in this season.
        foreach (var episode in episodes)
        {
            if (!Plugin.Instance!.Intros.ContainsKey(episode.EpisodeId))
            {
                unanalyzed = true;
                break;
            }
        }

        if (unanalyzed)
        {
            _logger.LogInformation(
                "Analyzing {Count} episodes from {Name} season {Season}",
                season.Value.Count,
                first.SeriesName,
                first.SeasonNumber);
        }
        else
        {
            _logger.LogDebug(
                "All episodes from {Name} season {Season} have already been analyzed",
                first.SeriesName,
                first.SeasonNumber);

            return 0;
        }

        // Ensure there are an even number of episodes
        if (episodes.Count % 2 != 0)
        {
            episodes.Add(episodes[episodes.Count - 2]);
        }

        // Analyze each pair of episodes in the current season
        var everFoundIntro = false;
        var failures = 0;
        for (var i = 0; i < episodes.Count; i += 2)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var lhs = episodes[i];
            var rhs = episodes[i + 1];

            if (!everFoundIntro && failures >= 20)
            {
                _logger.LogWarning(
                    "Failed to find an introduction in {Series} season {Season}",
                    lhs.SeriesName,
                    lhs.SeasonNumber);

                break;
            }

            if (Plugin.Instance!.Intros.ContainsKey(lhs.EpisodeId) && Plugin.Instance!.Intros.ContainsKey(rhs.EpisodeId))
            {
                _logger.LogTrace(
                    "Episodes {LHS} and {RHS} have both already been fingerprinted",
                    lhs.EpisodeId,
                    rhs.EpisodeId);

                continue;
            }

            try
            {
                _logger.LogTrace("Analyzing {LHS} and {RHS}", lhs.Path, rhs.Path);

                var (lhsIntro, rhsIntro) = FingerprintEpisodes(lhs, rhs);
                seasonIntros[lhsIntro.EpisodeId] = lhsIntro;
                seasonIntros[rhsIntro.EpisodeId] = rhsIntro;
                analysisStatistics.TotalAnalyzedEpisodes.Add(2);

                if (!lhsIntro.Valid)
                {
                    failures += 2;
                    continue;
                }

                everFoundIntro = true;
            }
            catch (FingerprintException ex)
            {
                _logger.LogError("Caught fingerprint error: {Ex}", ex);
            }
        }

        // Ensure only one thread at a time can update the shared intro dictionary.
        lock (_introsLock)
        {
            foreach (var intro in seasonIntros)
            {
                Plugin.Instance!.Intros[intro.Key] = intro.Value;
            }
        }

        // Only run the second pass if the user hasn't requested cancellation and we found an intro
        if (!cancellationToken.IsCancellationRequested && everFoundIntro)
        {
            var start = DateTime.Now;

            // Run a second pass over this season to remove outliers and fix episodes that failed in the first pass.
            RunSecondPass(season.Value);

            analysisStatistics.SecondPassCPUTime.AddDuration(start);
        }

        lock (_introsLock)
        {
            Plugin.Instance!.SaveTimestamps();
        }

        return episodes.Count;
    }

    /// <summary>
    /// Analyze two episodes to find an introduction sequence shared between them.
    /// </summary>
    /// <param name="lhsEpisode">First episode to analyze.</param>
    /// <param name="rhsEpisode">Second episode to analyze.</param>
    /// <returns>Intros for the first and second episodes.</returns>
    public (Intro Lhs, Intro Rhs) FingerprintEpisodes(QueuedEpisode lhsEpisode, QueuedEpisode rhsEpisode)
    {
        var start = DateTime.Now;
        var lhsFingerprint = Chromaprint.Fingerprint(lhsEpisode);
        var rhsFingerprint = Chromaprint.Fingerprint(rhsEpisode);
        analysisStatistics.FingerprintCPUTime.AddDuration(start);

        // Cache the fingerprints for quicker recall in the second pass (if one is needed).
        lock (_fingerprintCacheLock)
        {
            _fingerprintCache[lhsEpisode.EpisodeId] = lhsFingerprint;
            _fingerprintCache[rhsEpisode.EpisodeId] = rhsFingerprint;
        }

        return FingerprintEpisodes(
            lhsEpisode.EpisodeId,
            lhsFingerprint,
            rhsEpisode.EpisodeId,
            rhsFingerprint,
            true);
    }

    /// <summary>
    /// Analyze two episodes to find an introduction sequence shared between them.
    /// </summary>
    /// <param name="lhsId">First episode id.</param>
    /// <param name="lhsPoints">First episode fingerprint points.</param>
    /// <param name="rhsId">Second episode id.</param>
    /// <param name="rhsPoints">Second episode fingerprint points.</param>
    /// <param name="isFirstPass">If this was called as part of the first analysis pass, add the elapsed time to the statistics.</param>
    /// <returns>Intros for the first and second episodes.</returns>
    public (Intro Lhs, Intro Rhs) FingerprintEpisodes(
        Guid lhsId,
        ReadOnlyCollection<uint> lhsPoints,
        Guid rhsId,
        ReadOnlyCollection<uint> rhsPoints,
        bool isFirstPass)
    {
        // If this isn't running as part of the first analysis pass, don't count this CPU time as first pass time.
        var start = isFirstPass ? DateTime.Now : DateTime.MinValue;

        // ===== Method 1: Inverted indexes =====
        // Creates an inverted fingerprint point index for both episodes.
        // For every point which is a 100% match, search for an introduction at that point.
        var (lhsRanges, rhsRanges) = SearchInvertedIndex(lhsPoints, rhsPoints);

        if (lhsRanges.Count > 0)
        {
            _logger.LogTrace("Index search successful");
            analysisStatistics.IndexSearches.Increment();
            analysisStatistics.FirstPassCPUTime.AddDuration(start);

            return GetLongestTimeRange(lhsId, lhsRanges, rhsId, rhsRanges);
        }

        // ===== Method 2: Quick scan =====
        // Tests if an intro can be found within the first 5 seconds of the episodes. ±5/0.128 = ±40 samples.
        (lhsRanges, rhsRanges) = ShiftEpisodes(lhsPoints, rhsPoints, -40, 40);

        if (lhsRanges.Count > 0)
        {
            _logger.LogTrace("Quick scan successful");
            analysisStatistics.QuickScans.Increment();
            analysisStatistics.FirstPassCPUTime.AddDuration(start);

            return GetLongestTimeRange(lhsId, lhsRanges, rhsId, rhsRanges);
        }

        // ===== Method 3: Full scan =====
        // Compares all elements of the shortest fingerprint to the other fingerprint.
        var limit = Math.Min(lhsPoints.Count, rhsPoints.Count);
        (lhsRanges, rhsRanges) = ShiftEpisodes(lhsPoints, rhsPoints, -1 * limit, limit);

        if (lhsRanges.Count > 0)
        {
            _logger.LogTrace("Full scan successful");
            analysisStatistics.FullScans.Increment();
            analysisStatistics.FirstPassCPUTime.AddDuration(start);

            return GetLongestTimeRange(lhsId, lhsRanges, rhsId, rhsRanges);
        }

        // No method was able to find an introduction, return nothing.

        _logger.LogTrace(
            "Unable to find a shared introduction sequence between {LHS} and {RHS}",
            lhsId,
            rhsId);

        analysisStatistics.FirstPassCPUTime.AddDuration(start);

        return (new Intro(lhsId), new Intro(rhsId));
    }

    /// <summary>
    /// Locates the longest range of similar audio and returns an Intro class for each range.
    /// </summary>
    /// <param name="lhsId">First episode id.</param>
    /// <param name="lhsRanges">First episode shared timecodes.</param>
    /// <param name="rhsId">Second episode id.</param>
    /// <param name="rhsRanges">Second episode shared timecodes.</param>
    /// <returns>Intros for the first and second episodes.</returns>
    private (Intro Lhs, Intro Rhs) GetLongestTimeRange(
        Guid lhsId,
        List<TimeRange> lhsRanges,
        Guid rhsId,
        List<TimeRange> rhsRanges)
    {
        // Store the longest time range as the introduction.
        lhsRanges.Sort();
        rhsRanges.Sort();

        var lhsIntro = lhsRanges[0];
        var rhsIntro = rhsRanges[0];

        // If the intro starts early in the episode, move it to the beginning.
        if (lhsIntro.Start <= 5)
        {
            lhsIntro.Start = 0;
        }

        if (rhsIntro.Start <= 5)
        {
            rhsIntro.Start = 0;
        }

        // Create Intro classes for each time range.
        return (new Intro(lhsId, lhsIntro), new Intro(rhsId, rhsIntro));
    }

    /// <summary>
    /// Search for a shared introduction sequence using inverted indexes.
    /// </summary>
    /// <param name="lhsPoints">Left episode fingerprint points.</param>
    /// <param name="rhsPoints">Right episode fingerprint points.</param>
    /// <returns>List of shared TimeRanges between the left and right episodes.</returns>
    private (List<TimeRange> Lhs, List<TimeRange> Rhs) SearchInvertedIndex(
        ReadOnlyCollection<uint> lhsPoints,
        ReadOnlyCollection<uint> rhsPoints)
    {
        var lhsRanges = new List<TimeRange>();
        var rhsRanges = new List<TimeRange>();

        // Generate inverted indexes for the left and right episodes.
        var lhsIndex = Chromaprint.CreateInvertedIndex(lhsPoints);
        var rhsIndex = Chromaprint.CreateInvertedIndex(rhsPoints);
        var indexShifts = new HashSet<int>();

        // For all audio points in the left episode, check if the right episode has a point which matches exactly.
        // If an exact match is found, calculate the shift that must be used to align the points.
        foreach (var kvp in lhsIndex)
        {
            var point = kvp.Key;

            if (rhsIndex.ContainsKey(point))
            {
                var lhsFirst = (int)lhsIndex[point];
                var rhsFirst = (int)rhsIndex[point];
                indexShifts.Add(rhsFirst - lhsFirst);
            }
        }

        // Use all discovered shifts to compare the episodes.
        foreach (var shift in indexShifts)
        {
            var (lhsIndexContiguous, rhsIndexContiguous) = ShiftEpisodes(lhsPoints, rhsPoints, shift, shift);
            lhsRanges.AddRange(lhsIndexContiguous);
            rhsRanges.AddRange(rhsIndexContiguous);
        }

        return (lhsRanges, rhsRanges);
    }

    /// <summary>
    /// Shifts a pair of episodes through the range of provided shift amounts and returns discovered contiguous time ranges.
    /// </summary>
    /// <param name="lhs">First episode fingerprint.</param>
    /// <param name="rhs">Second episode fingerprint.</param>
    /// <param name="lower">Lower end of the shift range.</param>
    /// <param name="upper">Upper end of the shift range.</param>
    private static (List<TimeRange> Lhs, List<TimeRange> Rhs) ShiftEpisodes(
        ReadOnlyCollection<uint> lhs,
        ReadOnlyCollection<uint> rhs,
        int lower,
        int upper)
    {
        var lhsRanges = new List<TimeRange>();
        var rhsRanges = new List<TimeRange>();

        for (int amount = lower; amount <= upper; amount++)
        {
            var (lRange, rRange) = FindContiguous(lhs, rhs, amount);

            if (lRange.End == 0 && rRange.End == 0)
            {
                continue;
            }

            lhsRanges.Add(lRange);
            rhsRanges.Add(rRange);
        }

        return (lhsRanges, rhsRanges);
    }

    /// <summary>
    /// Finds the longest contiguous region of similar audio between two fingerprints using the provided shift amount.
    /// </summary>
    /// <param name="lhs">First fingerprint to compare.</param>
    /// <param name="rhs">Second fingerprint to compare.</param>
    /// <param name="shiftAmount">Amount to shift one fingerprint by.</param>
    private static (TimeRange Lhs, TimeRange Rhs) FindContiguous(
        ReadOnlyCollection<uint> lhs,
        ReadOnlyCollection<uint> rhs,
        int shiftAmount)
    {
        var leftOffset = 0;
        var rightOffset = 0;

        // Calculate the offsets for the left and right hand sides.
        if (shiftAmount < 0)
        {
            leftOffset -= shiftAmount;
        }
        else
        {
            rightOffset += shiftAmount;
        }

        // Store similar times for both LHS and RHS.
        var lhsTimes = new List<double>();
        var rhsTimes = new List<double>();
        var upperLimit = Math.Min(lhs.Count, rhs.Count) - Math.Abs(shiftAmount);

        // XOR all elements in LHS and RHS, using the shift amount from above.
        for (var i = 0; i < upperLimit; i++)
        {
            // XOR both samples at the current position.
            var lhsPosition = i + leftOffset;
            var rhsPosition = i + rightOffset;
            var diff = lhs[lhsPosition] ^ rhs[rhsPosition];

            // If the difference between the samples is small, flag both times as similar.
            if (CountBits(diff) > MaximumDifferences)
            {
                continue;
            }

            var lhsTime = lhsPosition * SamplesToSeconds;
            var rhsTime = rhsPosition * SamplesToSeconds;

            lhsTimes.Add(lhsTime);
            rhsTimes.Add(rhsTime);
        }

        // Ensure the last timestamp is checked
        lhsTimes.Add(double.MaxValue);
        rhsTimes.Add(double.MaxValue);

        // Now that both fingerprints have been compared at this shift, see if there's a contiguous time range.
        var lContiguous = TimeRangeHelpers.FindContiguous(lhsTimes.ToArray(), MaximumDistance);
        if (lContiguous is null || lContiguous.Duration < minimumIntroDuration)
        {
            return (new TimeRange(), new TimeRange());
        }

        // Since LHS had a contiguous time range, RHS must have one also.
        var rContiguous = TimeRangeHelpers.FindContiguous(rhsTimes.ToArray(), MaximumDistance)!;

        // Tweak the end timestamps just a bit to ensure as little content as possible is skipped over.
        if (lContiguous.Duration >= 90)
        {
            lContiguous.End -= 2 * MaximumDistance;
            rContiguous.End -= 2 * MaximumDistance;
        }
        else if (lContiguous.Duration >= 30)
        {
            lContiguous.End -= MaximumDistance;
            rContiguous.End -= MaximumDistance;
        }

        return (lContiguous, rContiguous);
    }

    /// <summary>
    /// Count the number of bits that are set in the provided number.
    /// </summary>
    /// <param name="number">Number to count bits in.</param>
    /// <returns>Number of bits that are equal to 1.</returns>
    public static int CountBits(uint number)
    {
        return BitOperations.PopCount(number);
    }

    /// <summary>
    /// Reanalyze the most recently analyzed season.
    /// Looks for and fixes intro durations that were either not found or are statistical outliers.
    /// </summary>
    /// <param name="episodes">List of episodes that was just analyzed.</param>
    private void RunSecondPass(List<QueuedEpisode> episodes)
    {
        // First, assert that at least half of the episodes in this season have an intro.
        var validCount = 0;
        var totalCount = episodes.Count;

        foreach (var episode in episodes)
        {
            if (Plugin.Instance!.Intros.TryGetValue(episode.EpisodeId, out var intro) && intro.Valid)
            {
                validCount++;
            }
        }

        var percentValid = (validCount * 100) / totalCount;
        _logger.LogTrace("Found intros in {Valid}/{Total} ({Percent}%) of episodes", validCount, totalCount, percentValid);
        if (percentValid < 50)
        {
            return;
        }

        // Create a histogram of all episode durations
        var histogram = new Dictionary<int, SeasonHistogram>();
        foreach (var episode in episodes)
        {
            var id = episode.EpisodeId;
            var duration = GetIntroDuration(id);

            if (duration < minimumIntroDuration)
            {
                continue;
            }

            // Bucket the duration into equally sized groups
            var bucket = Convert.ToInt32(Math.Floor(duration / ReanalysisBucketWidth)) * ReanalysisBucketWidth;

            // TryAdd returns true when the key was successfully added (i.e. for newly created buckets).
            // Newly created buckets are initialized with the provided episode ID, so nothing else needs to be done for them.
            if (histogram.TryAdd(bucket, new SeasonHistogram(id)))
            {
                continue;
            }

            histogram[bucket].Episodes.Add(id);
        }

        // Find the bucket that was seen most often, as this is likely to be the true intro length.
        var maxDuration = 0;
        var maxBucket = new SeasonHistogram(Guid.Empty);
        foreach (var entry in histogram)
        {
            if (entry.Value.Count > maxBucket.Count)
            {
                maxDuration = entry.Key;
                maxBucket = entry.Value;
            }
        }

        // Ensure that the most frequently seen bucket has a majority
        percentValid = (maxBucket.Count * 100) / validCount;
        _logger.LogTrace(
            "Intro duration {Duration} appeared {Frequency} times ({Percent}%)",
            maxDuration,
            maxBucket.Count,
            percentValid);

        if (percentValid < 50 || maxBucket.Episodes[0].Equals(Guid.Empty))
        {
            return;
        }

        _logger.LogTrace("Second pass is processing {Count} episodes", totalCount - maxBucket.Count);

        // Calculate a range of intro durations that are most likely to be correct.
        var maxEpisode = episodes.Find(x => x.EpisodeId == maxBucket.Episodes[0]);
        if (maxEpisode is null)
        {
            _logger.LogError("Second pass failed to get episode from bucket");
            return;
        }

        var lhsDuration = GetIntroDuration(maxEpisode.EpisodeId);
        var (lowTargetDuration, highTargetDuration) = (
            lhsDuration - ReanalysisTolerance,
            lhsDuration + ReanalysisTolerance);

        // TODO: add limit and make it customizable
        var count = maxBucket.Episodes.Count - 1;
        var goodFingerprints = new List<ReadOnlyCollection<uint>>();
        foreach (var id in maxBucket.Episodes)
        {
            if (!_fingerprintCache.TryGetValue(id, out var fp))
            {
                _logger.LogTrace("Second pass: max bucket episode {Id} not in cache, skipping", id);
                continue;
            }

            goodFingerprints.Add(fp);
        }

        foreach (var episode in episodes)
        {
            // Don't reanalyze episodes from the max bucket
            if (maxBucket.Episodes.Contains(episode.EpisodeId))
            {
                continue;
            }

            var oldDuration = GetIntroDuration(episode.EpisodeId);

            // If the episode's intro duration is close enough to the targeted bucket, leave it alone.
            if (Math.Abs(lhsDuration - oldDuration) <= ReanalysisTolerance)
            {
                _logger.LogTrace(
                    "Not reanalyzing episode {Path} (intro is {Initial}, target is {Max})",
                    episode.Path,
                    Math.Round(oldDuration, 2),
                    maxDuration);

                continue;
            }

            _logger.LogTrace(
                "Reanalyzing episode {Path} (intro is {Initial}, target is {Max})",
                episode.Path,
                Math.Round(oldDuration, 2),
                maxDuration);

            // Analyze the episode again, ignoring whatever is returned for the known good episode.
            foreach (var lhsFingerprint in goodFingerprints)
            {
                if (!_fingerprintCache.TryGetValue(episode.EpisodeId, out var fp))
                {
                    _logger.LogTrace("Unable to get cached fingerprint for {Id}, skipping", episode.EpisodeId);
                    continue;
                }

                var (_, newRhs) = FingerprintEpisodes(
                    maxEpisode.EpisodeId,
                    lhsFingerprint,
                    episode.EpisodeId,
                    fp,
                    false);

                // Ensure that the new intro duration is within the targeted bucket and longer than what was found previously.
                var newDuration = Math.Round(newRhs.IntroEnd - newRhs.IntroStart, 2);
                if (newDuration < oldDuration || newDuration < lowTargetDuration || newDuration > highTargetDuration)
                {
                    _logger.LogTrace(
                        "Ignoring reanalysis for {Path} (was {Initial}, now is {New})",
                        episode.Path,
                        oldDuration,
                        newDuration);

                    continue;
                }

                _logger.LogTrace(
                    "Reanalysis succeeded for {Path} (was {Initial}, now is {New})",
                    episode.Path,
                    oldDuration,
                    newDuration);

                lock (_introsLock)
                {
                    Plugin.Instance!.Intros[episode.EpisodeId] = newRhs;
                }

                break;
            }
        }
    }

    private double GetIntroDuration(Guid id)
    {
        if (!Plugin.Instance!.Intros.TryGetValue(id, out var episode))
        {
            return 0;
        }

        return episode.Valid ? Math.Round(episode.IntroEnd - episode.IntroStart, 2) : 0;
    }

    /// <summary>
    /// Get task triggers.
    /// </summary>
    /// <returns>Task triggers.</returns>
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerDaily,
                TimeOfDayTicks = TimeSpan.FromDays(24).Ticks
            }
        };
    }
}
