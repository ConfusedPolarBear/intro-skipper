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
        AnalysisResults = new Collection<Intro>();
    }

    /// <summary>
    /// Save timestamps to disk.
    /// </summary>
    public void SaveTimestamps()
    {
        AnalysisResults.Clear();

        foreach (var intro in Plugin.Instance!.Intros)
        {
            AnalysisResults.Add(intro.Value);
        }

        Plugin.Instance!.SaveConfiguration();
    }

    /// <summary>
    /// Restore previous analysis results from disk.
    /// </summary>
    public void RestoreTimestamps()
    {
        // Since dictionaries can't be easily serialized, analysis results are stored on disk as a list.
        foreach (var intro in AnalysisResults)
        {
            Plugin.Instance!.Intros[intro.EpisodeId] = intro;
        }
    }

    /// <summary>
    /// Previous analysis results.
    /// </summary>
    public Collection<Intro> AnalysisResults { get; private set; }
}
