using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LibraryUnifier
{
    /// <summary>
    /// Monitors library changes and triggers unified library rebuild.
    /// </summary>
    public class LibraryMonitor : IHostedService, IDisposable
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<LibraryUnifierManager> _logger;
        private readonly IFileSystem _fileSystem;

        private Timer _debounceTimer;
        private readonly object _timerLock = new object();

        public LibraryMonitor(
            ILibraryManager libraryManager,
            ILogger<LibraryUnifierManager> logger,
            IFileSystem fileSystem)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _fileSystem = fileSystem;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Library Unifier: Starting library monitor");

            _libraryManager.ItemAdded += OnLibraryChanged;
            _libraryManager.ItemRemoved += OnLibraryChanged;

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _libraryManager.ItemAdded -= OnLibraryChanged;
            _libraryManager.ItemRemoved -= OnLibraryChanged;

            lock (_timerLock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = null;
            }

            return Task.CompletedTask;
        }

        private void OnLibraryChanged(object sender, ItemChangeEventArgs e)
        {
            // Only care about episodes and series
            if (e.Item.MediaType != MediaType.Video &&
                e.Item is not MediaBrowser.Controller.Entities.TV.Series &&
                e.Item is not MediaBrowser.Controller.Entities.TV.Episode)
            {
                return;
            }

            var config = Plugin.Instance?.PluginConfiguration;
            if (config == null || string.IsNullOrWhiteSpace(config.UnifiedLibraryPath))
            {
                return;
            }

            _logger.LogDebug($"Library Unifier: Change detected - {e.Item.Name}");

            // Debounce: wait 2 minutes after last change before rebuilding
            lock (_timerLock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = new Timer(
                    RebuildUnifiedLibrary,
                    null,
                    TimeSpan.FromMinutes(2),
                    Timeout.InfiniteTimeSpan);
            }
        }

        private async void RebuildUnifiedLibrary(object state)
        {
            _logger.LogInformation("Library Unifier: Rebuilding unified library after changes");

            try
            {
                var manager = new LibraryUnifierManager(_libraryManager, _logger, _fileSystem);
                await manager.CreateUnifiedLibraryAsync(null);
                _logger.LogInformation("Library Unifier: Unified library rebuilt successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Library Unifier: Error rebuilding unified library");
            }
        }

        public void Dispose()
        {
            lock (_timerLock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = null;
            }
        }
    }
}
