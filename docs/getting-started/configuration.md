# Configuration

DaCollector settings are stored in the server data directory and can also be overridden with selected environment variables. Environment variables are useful for test runs, portable instances, and secrets you do not want written into local scripts.

## Core Settings

| Setting | Environment variable | Default |
| --- | --- | --- |
| Application data directory | `DACOLLECTOR_HOME` | `C:\ProgramData\DaCollector` |
| HTTP port | `DACOLLECTOR_PORT` | `38111` |
| Web UI prefix | Stored setting | `webui` |
| SQLite database file | Stored setting | `DaCollector.db3` |

## Provider Settings

| Provider | Setting | Environment variable |
| --- | --- | --- |
| TMDB | API key | `TMDB_API_KEY` |
| IMDb | Dataset path | `IMDB_DATASET_PATH` |
| IMDb | Cache expiration days | `IMDB_CACHE_EXPIRATION_DAYS` |
| TVDB | API key | `TVDB_API_KEY` |
| TVDB | Subscriber PIN | `TVDB_PIN` |
| TVDB | Cache expiration days | `TVDB_CACHE_EXPIRATION_DAYS` |

TMDB is used for rich movie and TV metadata. IMDb collection builders that rely on local datasets require the IMDb dataset path to point at the downloaded TSV files. TVDB collection builders require TVDB credentials when calling TVDB APIs.

## Plex Target Settings

| Setting | Environment variable | Default |
| --- | --- | --- |
| Plex base URL | `PLEX_TARGET_BASE_URL` | `http://127.0.0.1:32400` |
| Plex library section key | `PLEX_TARGET_SECTION_KEY` | Empty |
| Plex token | `PLEX_TARGET_TOKEN` | Empty |

The Plex token is hidden in configuration surfaces. Use a Plex token that can read the target library and update collections.

## Example Local Test Environment

```powershell
$env:DACOLLECTOR_HOME = "D:\DaCollectorData"
$env:DACOLLECTOR_PORT = "38111"
$env:TMDB_API_KEY = "<tmdb-api-key>"
$env:PLEX_TARGET_BASE_URL = "http://127.0.0.1:32400"
$env:PLEX_TARGET_SECTION_KEY = "<plex-section-key>"
$env:PLEX_TARGET_TOKEN = "<plex-token>"
.\DaCollector.exe
```

## Backups

Before large configuration changes, stop DaCollector and back up:

```text
C:\ProgramData\DaCollector
```

At minimum, keep a copy of the `SQLite` folder and settings files.
