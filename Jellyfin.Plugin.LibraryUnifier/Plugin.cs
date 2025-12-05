using System;
using System.Collections.Generic;
using Jellyfin.Plugin.LibraryUnifier.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Common.Plugins;

namespace Jellyfin.Plugin.LibraryUnifier
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public Plugin(IServerApplicationPaths appPaths, IXmlSerializer xmlSerializer)
            : base(appPaths, xmlSerializer)
        {
            Instance = this;
        }

        public override string Name => "Library Unifier";

        public static Plugin Instance { get; private set; }

        public override string Description
            => "Creates a unified library structure using symlinks to merge series from different folders";

        public PluginConfiguration PluginConfiguration => Configuration;

        private readonly Guid _id = new Guid("b8e3c5a1-9d4f-4e2b-8c6a-1f3d5e7b9a0c");
        public override Guid Id => _id;

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "Library Unifier",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configurationpage.html"
                }
            };
        }
    }
}
