# Library Unifier for Jellyfin

A Jellyfin plugin that creates a unified TV library using symlinks/hardlinks to merge series from different folders.

## Features

- Creates a unified folder structure using symlinks or hardlinks
- Merges series by metadata provider IDs (TVDB, IMDB, TMDB)
- Falls back to name-based matching with quality tag normalization
- Scheduled task for automatic updates (every 24 hours)
- Supports both symlinks (cross-filesystem) and hardlinks (same filesystem, no extra space)

## Use Case

If you have TV shows spread across multiple folders like:
```
/tv/Show.Name.S01.1080p.WEB-DL/
/tv/Show.Name.S02.720p.BluRay/
/tv-new/Show Name Season 3/
```

This plugin creates a unified structure:
```
/tv-unified/Show Name/
  Season 01/
  Season 02/
  Season 03/
```

## Installation

### From Repository (Recommended)

1. In Jellyfin, go to **Dashboard → Plugins → Repositories**
2. Add repository URL: `https://raw.githubusercontent.com/tetrahydroc/jellyfin-plugin-library-unifier/main/repository.json`
3. Go to **Catalog** and install "Library Unifier"
4. Restart Jellyfin

### Manual Installation

1. Download the latest release ZIP
2. Extract to your Jellyfin plugins folder (e.g., `/var/lib/jellyfin/plugins/`)
3. Restart Jellyfin

## Usage

1. Go to **Dashboard → Plugins → Library Unifier**
2. Set the **Unified Library Output Path** (e.g., `/storage/tv-unified`)
3. Choose whether to prefer hardlinks (recommended if on same filesystem)
4. Click **Save**
5. Click **Create Unified Library**
6. Add the output path as a new TV library in Jellyfin

## Troubleshooting

### Duplicates in unified library

If you see duplicates, the series likely have different or missing metadata provider IDs:
1. In Jellyfin, go to the duplicate series
2. Click the three dots → **Identify**
3. Search for the correct show and select it
4. Re-run **Create Unified Library**

### Permission errors

Ensure the Jellyfin user has write access to the output directory:
```bash
sudo chown -R jellyfin:jellyfin /path/to/unified-library
```

## Requirements

- Jellyfin 10.11.0 or later
- .NET 9.0 runtime

## License

MIT License
