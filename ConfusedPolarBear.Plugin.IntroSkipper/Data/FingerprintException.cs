using System;

namespace ConfusedPolarBear.Plugin.IntroSkipper;

/// <summary>
/// Exception raised when an error is encountered analyzing audio.
/// </summary>
public class FingerprintException: Exception {
    /// <summary>
    /// Constructor.
    /// </summary>
    public FingerprintException()
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    public FingerprintException(string message): base(message)
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    public FingerprintException(string message, Exception inner): base(message, inner)
    {
    }
}
