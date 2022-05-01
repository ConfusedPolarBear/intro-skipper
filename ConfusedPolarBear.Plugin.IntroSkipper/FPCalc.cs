using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
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

        return results.AsReadOnly();
    }

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
}
