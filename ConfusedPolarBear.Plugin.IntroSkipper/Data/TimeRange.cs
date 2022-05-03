using System;
using System.Collections.Generic;

namespace ConfusedPolarBear.Plugin.IntroSkipper;

/// <summary>
/// Range of contiguous time.
/// </summary>
public class TimeRange : IComparable
{
    /// <summary>
    /// Time range start (in seconds).
    /// </summary>
    public double Start { get; set; }

    /// <summary>
    /// Time range end (in seconds).
    /// </summary>
    public double End { get; set; }

    /// <summary>
    /// Duration of this time range (in seconds).
    /// </summary>
    public double Duration => End - Start;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public TimeRange()
    {
        Start = 0;
        End = 0;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    public TimeRange(double start, double end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Copy constructor.
    /// </summary>
    public TimeRange(TimeRange original)
    {
        Start = original.Start;
        End = original.End;
    }

    /// <summary>
    /// Compares this TimeRange to another TimeRange.
    /// </summary>
    /// <param name="obj">Other object to compare against.</param>
    public int CompareTo(object? obj)
    {
        if (obj is not TimeRange tr)
        {
            return 0;
        }

        return this.Duration.CompareTo(tr.Duration);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null || obj is not TimeRange tr)
        {
            return false;
        }

        return this.Start == tr.Start && this.Duration == tr.Duration;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return this.Start.GetHashCode() + this.Duration.GetHashCode();
    }

    /// <inheritdoc/>
    public static bool operator ==(TimeRange left, TimeRange right)
    {
        return left.Equals(right);
    }

    /// <inheritdoc/>
    public static bool operator !=(TimeRange left, TimeRange right)
    {
        return !left.Equals(right);
    }

    /// <inheritdoc/>
    public static bool operator <=(TimeRange left, TimeRange right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <inheritdoc/>
    public static bool operator <(TimeRange left, TimeRange right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <inheritdoc/>
    public static bool operator >=(TimeRange left, TimeRange right)
    {
        return left.CompareTo(right) >= 0;
    }

    /// <inheritdoc/>
    public static bool operator >(TimeRange left, TimeRange right)
    {
        return left.CompareTo(right) > 0;
    }
}

/// <summary>
/// Time range helpers.
/// </summary>
public static class TimeRangeHelpers
{
    /// <summary>
    /// Finds the longest contiguous time range.
    /// </summary>
    /// <param name="times">Sorted timestamps to search.</param>
    /// <param name="maximumDistance">Maximum distance permitted between contiguous timestamps.</param>
    public static TimeRange? FindContiguous(double[] times, double maximumDistance)
    {
        if (times.Length == 0)
        {
            return null;
        }

        Array.Sort(times);

        var ranges = new List<TimeRange>();
        var currentRange = new TimeRange(times[0], 0);

        // For all provided timestamps, check if it is contiguous with its neighbor.
        for (var i = 0; i < times.Length - 1; i++)
        {
            var current = times[i];
            var next = times[i + 1];

            if (next - current <= maximumDistance)
            {
                currentRange.End = next;
                continue;
            }

            ranges.Add(new TimeRange(currentRange));
            currentRange.Start = next;
        }

        // Find and return the longest contiguous range.
        ranges.Sort();

        return (ranges.Count > 0) ? ranges[0] : null;
    }
}
