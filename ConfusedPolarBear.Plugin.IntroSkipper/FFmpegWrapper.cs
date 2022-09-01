using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ConfusedPolarBear.Plugin.IntroSkipper;

/// <summary>
/// Wrapper for libchromaprint and the silencedetect filter.
/// </summary>
public static class FFmpegWrapper
{
    // FFmpeg logs lines similar to the following:
    // [silencedetect @ 0x000000000000] silence_start: 12.34
    // [silencedetect @ 0x000000000000] silence_end: 56.123 | silence_duration: 43.783

    /// <summary>
    /// Used with FFmpeg's silencedetect filter to extract the start and end times of silence.
    /// </summary>
    private static readonly Regex SilenceDetectionExpression = new(
        "silence_(?<type>start|end): (?<time>[0-9\\.]+)");

    /// <summary>
    /// Gets or sets the logger.
    /// </summary>
    public static ILogger? Logger { get; set; }

    private static Dictionary<string, string> ChromaprintLogs { get; set; } = new();

    private static Dictionary<Guid, Dictionary<uint, int>> InvertedIndexCache { get; set; } = new();

    /// <summary>
    /// Check that the installed version of ffmpeg supports chromaprint.
    /// </summary>
    /// <returns>true if a compatible version of ffmpeg is installed, false on any error.</returns>
    public static bool CheckFFmpegVersion()
    {
        try
        {
            // Log the output of "ffmpeg -version".
            ChromaprintLogs["version"] = Encoding.UTF8.GetString(
                GetOutput("-version", string.Empty, false, 2000));
            Logger?.LogDebug("ffmpeg version information: {Version}", ChromaprintLogs["version"]);

            // First, validate that the installed version of ffmpeg supports chromaprint at all.
            var muxers = Encoding.UTF8.GetString(
                GetOutput("-muxers", string.Empty, false, 2000));
            ChromaprintLogs["muxer list"] = muxers;
            Logger?.LogTrace("ffmpeg muxers: {Muxers}", muxers);

            if (!muxers.Contains("chromaprint", StringComparison.OrdinalIgnoreCase))
            {
                ChromaprintLogs["error"] = "muxer_not_supported";
                Logger?.LogError("The installed version of ffmpeg does not support chromaprint");
                return false;
            }

            // Second, validate that ffmpeg understands the "-fp_format raw" option.
            var muxerHelp = Encoding.UTF8.GetString(
                GetOutput("-h muxer=chromaprint", string.Empty, false, 2000));
            ChromaprintLogs["chromaprint options"] = muxerHelp;
            Logger?.LogTrace("ffmpeg chromaprint options: {MuxerHelp}", muxerHelp);

            if (!muxerHelp.Contains("-fp_format", StringComparison.OrdinalIgnoreCase))
            {
                ChromaprintLogs["error"] = "fp_format_not_supported";
                Logger?.LogError("The installed version of ffmpeg does not support the -fp_format flag");
                return false;
            }
            else if (!muxerHelp.Contains("binary raw fingerprint", StringComparison.OrdinalIgnoreCase))
            {
                ChromaprintLogs["error"] = "fp_format_missing_options";
                Logger?.LogError("The installed version of ffmpeg does not support raw binary fingerprints");
                return false;
            }

            // Third, validate that ffmpeg supports of the all required silencedetect options.
            var silenceDetectOptions = Encoding.UTF8.GetString(
                GetOutput("-h filter=silencedetect", string.Empty, false, 2000));
            ChromaprintLogs["silencedetect options"] = silenceDetectOptions;
            Logger?.LogTrace("ffmpeg silencedetect options: {Options}", silenceDetectOptions);

            if (!silenceDetectOptions.Contains("noise tolerance", StringComparison.OrdinalIgnoreCase) ||
                !silenceDetectOptions.Contains("minimum duration", StringComparison.OrdinalIgnoreCase))
            {
                ChromaprintLogs["error"] = "silencedetect_missing_options";
                Logger?.LogError("The installed version of ffmpeg does not support the silencedetect filter");
                return false;
            }

            Logger?.LogDebug("Installed version of ffmpeg meets fingerprinting requirements");
            ChromaprintLogs["error"] = "okay";
            return true;
        }
        catch
        {
            ChromaprintLogs["error"] = "unknown_error";
            return false;
        }
    }

