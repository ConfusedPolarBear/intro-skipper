namespace ConfusedPolarBear.Plugin.IntroSkipper;

/// <summary>
/// Audio fingerprinter class.
/// </summary>
public class Fingerprinter {
    /// <summary>
    /// First file to fingerprint and compare.
    /// </summary>
    public string FileA { get; private set; }

    /// <summary>
    /// Second file to fingerprint and compare.
    /// </summary>
    public string FileB { get; private set; }

    /// <summary>
    /// Constructor.
    /// </summary>
    public Fingerprinter(string fileA, string fileB) {
        FileA = fileA;
        FileB = fileB;
    }
}
