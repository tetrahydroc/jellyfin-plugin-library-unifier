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

        /// <summary>
        /// Whether to automatically merge duplicate movie versions (same TMDB ID, different quality).
        /// </summary>
        public bool EnableMergeMovies { get; set; }

        /// <summary>
        /// Whether to automatically merge duplicate episode versions (same episode, different quality).
        /// </summary>
        public bool EnableMergeEpisodes { get; set; }

        /// <summary>
        /// Paths to exclude from merge operations.
        /// </summary>
        public string[] LocationsExcludedFromMerge { get; set; }

        public PluginConfiguration()
        {
            UnifiedLibraryPath = string.Empty;
            PreferHardlinks = true;
            EnableAutoIdentify = true;
            EnableMergeMovies = false;
            EnableMergeEpisodes = false;
            LocationsExcludedFromMerge = [];
        }
    }
}
