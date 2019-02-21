using MediaBrowser.Model.Plugins;
namespace Jellyfin.Plugin.ExamplePlugin.Configuration
{
    public enum SomeOptions
    {
        OneOption,
        AnotherOption
    }
    public class PluginConfiguration : BasePluginConfiguration
    {
        //This is where you should store configurable settings your plugin might need.
        public bool TrueFalseSetting {get; set;}
        public int AnInteger {get; set;}
        public string AString {get; set;}
        public SomeOptions Options {get; set;}
        public PluginConfiguration()
        {
            Options = SomeOptions.AnotherOption;
            TrueFalseSetting = true;
            AnInteger = 5;
            AString = "This is a string setting";
        }
    }
}