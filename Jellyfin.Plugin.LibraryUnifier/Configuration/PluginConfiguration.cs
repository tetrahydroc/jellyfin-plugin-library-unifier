using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.LibraryUnifier.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Output path for the unified library structure (symlinks/hardlinks).
        /// </summary>
        public string UnifiedLibraryPath { get; set; }

        /// <summary>
        /// Whether to prefer hardlinks over symlinks (hardlinks save space but require same filesystem).
        /// </summary>
        public bool PreferHardlinks { get; set; }

        /// <summary>
        /// Whether to automatically identify series that have no provider IDs before building the unified library.
        /// </summary>
        public bool EnableAutoIdentify { get; set; }

        public PluginConfiguration()
        {
            UnifiedLibraryPath = string.Empty;
            PreferHardlinks = true;
            EnableAutoIdentify = true;
        }
    }
}
