using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LibraryUnifier.Api
{
    /// <summary>
    /// The Library Unifier API controller.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("LibraryUnifier")]
    [Produces(MediaTypeNames.Application.Json)]
    public class LibraryUnifierController : ControllerBase
    {
        private readonly LibraryUnifierManager _manager;
        private readonly ILogger<LibraryUnifierManager> _logger;

        public LibraryUnifierController(
            ILibraryManager libraryManager,
            ILogger<LibraryUnifierManager> logger,
            IFileSystem fileSystem,
            IProviderManager providerManager
        )
        {
            _manager = new LibraryUnifierManager(libraryManager, logger, fileSystem, providerManager);
            _logger = logger;
        }

        /// <summary>
        /// Creates a unified library folder structure using symlinks/hardlinks.
        /// </summary>
        /// <response code="204">Unified library creation started successfully.</response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("Create")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult> CreateUnifiedLibraryAsync()
        {
            _logger.LogInformation("Creating unified library structure");

            // Run auto-identify first if enabled
            await _manager.AutoIdentifyUnmatchedSeriesAsync(null, CancellationToken.None);

            // Then create the unified library
            await _manager.CreateUnifiedLibraryAsync(null);
            _logger.LogInformation("Completed creating unified library");
            return NoContent();
        }

        /// <summary>
        /// Removes the unified library folder structure (symlinks only).
        /// </summary>
        /// <response code="204">Unified library removal started successfully.</response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("Remove")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult> RemoveUnifiedLibraryAsync()
        {
            _logger.LogInformation("Removing unified library structure");
            await _manager.RemoveUnifiedLibraryAsync(null);
            _logger.LogInformation("Completed removing unified library");
            return NoContent();
        }
    }
}
