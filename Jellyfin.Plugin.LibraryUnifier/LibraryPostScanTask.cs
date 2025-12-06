using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LibraryUnifier
{
    /// <summary>
    /// Runs after library scans complete to update the unified library.
    /// </summary>
    public class LibraryPostScanTask : ILibraryPostScanTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<LibraryUnifierManager> _logger;
        private readonly IFileSystem _fileSystem;

        public LibraryPostScanTask(
            ILibraryManager libraryManager,
            ILogger<LibraryUnifierManager> logger,
            IFileSystem fileSystem)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _fileSystem = fileSystem;
        }

        public async Task Run(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var config = Plugin.Instance?.PluginConfiguration;

            if (config == null || string.IsNullOrWhiteSpace(config.UnifiedLibraryPath))
            {
                _logger.LogDebug("Library Unifier: Skipping post-scan task - no unified library path configured");
                return;
            }

            _logger.LogInformation("Library Unifier: Library scan completed, updating unified library");

            try
            {
                var manager = new LibraryUnifierManager(_libraryManager, _logger, _fileSystem);
                await manager.CreateUnifiedLibraryAsync(progress);
                _logger.LogInformation("Library Unifier: Unified library updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Library Unifier: Error updating unified library after scan");
            }
        }
    }
}