    /// <summary>
    /// Fingerprint a queued episode.
    /// </summary>
    /// <param name="episode">Queued episode to fingerprint.</param>
    /// <returns>Numerical fingerprint points.</returns>
    public static uint[] Fingerprint(QueuedEpisode episode)
    {
        // Try to load this episode from cache before running ffmpeg.
        if (LoadCachedFingerprint(episode, out uint[] cachedFingerprint))
        {
            Logger?.LogTrace("Fingerprint cache hit on {File}", episode.Path);
            return cachedFingerprint;
        }

        Logger?.LogDebug(
            "Fingerprinting {Duration} seconds from \"{File}\" (id {Id})",
            episode.FingerprintDuration,
            episode.Path,
            episode.EpisodeId);

        var args = string.Format(
            CultureInfo.InvariantCulture,
            "-i \"{0}\" -to {1} -ac 2 -f chromaprint -fp_format raw -",
            episode.Path,
            episode.FingerprintDuration);

        // Returns all fingerprint points as raw 32 bit unsigned integers (little endian).
        var rawPoints = GetOutput(args, string.Empty);
        if (rawPoints.Length == 0 || rawPoints.Length % 4 != 0)
        {
            Logger?.LogWarning("Chromaprint returned {Count} points for \"{Path}\"", rawPoints.Length, episode.Path);
            throw new FingerprintException("chromaprint output for \"" + episode.Path + "\" was malformed");
        }

        var results = new List<uint>();
        for (var i = 0; i < rawPoints.Length; i += 4)
        {
            var rawPoint = rawPoints.Slice(i, 4);
            results.Add(BitConverter.ToUInt32(rawPoint));
        }

        // Try to cache this fingerprint.
        CacheFingerprint(episode, results);

        return results.ToArray();
    }

