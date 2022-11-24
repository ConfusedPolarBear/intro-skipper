namespace ConfusedPolarBear.Plugin.IntroSkipper;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;
using MediaBrowser.Model.Entities;

/// <summary>
/// Chapter name analyzer.
/// </summary>
public class ChapterAnalyzer : IMediaFileAnalyzer
{
    private ILogger<ChapterAnalyzer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChapterAnalyzer"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public ChapterAnalyzer(ILogger<ChapterAnalyzer> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public ReadOnlyCollection<QueuedEpisode> AnalyzeMediaFiles(
        ReadOnlyCollection<QueuedEpisode> analysisQueue,
        AnalysisMode mode,
        CancellationToken cancellationToken)
    {
        var skippableRanges = new Dictionary<Guid, Intro>();
        var expression = mode == AnalysisMode.Introduction ?
            Plugin.Instance!.Configuration.ChapterAnalyzerIntroductionPattern :
            Plugin.Instance!.Configuration.ChapterAnalyzerEndCreditsPattern;

        foreach (var episode in analysisQueue)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var skipRange = FindMatchingChapter(
                episode.EpisodeId,
                episode.Duration,
                new(Plugin.Instance!.GetChapters(episode.EpisodeId)),
                expression,
                mode);

            if (skipRange is null)
            {
                continue;
            }

            skippableRanges.Add(episode.EpisodeId, skipRange);
        }

        Plugin.Instance!.UpdateTimestamps(skippableRanges, mode);

        return analysisQueue;
    }

    /// <summary>
    /// Searches a list of chapter names for one that matches the provided regular expression.
    /// Only public to allow for unit testing.
    /// </summary>
    /// <param name="id">Item id.</param>
    /// <param name="duration">Duration of media file in seconds.</param>
    /// <param name="chapters">Media item chapters.</param>
    /// <param name="expression">Regular expression pattern.</param>
    /// <param name="mode">Analysis mode.</param>
    /// <returns>Intro object containing skippable time range, or null if no chapter matched.</returns>
    public Intro? FindMatchingChapter(
        Guid id,
        int duration,
        Collection<ChapterInfo> chapters,
        string expression,
        AnalysisMode mode)
    {
        Intro? matchingChapter = null;

        var config = Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration();
        var minDuration = config.MinimumIntroDuration;
        int maxDuration = mode == AnalysisMode.Introduction ?
            config.MaximumIntroDuration :
            config.MaximumEpisodeCreditsDuration;

        if (mode == AnalysisMode.Credits)
        {
            // Since the ending credits chapter may be the last chapter in the file, append a virtual
            // chapter at the very end of the file.
            chapters.Add(new ChapterInfo()
            {
                StartPositionTicks = TimeSpan.FromSeconds(duration).Ticks
            });
        }

        // Check all chapters
        for (int i = 0; i < chapters.Count - 1; i++)
        {
            // Calculate chapter position and duration
            var current = chapters[i];
            var next = chapters[i + 1];

            var currentRange = new TimeRange(
                TimeSpan.FromTicks(current.StartPositionTicks).TotalSeconds,
                TimeSpan.FromTicks(next.StartPositionTicks).TotalSeconds);

            // Skip chapters with that don't have a name or are too short/long
            if (string.IsNullOrEmpty(current.Name) ||
                currentRange.Duration < minDuration ||
                currentRange.Duration > maxDuration)
            {
                continue;
            }

            // Regex.IsMatch() is used here in order to allow the runtime to cache the compiled regex
            // between function invocations.
            var match = Regex.IsMatch(
                current.Name,
                expression,
                RegexOptions.None,
                TimeSpan.FromSeconds(1));

            if (!match)
            {
                continue;
            }

            matchingChapter = new Intro(id, currentRange);
            break;
        }

        return matchingChapter;
    }
}
