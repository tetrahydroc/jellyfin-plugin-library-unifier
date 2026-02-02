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

        /// <summary>
        /// Merges duplicate movie versions (same TMDB ID, different quality).
        /// </summary>
        /// <response code="204">Movie merge started successfully.</response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("MergeMovies")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult> MergeMoviesAsync()
        {
            _logger.LogInformation("Merging duplicate movie versions");
            await _manager.MergeMoviesAsync(null, CancellationToken.None);
            _logger.LogInformation("Completed merging movies");
            return NoContent();
        }

        /// <summary>
        /// Merges duplicate episode versions (same episode, different quality).
        /// </summary>
        /// <response code="204">Episode merge started successfully.</response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("MergeEpisodes")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult> MergeEpisodesAsync()
        {
            _logger.LogInformation("Merging duplicate episode versions");
            await _manager.MergeEpisodesAsync(null, CancellationToken.None);
            _logger.LogInformation("Completed merging episodes");
            return NoContent();
        }

        /// <summary>
        /// Splits all merged movie versions back to individual items.
        /// </summary>
        /// <response code="204">Movie split started successfully.</response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("SplitMovies")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult> SplitMoviesAsync()
        {
            _logger.LogInformation("Splitting merged movie versions");
            await _manager.SplitMoviesAsync(null, CancellationToken.None);
            _logger.LogInformation("Completed splitting movies");
            return NoContent();
        }

        /// <summary>
        /// Splits all merged episode versions back to individual items.
        /// </summary>
        /// <response code="204">Episode split started successfully.</response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("SplitEpisodes")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult> SplitEpisodesAsync()
        {
            _logger.LogInformation("Splitting merged episode versions");
            await _manager.SplitEpisodesAsync(null, CancellationToken.None);
            _logger.LogInformation("Completed splitting episodes");
            return NoContent();
        }
    }
}
