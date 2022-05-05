using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
    /// If the output of fpcalc should be cached to the filesystem.
    /// </summary>
    public bool CacheFingerprints { get; set; }
}
