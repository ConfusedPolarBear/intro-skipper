using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Template.Configuration
{
    public enum SomeOptions
    {
        OneOption,
        AnotherOption
    }

    public class PluginConfiguration : BasePluginConfiguration
    {
        // store configurable settings your plugin might need
        public bool TrueFalseSetting { get; set; }
        public int AnInteger { get; set; }
        public string AString { get; set; }
        public SomeOptions Options { get; set; }

        public PluginConfiguration()
        {
            // set default options here
            Options = SomeOptions.AnotherOption;
            TrueFalseSetting = true;
            AnInteger = 2;
            AString = "string";
        }
    }
}
