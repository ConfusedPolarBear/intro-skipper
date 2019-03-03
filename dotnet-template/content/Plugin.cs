using System;
using System.Collections.Generic;
using Jellyfin.Plugin.dotnet_template.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.dotnet_template
{
    public class Plugin : BasePlugin<PluginConfiguration>,
    IHasWebPages
    {
        public override string Name => "dotnet_template";
        public override Guid Id => Guid.Parse("pluginguid");
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public static Plugin Instance { get; private set; }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = this.Name,
                    EmbeddedResourcePath = string.Format("Jellyfin.Plugin.{0}.Configuration.configPage.html",this.Name)
                }
            };
        }
    }
}