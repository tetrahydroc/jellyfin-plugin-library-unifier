using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LibraryUnifier.ScheduledTasks
{
    public class UpdateUnifiedLibraryTask : IScheduledTask
    {
        private readonly ILogger _logger;
        private readonly LibraryUnifierManager _manager;

        public UpdateUnifiedLibraryTask(
            ILibraryManager libraryManager,
            ILogger<LibraryUnifierManager> logger,
            IFileSystem fileSystem,
            IProviderManager providerManager
        )
        {
            _logger = logger;
            _manager = new LibraryUnifierManager(libraryManager, logger, fileSystem, providerManager);
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.LogInformation("Starting scheduled task: Update Unified Library");

            // Run auto-identify first if enabled
            await _manager.AutoIdentifyUnmatchedSeriesAsync(progress, cancellationToken);

            // Then create the unified library
            await _manager.CreateUnifiedLibraryAsync(progress);
            _logger.LogInformation("Update Unified Library task finished");
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfoType.IntervalTrigger,
                    IntervalTicks = TimeSpan.FromHours(24).Ticks
                }
            };
        }

        public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            return Execute(cancellationToken, progress);
        }

        public string Name => "Update Unified Library";
        public string Key => "UpdateUnifiedLibraryTask";
        public string Description => "Recreates the unified library folder structure with symlinks";
        public string Category => "Library Unifier";
    }
}
