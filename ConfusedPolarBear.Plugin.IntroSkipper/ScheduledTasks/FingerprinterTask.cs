using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace ConfusedPolarBear.Plugin.IntroSkipper;

/// <summary>
/// Fingerprint and analyze all queued episodes for common audio sequences.
/// </summary>
public class FingerprinterTask : IScheduledTask
{
    /// <summary>
    /// Minimum time (in seconds) for a contiguous time range to be considered an introduction.
    /// </summary>
    private const int MinimumIntroDuration = 15;

    /// <summary>
    /// Maximum number of bits (out of 32 total) that can be different between segments before they are considered dissimilar.
    /// </summary>
    private const double MaximumDifferences = 3;

    /// <summary>
    /// Maximum time (in seconds) permitted between timestamps before they are considered non-contiguous.
    /// </summary>
    private const double MaximumDistance = 3.25;

    /// <summary>
    /// Seconds of audio in one fingerprint point. This value is defined by the Chromaprint library and should not be changed.
    /// </summary>
    private const double SamplesToSeconds = 0.128;

    private readonly ILogger<FingerprinterTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FingerprinterTask"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public FingerprinterTask(ILogger<FingerprinterTask> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the last detected intro sequence. Only populated when a unit test is running.
    /// </summary>
    public static Intro LastIntro { get; private set; } = new Intro();

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
    /// Analyze all episodes in the queue.
    /// </summary>
    /// <param name="progress">Task progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task.</returns>
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var queue = Plugin.Instance!.AnalysisQueue;
        var totalProcessed = 0;

        foreach (var season in queue)
        {
            var first = season.Value[0];

            /* Don't analyze specials or seasons with an insufficient number of episodes.
             * A season with only 1 episode can't be analyzed as it would compare the episode to itself,
             * which would result in the entire episode being marked as an introduction, as the audio is identical.
             */
            if (season.Value.Count < 2 || first.SeasonNumber == 0)
            {
                continue;
            }

            _logger.LogInformation(
                "Analyzing {Count} episodes from {Name} season {Season}",
                season.Value.Count,
                first.SeriesName,
                first.SeasonNumber);

            // Ensure there are an even number of episodes
            var episodes = season.Value;
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

                // TODO: make configurable
                if (!everFoundIntro && failures >= 6)
                {
                    _logger.LogWarning(
                        "Failed to find an introduction in {Series} season {Season}",
                        lhs.SeriesName,
                        lhs.SeasonNumber);

                    break;
                }

                // FIXME: add retry logic
                var alreadyDone = Plugin.Instance!.Intros;
                if (alreadyDone.ContainsKey(lhs.EpisodeId) && alreadyDone.ContainsKey(rhs.EpisodeId))
                {
                    _logger.LogDebug(
                        "Episodes {LHS} and {RHS} have both already been fingerprinted",
                        lhs.EpisodeId,
                        rhs.EpisodeId);

                    totalProcessed += 2;
                    progress.Report((totalProcessed * 100) / Plugin.Instance!.TotalQueued);

                    continue;
                }

                try
                {
                    _logger.LogDebug("Analyzing {LHS} and {RHS}", lhs.Path, rhs.Path);

                    if (FingerprintEpisodes(lhs, rhs))
                    {
                        everFoundIntro = true;
                    }
                    else
                    {
                        failures += 2;
                    }
                }
                catch (FingerprintException ex)
                {
                    _logger.LogError("Caught fingerprint error: {Ex}", ex);
                }
                finally
                {
                    totalProcessed += 2;
                    progress.Report((totalProcessed * 100) / Plugin.Instance!.TotalQueued);
                }
            }

            Plugin.Instance!.SaveTimestamps();

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Analyze two episodes to find an introduction sequence shared between them.
    /// </summary>
    /// <param name="lhsEpisode">First episode to analyze.</param>
    /// <param name="rhsEpisode">Second episode to analyze.</param>
    /// <returns>true if an intro was found in both episodes, otherwise false.</returns>
    public bool FingerprintEpisodes(QueuedEpisode lhsEpisode, QueuedEpisode rhsEpisode)
    {
        var lhs = FPCalc.Fingerprint(lhsEpisode);
        var rhs = FPCalc.Fingerprint(rhsEpisode);

        var lhsRanges = new List<TimeRange>();
        var rhsRanges = new List<TimeRange>();

        // Compare all elements of the shortest fingerprint to the other fingerprint.
        var limit = Math.Min(lhs.Count, rhs.Count);

        // First, test if an intro can be found within the first 5 seconds of the episodes (±5/0.128 = ±40 samples).
        var (lhsContiguous, rhsContiguous) = ShiftEpisodes(lhs, rhs, -40, 40);
        lhsRanges.AddRange(lhsContiguous);
        rhsRanges.AddRange(rhsContiguous);

        // If no valid ranges were found, re-analyze the episodes considering all possible shifts.
        if (lhsRanges.Count == 0)
        {
            _logger.LogDebug("using full scan");

            (lhsContiguous, rhsContiguous) = ShiftEpisodes(lhs, rhs, -1 * limit, limit);
            lhsRanges.AddRange(lhsContiguous);
            rhsRanges.AddRange(rhsContiguous);
        }
        else
        {
            _logger.LogDebug("intro found with quick scan");
        }

        if (lhsRanges.Count == 0)
        {
            _logger.LogDebug(
                "Unable to find a shared introduction sequence between {LHS} and {RHS}",
                lhsEpisode.Path,
                rhsEpisode.Path);

            // TODO: if an episode fails but others in the season succeed, reanalyze it against two that succeeded.

            StoreIntro(lhsEpisode.EpisodeId, 0, 0);
            StoreIntro(rhsEpisode.EpisodeId, 0, 0);

            return false;
        }

        // After comparing both episodes at all possible shift positions, store the longest time range as the intro.
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

        StoreIntro(lhsEpisode.EpisodeId, lhsIntro.Start, lhsIntro.End);
        StoreIntro(rhsEpisode.EpisodeId, rhsIntro.Start, rhsIntro.End);

        return true;
    }

    /// <summary>
    /// Shifts episodes through the range of provided shift amounts and returns discovered contiguous time ranges.
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
        if (lContiguous is null || lContiguous.Duration < MinimumIntroDuration)
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
        else if (lContiguous.Duration >= 35)
        {
            lContiguous.End -= MaximumDistance;
            rContiguous.End -= MaximumDistance;
        }

        return (lContiguous, rContiguous);
    }

    private static void StoreIntro(Guid episode, double introStart, double introEnd)
    {
        var intro = new Intro()
        {
            EpisodeId = episode,
            Valid = introEnd > 0,       // don't test introStart here as the intro could legitimately happen at the start.
            IntroStart = introStart,
            IntroEnd = introEnd
        };

        if (Plugin.Instance is null)
        {
            LastIntro = intro;
            return;
        }

        Plugin.Instance.Intros[episode] = intro;
    }

    /// <summary>
    /// Count the number of bits that are set in the provided number.
    /// </summary>
    /// <param name="number">Number to count bits in.</param>
    /// <returns>Number of bits that are equal to 1.</returns>
    public static int CountBits(uint number)
    {
        var count = 0;

        for (var i = 0; i < 32; i++)
        {
            var low = (number >> i) & 1;
            if (low == 1)
            {
                count++;
            }
        }

        return count;
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
