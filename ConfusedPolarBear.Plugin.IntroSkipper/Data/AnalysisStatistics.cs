namespace ConfusedPolarBear.Plugin.IntroSkipper;

using System;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Detailed statistics about the last analysis operation performed. All times are represented as milliseconds.
/// </summary>
public class AnalysisStatistics
{
    /// <summary>
    /// Gets the number of episodes that have been analyzed so far.
    /// </summary>
    public ThreadSafeInteger TotalAnalyzedEpisodes { get; } = new ThreadSafeInteger();

    /// <summary>
    /// Gets or sets the number of episodes that need to be analyzed.
    /// </summary>
    public int TotalQueuedEpisodes { get; set; }

    /// <summary>
    /// Gets the number of times a quick scan successfully located a pair of introductions.
    /// </summary>
    public ThreadSafeInteger QuickScans { get; } = new ThreadSafeInteger();

    /// <summary>
    /// Gets the number of times a full scan successfully located a pair of introductions.
    /// </summary>
    public ThreadSafeInteger FullScans { get; } = new ThreadSafeInteger();

    /// <summary>
    /// Gets the total CPU time spent waiting for audio fingerprints to be generated.
    /// </summary>
    public ThreadSafeInteger FingerprintCPUTime { get; } = new ThreadSafeInteger();

    /// <summary>
    /// Gets the total CPU time spent analyzing fingerprints in the initial pass.
    /// </summary>
    public ThreadSafeInteger FirstPassCPUTime { get; } = new ThreadSafeInteger();

    /// <summary>
    /// Gets the total CPU time spent analyzing fingerprints in the second pass.
    /// </summary>
    public ThreadSafeInteger SecondPassCPUTime { get; } = new ThreadSafeInteger();

    /// <summary>
    /// Gets the total task runtime across all threads.
    /// </summary>
    public ThreadSafeInteger TotalCPUTime { get; } = new ThreadSafeInteger();

    /// <summary>
    /// Gets the total task runtime as measured by a clock.
    /// </summary>
    public ThreadSafeInteger TotalTaskTime { get; } = new ThreadSafeInteger();
}

/// <summary>
/// Convenience wrapper around a thread safe integer.
/// </summary>
[JsonConverter(typeof(ThreadSafeIntegerJsonConverter))]
public class ThreadSafeInteger
{
    private int value = 0;

    /// <summary>
    /// Gets the current value stored by this integer.
    /// </summary>
    public int Value
    {
        get
        {
            return value;
        }
    }

    /// <summary>
    /// Increment the value of this integer by 1.
    /// </summary>
    public void Increment()
    {
        Add(1);
    }

    /// <summary>
    /// Adds the total milliseconds elapsed since a start time.
    /// </summary>
    /// <param name="start">Start time.</param>
    public void AddDuration(DateTime start)
    {
        var elapsed = DateTime.Now.Subtract(start);
        Add((int)elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Adds the provided amount to this integer.
    /// </summary>
    /// <param name="amount">Amount to add.</param>
    public void Add(int amount)
    {
        Interlocked.Add(ref value, amount);
    }
}

/// <summary>
/// Serialize thread safe integers to a regular integer (instead of an object with a Value property).
/// </summary>
public class ThreadSafeIntegerJsonConverter : JsonConverter<ThreadSafeInteger>
{
    /// <summary>
    /// Deserialization of TSIs is not supported and will always throw a NotSupportedException.
    /// </summary>
    /// <param name="reader">Reader.</param>
    /// <param name="typeToConvert">Type.</param>
    /// <param name="options">Options.</param>
    /// <returns>Never returns.</returns>
    public override ThreadSafeInteger? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Serialize the provided TSI.
    /// </summary>
    /// <param name="writer">Writer.</param>
    /// <param name="value">TSI.</param>
    /// <param name="options">Options.</param>
    public override void Write(Utf8JsonWriter writer, ThreadSafeInteger value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}
