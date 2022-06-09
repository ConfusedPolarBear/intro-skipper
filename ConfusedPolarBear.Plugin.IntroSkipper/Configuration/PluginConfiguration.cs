using MediaBrowser.Model.Plugins;

namespace ConfusedPolarBear.Plugin.IntroSkipper.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
    }

    /// <summary>
    /// Gets or sets a value indicating whether the episode's fingerprint should be cached to the filesystem.
    /// </summary>
    public bool CacheFingerprints { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether introductions should be automatically skipped.
    /// </summary>
    public bool AutoSkip { get; set; }

    /// <summary>
    /// Gets or sets the seconds before the intro starts to show the skip prompt at.
    /// </summary>
    public int ShowPromptAdjustment { get; set; } = 5;

    /// <summary>
    /// Gets or sets the seconds after the intro starts to hide the skip prompt at.
    /// </summary>
    public int HidePromptAdjustment { get; set; } = 10;
}
