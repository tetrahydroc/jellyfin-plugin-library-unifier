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

        public PluginConfiguration()
        {
            UnifiedLibraryPath = string.Empty;
            PreferHardlinks = true;
        }
    }
}