    /// <summary>
    /// Transforms a Chromaprint into an inverted index of fingerprint points to the last index it appeared at.
    /// </summary>
    /// <param name="id">Episode ID.</param>
    /// <param name="fingerprint">Chromaprint fingerprint.</param>
    /// <returns>Inverted index.</returns>
    public static Dictionary<uint, int> CreateInvertedIndex(Guid id, uint[] fingerprint)
    {
        if (InvertedIndexCache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var invIndex = new Dictionary<uint, int>();

        for (int i = 0; i < fingerprint.Length; i++)
        {
            // Get the current point.
            var point = fingerprint[i];

            // Append the current sample's timecode to the collection for this point.
            invIndex[point] = i;
        }

        InvertedIndexCache[id] = invIndex;

        return invIndex;
    }

    /// <summary>
    /// Detect ranges of silence in the provided episode.
    /// </summary>
    /// <param name="episode">Queued episode.</param>
    /// <param name="limit">Maximum amount of audio (in seconds) to detect silence in.</param>
    /// <returns>Array of TimeRange objects that are silent in the queued episode.</returns>
    public static TimeRange[] DetectSilence(QueuedEpisode episode, int limit)
    {
        Logger?.LogTrace(
            "Detecting silence in \"{File}\" (limit {Limit}, id {Id})",
            episode.Path,
            limit,
            episode.EpisodeId);

        // TODO: select the audio track that matches the user's preferred language, falling
        //     back to the first track if nothing matches

        // -vn, -sn, -dn: ignore video, subtitle, and data tracks
        var args = string.Format(
            CultureInfo.InvariantCulture,
            "-vn -sn -dn " +
                "-i \"{0}\" -to {1} -af \"silencedetect=noise={2}dB:duration=0.1\" -f null -",
            episode.Path,
            limit,
            Plugin.Instance?.Configuration.SilenceDetectionMaximumNoise ?? -50);

        // Cache the output of this command to "GUID-intro-silence-v1"
        var cacheKey = episode.EpisodeId.ToString("N") + "-intro-silence-v1";

        var currentRange = new TimeRange();
        var silenceRanges = new List<TimeRange>();

        // Each match will have a type (either "start" or "end") and a timecode (a double).
        var raw = Encoding.UTF8.GetString(GetOutput(args, cacheKey, true));
        foreach (Match match in SilenceDetectionExpression.Matches(raw))
        {
            var isStart = match.Groups["type"].Value == "start";
            var time = Convert.ToDouble(match.Groups["time"].Value, CultureInfo.InvariantCulture);

            if (isStart)
            {
                currentRange.Start = time;
            }
            else
            {
                currentRange.End = time;
                silenceRanges.Add(new TimeRange(currentRange));
            }
        }

        return silenceRanges.ToArray();
    }

    /// <summary>
    /// Runs ffmpeg and returns standard output (or error).
    /// If caching is enabled, will use cacheFilename to cache the output of this command.
    /// </summary>
    /// <param name="args">Arguments to pass to ffmpeg.</param>
    /// <param name="cacheFilename">Filename to cache the output of this command to, or string.Empty if this command should not be cached.</param>
    /// <param name="stderr">If standard error should be returned.</param>
    /// <param name="timeout">Timeout (in miliseconds) to wait for ffmpeg to exit.</param>
    private static ReadOnlySpan<byte> GetOutput(
        string args,
        string cacheFilename,
        bool stderr = false,
        int timeout = 60 * 1000)
    {
        var ffmpegPath = Plugin.Instance?.FFmpegPath ?? "ffmpeg";

        var cacheOutput =
            (Plugin.Instance?.Configuration.CacheFingerprints ?? false) &&
            !string.IsNullOrEmpty(cacheFilename);

        // If caching is enabled, try to load the output of this command from the cached file.
        if (cacheOutput)
        {
            // Calculate the absolute path to the cached file.
            cacheFilename = Path.Join(Plugin.Instance!.FingerprintCachePath, cacheFilename);

            // If the cached file exists, return whatever it holds.
            if (File.Exists(cacheFilename))
            {
                Logger?.LogTrace("Returning contents of cache {Cache}", cacheFilename);
                return File.ReadAllBytes(cacheFilename);
            }

            Logger?.LogTrace("Not returning contents of cache {Cache} (not found)", cacheFilename);
        }

        // Prepend some flags to prevent FFmpeg from logging it's banner and progress information
        // for each file that is fingerprinted.
        var info = new ProcessStartInfo(ffmpegPath, args.Insert(0, "-hide_banner -loglevel info "))
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            UseShellExecute = false,
            ErrorDialog = false,

            RedirectStandardOutput = !stderr,
            RedirectStandardError = stderr
        };

        var ffmpeg = new Process
        {
            StartInfo = info
        };

        Logger?.LogDebug(
            "Starting ffmpeg with the following arguments: {Arguments}",
            ffmpeg.StartInfo.Arguments);

        ffmpeg.Start();

        using (MemoryStream ms = new MemoryStream())
        {
            var buf = new byte[4096];
            var bytesRead = 0;

            do
            {
                var streamReader = stderr ? ffmpeg.StandardError : ffmpeg.StandardOutput;
                bytesRead = streamReader.BaseStream.Read(buf, 0, buf.Length);
                ms.Write(buf, 0, bytesRead);
            }
            while (bytesRead > 0);

            ffmpeg.WaitForExit(timeout);

            var output = ms.ToArray();

            // If caching is enabled, cache the output of this command.
            if (cacheOutput)
            {
                File.WriteAllBytes(cacheFilename, output);
            }

            return output;
        }
    }

