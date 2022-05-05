using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ConfusedPolarBear.Plugin.IntroSkipper;

/// <summary>
/// Wrapper for the fpcalc utility.
/// </summary>
public static class FPCalc {
    /// <summary>
    /// Logger.
    /// </summary>
    public static ILogger? Logger { get; set; }

    /// <summary>
    /// Check that the fpcalc utility is installed.
    /// </summary>
    public static bool CheckFPCalcInstalled()
    {
        try
        {
            var version = getOutput("-version", 2000);
            Logger?.LogDebug("fpcalc version: {Version}", version);
            return version.StartsWith("fpcalc version", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Fingerprint a queued episode.
    /// </summary>
    /// <param name="episode">Queued episode to fingerprint.</param>
    public static ReadOnlyCollection<uint> Fingerprint(QueuedEpisode episode)
    {
        // Try to load this episode from cache before running fpcalc.
        if (loadCachedFingerprint(episode, out ReadOnlyCollection<uint> cachedFingerprint))
        {
            Logger?.LogDebug("Fingerprint cache hit on {File}", episode.Path);
            return cachedFingerprint;
        }

        Logger?.LogDebug("Fingerprinting {Duration} seconds from {File}", episode.FingerprintDuration, episode.Path);

        // FIXME: revisit escaping
        var path = "\"" + episode.Path + "\"";
        var duration = episode.FingerprintDuration.ToString(CultureInfo.InvariantCulture);
        var args = " -raw -length " + duration + " " + path;

        /* Returns output similar to the following:
         * DURATION=123
         * FINGERPRINT=123456789,987654321,123456789,987654321,123456789,987654321
        */

        var raw = getOutput(args);
        var lines = raw.Split("\n");

        if (lines.Length < 2)
        {
            Logger?.LogTrace("fpcalc output is {Raw}", raw);
            throw new FingerprintException("fpcalc output was malformed");
        }

        // Remove the "FINGERPRINT=" prefix and split into an array of numbers.
        var fingerprint = lines[1].Substring(12).Split(",");

        var results = new List<uint>();
        foreach (var rawNumber in fingerprint)
        {
            results.Add(Convert.ToUInt32(rawNumber, CultureInfo.InvariantCulture));
        }

        // Try to cache this fingerprint.
        cacheFingerprint(episode, results);

        return results.AsReadOnly();
    }

    /// <summary>
    /// Runs fpcalc and returns standard output.
    /// </summary>
    /// <param name="args">Arguments to pass to fpcalc.</param>
    /// <param name="timeout">Timeout (in seconds) to wait for fpcalc to exit.</param>
    private static string getOutput(string args, int timeout = 60 * 1000)
    {
        var info = new ProcessStartInfo("fpcalc", args);
        info.CreateNoWindow = true;
        info.RedirectStandardOutput = true;

        var fpcalc = new Process();
        fpcalc.StartInfo = info;

        fpcalc.Start();
        fpcalc.WaitForExit(timeout);

        return fpcalc.StandardOutput.ReadToEnd();
    }

    /// <summary>
    /// Tries to load an episode's fingerprint from cache. If caching is not enabled, calling this function is a no-op.
    /// </summary>
    /// <param name="episode">Episode to try to load from cache.</param>
    /// <param name="fingerprint">ReadOnlyCollection to store the fingerprint in.</param>
    /// <returns>true if the episode was successfully loaded from cache, false on any other error.</returns>
    private static bool loadCachedFingerprint(QueuedEpisode episode, out ReadOnlyCollection<uint> fingerprint)
    {
        fingerprint = new List<uint>().AsReadOnly();

        // If fingerprint caching isn't enabled, don't try to load anything.
        if (!Plugin.Instance!.Configuration.CacheFingerprints)
        {
            return false;
        }

        var path = getFingerprintCachePath(episode);

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
        foreach (var rawNumber in raw)
        {
            result.Add(Convert.ToUInt32(rawNumber, CultureInfo.InvariantCulture));
        }

        fingerprint = result.AsReadOnly();
        return true;
    }

    /// <summary>
    /// Cache an episode's fingerprint to disk. If caching is not enabled, calling this function is a no-op.
    /// </summary>
    /// <param name="episode">Episode to store in cache.</param>
    /// <param name="fingerprint">Fingerprint of the episode to store.</param>
    private static void cacheFingerprint(QueuedEpisode episode, List<uint> fingerprint)
    {
        // Bail out if caching isn't enabled.
        if (!Plugin.Instance!.Configuration.CacheFingerprints)
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
        File.WriteAllLinesAsync(getFingerprintCachePath(episode), lines, Encoding.UTF8).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines the path an episode should be cached at.
    /// </summary>
    /// <param name="episode">Episode.</param>
    private static string getFingerprintCachePath(QueuedEpisode episode)
    {
        return Path.Join(Plugin.Instance!.FingerprintCachePath, episode.EpisodeId.ToString("N"));
    }
}
