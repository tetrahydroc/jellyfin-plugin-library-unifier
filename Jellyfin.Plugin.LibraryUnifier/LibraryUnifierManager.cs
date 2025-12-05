using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LibraryUnifier
{
    public class LibraryUnifierManager
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<LibraryUnifierManager> _logger;
        private readonly IFileSystem _fileSystem;

        public LibraryUnifierManager(
            ILibraryManager libraryManager,
            ILogger<LibraryUnifierManager> logger,
            IFileSystem fileSystem
        )
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _fileSystem = fileSystem;
        }

        /// <summary>
        /// Creates a unified library folder structure using symlinks/hardlinks.
        /// Groups series by metadata provider ID and creates proper folder structure.
        /// </summary>
        public Task CreateUnifiedLibraryAsync(IProgress<double> progress)
        {
            var unifiedPath = Plugin.Instance.PluginConfiguration.UnifiedLibraryPath;
            var preferHardlinks = Plugin.Instance.PluginConfiguration.PreferHardlinks;

            if (string.IsNullOrWhiteSpace(unifiedPath))
            {
                _logger.LogError("Unified library path is not configured. Please set it in plugin settings.");
                return Task.CompletedTask;
            }

            _logger.LogInformation($"Creating unified library at: {unifiedPath}");

            // Clean up existing unified library to prevent stale entries
            if (Directory.Exists(unifiedPath))
            {
                _logger.LogInformation("Cleaning up existing unified library before recreating...");
                try
                {
                    foreach (var dir in Directory.GetDirectories(unifiedPath))
                    {
                        Directory.Delete(dir, recursive: true);
                    }
                    foreach (var file in Directory.GetFiles(unifiedPath))
                    {
                        File.Delete(file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error cleaning up unified library, continuing anyway...");
                }
            }
            else
            {
                Directory.CreateDirectory(unifiedPath);
            }

            // Get all episodes and group by series provider ID
            var allEpisodes = GetAllEpisodesWithMetadata();

            _logger.LogInformation($"Found {allEpisodes.Count} episodes to process");

            var episodesWithSeries = allEpisodes.Where(e => e.Series != null).ToList();

            // Build a unified provider ID mapping
            var providerIdToCanonicalKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var processedSeries = new HashSet<Guid>();
            foreach (var ep in episodesWithSeries)
            {
                if (processedSeries.Contains(ep.Series.Id)) continue;
                processedSeries.Add(ep.Series.Id);

                var seriesName = ep.Series.Name ?? ep.SeriesName ?? "Unknown";
                var allIds = GetAllSeriesProviderIds(ep.Series);

                if (allIds.Count == 0)
                {
                    continue;
                }

                string canonicalKey = null;
                foreach (var id in allIds)
                {
                    if (providerIdToCanonicalKey.TryGetValue(id, out var existingKey))
                    {
                        canonicalKey = existingKey;
                        _logger.LogDebug($"Series: '{seriesName}' matched existing canonical key: {canonicalKey} via {id}");
                        break;
                    }
                }

                if (canonicalKey == null)
                {
                    canonicalKey = allIds[0];
                }

                foreach (var id in allIds)
                {
                    providerIdToCanonicalKey[id] = canonicalKey;
                }
            }

            _logger.LogInformation($"Built provider ID mapping with {providerIdToCanonicalKey.Count} entries from {processedSeries.Count} unique series");

            var nameToCanonicalKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var prefixToCanonicalKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var ep in episodesWithSeries)
            {
                var normalizedName = NormalizeSeriesName(ep.Series.Name ?? ep.SeriesName ?? "");
                var canonicalKey = GetCanonicalKeyForSeries(ep.Series, providerIdToCanonicalKey);

                if (!string.IsNullOrEmpty(canonicalKey))
                {
                    if (!nameToCanonicalKey.ContainsKey(normalizedName))
                    {
                        nameToCanonicalKey[normalizedName] = canonicalKey;
                    }

                    var prefix = GetNamePrefix(normalizedName);
                    if (!string.IsNullOrEmpty(prefix) && !prefixToCanonicalKey.ContainsKey(prefix))
                    {
                        prefixToCanonicalKey[prefix] = canonicalKey;
                    }
                }
            }

            var seriesGroups = episodesWithSeries
                .GroupBy(e => {
                    var canonicalKey = GetCanonicalKeyForSeries(e.Series, providerIdToCanonicalKey);
                    if (!string.IsNullOrEmpty(canonicalKey))
                    {
                        return canonicalKey;
                    }

                    var normalizedName = NormalizeSeriesName(e.Series.Name ?? e.SeriesName ?? "");
                    if (nameToCanonicalKey.TryGetValue(normalizedName, out var linkedKey))
                    {
                        return linkedKey;
                    }

                    var prefix = GetNamePrefix(normalizedName);
                    if (!string.IsNullOrEmpty(prefix) && prefixToCanonicalKey.TryGetValue(prefix, out var prefixKey))
                    {
                        return prefixKey;
                    }

                    return $"name:{normalizedName}";
                })
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .ToList();

            _logger.LogInformation($"Found {seriesGroups.Count} unique series to unify");

            foreach (var group in seriesGroups)
            {
                var distinctSeriesNames = group.Select(e => e.Series?.Name ?? e.SeriesName ?? "Unknown").Distinct().ToList();
                if (distinctSeriesNames.Count > 1)
                {
                    _logger.LogInformation($"MERGING: Key '{group.Key}' combines: [{string.Join(", ", distinctSeriesNames)}]");
                }
            }

            var current = 0;
            var totalLinks = 0;
            var failedLinks = 0;

            foreach (var seriesGroup in seriesGroups)
            {
                current++;
                var percent = current / (double)seriesGroups.Count * 100;
                progress?.Report((int)percent);

                var seriesName = GetBestSeriesName(seriesGroup);
                var seriesPath = Path.Combine(unifiedPath, seriesName);

                var seasonGroups = seriesGroup
                    .GroupBy(e => e.ParentIndexNumber ?? 0)
                    .OrderBy(g => g.Key);

                foreach (var seasonGroup in seasonGroups)
                {
                    var seasonNumber = seasonGroup.Key;
                    var seasonFolder = seasonNumber == 0 ? "Specials" : $"Season {seasonNumber:D2}";
                    var seasonPath = Path.Combine(seriesPath, seasonFolder);

                    if (!Directory.Exists(seasonPath))
                    {
                        Directory.CreateDirectory(seasonPath);
                    }

                    foreach (var episode in seasonGroup.OrderBy(e => e.IndexNumber ?? 0))
                    {
                        if (string.IsNullOrEmpty(episode.Path) || !File.Exists(episode.Path))
                        {
                            continue;
                        }

                        var fileName = Path.GetFileName(episode.Path);
                        var linkPath = Path.Combine(seasonPath, fileName);

                        if (File.Exists(linkPath) || IsSymlink(linkPath))
                        {
                            continue;
                        }

                        try
                        {
                            if (preferHardlinks)
                            {
                                if (TryCreateHardLink(linkPath, episode.Path))
                                {
                                    totalLinks++;
                                    continue;
                                }
                            }

                            if (TryCreateSymLink(linkPath, episode.Path))
                            {
                                totalLinks++;
                            }
                            else
                            {
                                failedLinks++;
                                _logger.LogWarning($"Failed to create link for: {episode.Path}");
                            }
                        }
                        catch (Exception ex)
                        {
                            failedLinks++;
                            _logger.LogError(ex, $"Error creating link for: {episode.Path}");
                        }
                    }
                }

                _logger.LogInformation($"Processed series: {seriesName}");
            }

            progress?.Report(100);
            _logger.LogInformation($"Unified library created. Total links: {totalLinks}, Failed: {failedLinks}");

            return Task.CompletedTask;
        }

        /// <summary>
        /// Removes the unified library folder structure.
        /// </summary>
        public Task RemoveUnifiedLibraryAsync(IProgress<double> progress)
        {
            var unifiedPath = Plugin.Instance.PluginConfiguration.UnifiedLibraryPath;

            if (string.IsNullOrWhiteSpace(unifiedPath))
            {
                _logger.LogError("Unified library path is not configured.");
                return Task.CompletedTask;
            }

            if (!Directory.Exists(unifiedPath))
            {
                _logger.LogInformation("Unified library path does not exist, nothing to remove.");
                return Task.CompletedTask;
            }

            _logger.LogInformation($"Removing unified library at: {unifiedPath}");

            try
            {
                RemoveLinksRecursively(unifiedPath, progress);
                _logger.LogInformation("Unified library removed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing unified library");
            }

            progress?.Report(100);
            return Task.CompletedTask;
        }

        private void RemoveLinksRecursively(string path, IProgress<double> progress)
        {
            var directories = Directory.GetDirectories(path);

            foreach (var dir in directories)
            {
                RemoveLinksRecursively(dir, progress);

                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                }
            }

            var files = Directory.GetFiles(path);
            foreach (var file in files)
            {
                if (IsSymlink(file) || IsHardLink(file))
                {
                    File.Delete(file);
                }
            }
        }

        private List<Episode> GetAllEpisodesWithMetadata()
        {
            return _libraryManager
                .GetItemList(
                    new InternalItemsQuery
                    {
                        IncludeItemTypes = [BaseItemKind.Episode],
                        IsVirtualItem = false,
                        Recursive = true,
                    }
                )
                .Select(m => m as Episode)
                .Where(e => e != null && !string.IsNullOrEmpty(e.Path))
                .ToList();
        }

        private static List<string> GetAllSeriesProviderIds(Series series)
        {
            var ids = new List<string>();
            if (series == null || series.ProviderIds == null) return ids;

            var tvdb = GetProviderIdValue(series.ProviderIds, "Tvdb", "TVDB", "tvdb");
            if (!string.IsNullOrEmpty(tvdb))
            {
                ids.Add($"tvdb:{tvdb}");
            }

            var imdb = GetProviderIdValue(series.ProviderIds, "Imdb", "IMDB", "imdb", "IMDb");
            if (!string.IsNullOrEmpty(imdb))
            {
                ids.Add($"imdb:{imdb}");
            }

            var tmdb = GetProviderIdValue(series.ProviderIds, "Tmdb", "TMDB", "tmdb", "TMDb", "TheMovieDb");
            if (!string.IsNullOrEmpty(tmdb))
            {
                ids.Add($"tmdb:{tmdb}");
            }

            return ids;
        }

        private static string GetProviderIdValue(Dictionary<string, string> providerIds, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (providerIds.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
            return null;
        }

        private static string GetCanonicalKeyForSeries(Series series, Dictionary<string, string> providerIdToCanonicalKey)
        {
            if (series == null) return string.Empty;

            var allIds = GetAllSeriesProviderIds(series);
            foreach (var id in allIds)
            {
                if (providerIdToCanonicalKey.TryGetValue(id, out var canonicalKey))
                {
                    return canonicalKey;
                }
            }

            return string.Empty;
        }

        private static string NormalizeSeriesName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unknown";

            var normalized = name;

            var patternsToRemove = new[] {
                @"\s+S\d{1,2}(?:-S?\d{1,2})?\s*$",
                @"\s+S\d{1,2}(?:-S?\d{1,2})?\s+.*$",
                @"\s+\d{3,4}p\b.*$",
                @"\s+(?:WEB-DL|WEBRIP|BLURAY|BLU-RAY|HDTV|DVDRIP|REMUX)\b.*$",
                @"\s+(?:AMZN|HULU|DSNP|HMAX|ATVP|NOWTV)\b.*$",
                @"\s+(?:x264|x265|H\.?264|H\.?265|HEVC|AVC)\b.*$",
                @"\s+(?:DDP\d|DTS|AAC\d|AC3|TrueHD|Atmos)\b.*$",
                @"\s*\[.*?\]\s*$",
                @"\s*\((?:19|20)\d{2}\)\s*$",
                @"\s+iNTERNAL\b.*$",
                @"\s+HDR\b.*$",
                @"…$",
                @"\.{2,}$",
            };

            foreach (var pattern in patternsToRemove)
            {
                normalized = System.Text.RegularExpressions.Regex.Replace(
                    normalized, pattern, "",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[:\-–—]", " ");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[,']", "");
            normalized = normalized.TrimEnd('.', '-', '_', ' ', '…');
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();

            if (normalized.Length < 3) return "unknown";

            return normalized.ToLowerInvariant();
        }

        private static string GetNamePrefix(string normalizedName)
        {
            if (string.IsNullOrWhiteSpace(normalizedName)) return string.Empty;

            var words = normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length < 3) return string.Empty;

            var inIndex = Array.FindIndex(words, w => w.Equals("in", StringComparison.OrdinalIgnoreCase));
            if (inIndex >= 2)
            {
                return string.Join(" ", words.Take(inIndex + 1));
            }

            if (words.Length >= 3)
            {
                return string.Join(" ", words.Take(2));
            }

            return string.Empty;
        }

        private string GetBestSeriesName(IEnumerable<Episode> episodes)
        {
            var allNames = new List<string>();

            foreach (var ep in episodes)
            {
                if (ep.Series != null && !string.IsNullOrWhiteSpace(ep.Series.Name))
                {
                    allNames.Add(ep.Series.Name);
                }
                if (!string.IsNullOrWhiteSpace(ep.SeriesName))
                {
                    allNames.Add(ep.SeriesName);
                }
            }

            if (allNames.Count == 0)
            {
                return SanitizeFileName("Unknown Series");
            }

            var groupedByNormalized = allNames
                .GroupBy(n => NormalizeSeriesName(n))
                .OrderByDescending(g => g.Count())
                .First();

            var cleanName = groupedByNormalized
                .OrderBy(n => HasQualityTags(n) ? 1 : 0)
                .ThenBy(n => n.Length)
                .First();

            if (HasQualityTags(cleanName))
            {
                var betterName = groupedByNormalized.FirstOrDefault(n => !HasQualityTags(n));
                if (betterName != null)
                {
                    cleanName = betterName;
                }
                else
                {
                    cleanName = StripQualityTags(cleanName);
                }
            }

            if (string.IsNullOrWhiteSpace(cleanName) || cleanName.Length < 3)
            {
                cleanName = allNames.FirstOrDefault(n => n.Length >= 3) ?? "Unknown Series";
            }

            return SanitizeFileName(cleanName);
        }

        private static string StripQualityTags(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            var stripped = name;

            var patternsToRemove = new[] {
                @"\s+S\d{1,2}(?:-S?\d{1,2})?\s*$",
                @"\s+S\d{1,2}(?:-S?\d{1,2})?\s+.*$",
                @"\s+\d{3,4}p\b.*$",
                @"\s+(?:WEB-DL|WEBRIP|BLURAY|BLU-RAY|HDTV|DVDRIP|REMUX)\b.*$",
                @"\s+(?:AMZN|HULU|DSNP|HMAX|ATVP|NOWTV)\b.*$",
                @"\s+(?:x264|x265|H\.?264|H\.?265|HEVC|AVC)\b.*$",
                @"\s+(?:DDP\d|DTS|AAC\d|AC3|TrueHD|Atmos)\b.*$",
                @"\s+iNTERNAL\b.*$",
                @"\s+HDR\b.*$",
            };

            foreach (var pattern in patternsToRemove)
            {
                stripped = System.Text.RegularExpressions.Regex.Replace(
                    stripped, pattern, "",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            stripped = stripped.TrimEnd('.', '-', '_', ' ', '…');

            return stripped;
        }

        private static bool HasQualityTags(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            var lowerName = name.ToLowerInvariant();
            var qualityTags = new[] {
                "1080p", "720p", "480p", "2160p", "4k",
                "web-dl", "webrip", "bluray", "blu-ray", "hdtv", "dvdrip",
                "x264", "x265", "h264", "h.264", "h265", "h.265", "hevc", "avc",
                "amzn", "nf", "hulu", "dsnp", "hmax", "atvp",
                "ddp", "dts", "aac", "ac3", "truehd", "atmos",
                "hdr", "dv", "dolby", "remux",
                "s01", "s02", "s03", "s04", "s05", "s06", "s07", "s08", "s09", "s10",
                "s11", "s12", "s13", "s14", "s15", "s16", "s17", "s18", "s19", "s20"
            };

            return qualityTags.Any(tag => lowerName.Contains(tag));
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
        }

        private static bool IsSymlink(string path)
        {
            try
            {
                var fileInfo = new FileInfo(path);
                return fileInfo.Exists && fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsHardLink(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return false;
                }
                else
                {
                    var fileInfo = new UnixFileInfo(path);
                    return fileInfo.LinkCount > 1;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool TryCreateHardLink(string linkPath, string targetPath)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return CreateHardLinkWindows(linkPath, targetPath);
                }
                else
                {
                    return CreateHardLinkUnix(linkPath, targetPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, $"Hardlink failed for {targetPath}, will try symlink");
                return false;
            }
        }

        private bool TryCreateSymLink(string linkPath, string targetPath)
        {
            try
            {
                File.CreateSymbolicLink(linkPath, targetPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to create symlink: {linkPath} -> {targetPath}");
                return false;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        private static bool CreateHardLinkWindows(string linkPath, string targetPath)
        {
            return CreateHardLink(linkPath, targetPath, IntPtr.Zero);
        }

        private static bool CreateHardLinkUnix(string linkPath, string targetPath)
        {
            try
            {
                var linkSyscall = link(targetPath, linkPath);
                return linkSyscall == 0;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int link(string oldpath, string newpath);

        private class UnixFileInfo
        {
            public int LinkCount { get; }

            public UnixFileInfo(string path)
            {
                try
                {
                    if (stat(path, out var statbuf) == 0)
                    {
                        LinkCount = (int)statbuf.st_nlink;
                    }
                    else
                    {
                        LinkCount = 1;
                    }
                }
                catch
                {
                    LinkCount = 1;
                }
            }

            [DllImport("libc", SetLastError = true)]
            private static extern int stat(string path, out StatBuffer statbuf);

            [StructLayout(LayoutKind.Sequential)]
            private struct StatBuffer
            {
                public ulong st_dev;
                public ulong st_ino;
                public ulong st_nlink;
                public uint st_mode;
                public uint st_uid;
                public uint st_gid;
                public int __pad0;
                public ulong st_rdev;
                public long st_size;
                public long st_blksize;
                public long st_blocks;
                public long st_atime;
                public long st_atimensec;
                public long st_mtime;
                public long st_mtimensec;
                public long st_ctime;
                public long st_ctimensec;
                public long __unused0;
                public long __unused1;
                public long __unused2;
            }
        }
    }
}
