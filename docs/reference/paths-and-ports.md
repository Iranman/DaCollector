# Paths and Ports

## Defaults

| Item | Windows default |
| --- | --- |
| Web UI | `http://127.0.0.1:8111/webui` |
| Duplicate review page | `http://127.0.0.1:8111/webui/dacollector-duplicates.html` |
| Application data | `C:\ProgramData\DaCollector` |
| SQLite database | `C:\ProgramData\DaCollector\SQLite\DaCollector.db3` |
| Quartz database | `C:\ProgramData\DaCollector\SQLite\Quartz.db3` |
| Installer app folder | `C:\Program Files\DaCollector\DaCollector` |
| Docker data path | `/home/dacollector/.dacollector/DaCollector` |
| Docker Compose volume | `dacollector-data` |

## Environment Variables

| Variable | Purpose |
| --- | --- |
| `DACOLLECTOR_HOME` | Override the application data directory. |
| `DACOLLECTOR_PORT` | Override the HTTP port. |
| `TMDB_API_KEY` | Set the TMDB API key. |
| `IMDB_DATASET_PATH` | Set the local IMDb dataset folder. |
| `IMDB_CACHE_EXPIRATION_DAYS` | Set IMDb cache retention. |
| `TVDB_API_KEY` | Set the TVDB API key. |
| `TVDB_PIN` | Set the TVDB subscriber PIN. |
| `TVDB_CACHE_EXPIRATION_DAYS` | Set TVDB cache retention. |
| `PLEX_TARGET_BASE_URL` | Set the Plex target URL. |
| `PLEX_TARGET_SECTION_KEY` | Set the Plex library section key. |
| `PLEX_TARGET_TOKEN` | Set the Plex token. |

## Portable Instance Example

```powershell
$env:DACOLLECTOR_HOME = "D:\DaCollectorData"
$env:DACOLLECTOR_PORT = "8112"
.\DaCollector.exe
```

Open:

```text
http://127.0.0.1:8112/webui
```