    /// <summary>
    /// Tries to load an episode's fingerprint from cache. If caching is not enabled, calling this function is a no-op.
    /// This function was created before the unified caching mechanism was introduced (in v0.1.7).
    /// </summary>
    /// <param name="episode">Episode to try to load from cache.</param>
    /// <param name="fingerprint">Array to store the fingerprint in.</param>
    /// <returns>true if the episode was successfully loaded from cache, false on any other error.</returns>
    private static bool LoadCachedFingerprint(QueuedEpisode episode, out uint[] fingerprint)
    {
        fingerprint = Array.Empty<uint>();

        // If fingerprint caching isn't enabled, don't try to load anything.
        if (!(Plugin.Instance?.Configuration.CacheFingerprints ?? false))
        {
            return false;
        }

        var path = GetFingerprintCachePath(episode);

        // If this episode isn't cached, bail out.
        if (!File.Exists(path))
        {
            return false;
        }

        // TODO: make async
        var raw = File.ReadAllLines(path, Encoding.UTF8);
        var result = new List<uint>();

        // Read each stringified uint.
        result.EnsureCapacity(raw.Length);

        try
        {
            foreach (var rawNumber in raw)
            {
                result.Add(Convert.ToUInt32(rawNumber, CultureInfo.InvariantCulture));
            }
        }
        catch (FormatException)
        {
            // Occurs when the cached fingerprint is corrupt.
            Logger?.LogDebug(
                "Cached fingerprint for {Path} ({Id}) is corrupt, ignoring cache",
                episode.Path,
                episode.EpisodeId);

            return false;
        }

        fingerprint = result.ToArray();
        return true;
    }

    /// <summary>
    /// Cache an episode's fingerprint to disk. If caching is not enabled, calling this function is a no-op.
    /// This function was created before the unified caching mechanism was introduced (in v0.1.7).
    /// </summary>
    /// <param name="episode">Episode to store in cache.</param>
    /// <param name="fingerprint">Fingerprint of the episode to store.</param>
    private static void CacheFingerprint(QueuedEpisode episode, List<uint> fingerprint)
    {
        // Bail out if caching isn't enabled.
        if (!(Plugin.Instance?.Configuration.CacheFingerprints ?? false))
        {
            return;
        }

        // Stringify each data point.
        var lines = new List<string>();
        foreach (var number in fingerprint)
        {
            lines.Add(number.ToString(CultureInfo.InvariantCulture));
        }

        // Cache the episode.
        File.WriteAllLinesAsync(GetFingerprintCachePath(episode), lines, Encoding.UTF8).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines the path an episode should be cached at.
    /// This function was created before the unified caching mechanism was introduced (in v0.1.7).
    /// </summary>
    /// <param name="episode">Episode.</param>
    private static string GetFingerprintCachePath(QueuedEpisode episode)
    {
        return Path.Join(Plugin.Instance!.FingerprintCachePath, episode.EpisodeId.ToString("N"));
    }

    /// <summary>
    /// Gets Chromaprint debugging logs.
    /// </summary>
    /// <returns>Markdown formatted logs.</returns>
    public static string GetChromaprintLogs()
    {
        var logs = new StringBuilder(1024);

        // Print the Chromaprint detection status at the top.
        // Format: "* FFmpeg: `error`"
        logs.Append("* FFmpeg: `");
        logs.Append(ChromaprintLogs["error"]);
        logs.Append("`\n\n"); // Use two newlines to separate the bulleted list from the logs

        // Print all remaining logs
        foreach (var kvp in ChromaprintLogs)
        {
            var name = kvp.Key;
            var contents = kvp.Value;

            if (string.Equals(name, "error", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            /* Format:
             * FFmpeg NAME:
             * ```
             * LOGS
             * ```
             */
            logs.Append("FFmpeg ");
            logs.Append(name);
            logs.Append(":\n```\n");
            logs.Append(contents);

            // ensure the closing triple backtick is on a separate line
            if (!contents.EndsWith('\n'))
            {
                logs.Append('\n');
            }

            logs.Append("```\n\n");
        }

        return logs.ToString();
    }
}
