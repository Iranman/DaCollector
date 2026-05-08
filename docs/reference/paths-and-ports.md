# Paths and Ports

## Defaults

| Item | Windows default |
| --- | --- |
| Web UI | `http://127.0.0.1:38111/webui` |
| Duplicate review page | `http://127.0.0.1:38111/webui/dacollector-duplicates.html` |
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
| `TVDB_API_KEY` | Set the TVDB API key. |
| `TVDB_PIN` | Set the TVDB subscriber PIN. |
| `TVDB_CACHE_EXPIRATION_DAYS` | Set TVDB cache retention. |
| `PLEX_TARGET_BASE_URL` | Set the Plex target URL. |
| `PLEX_TARGET_SECTION_KEY` | Set the Plex library section key. |
| `PLEX_TARGET_TOKEN` | Set the Plex token. |

## Docker-Only Variables

These variables are read by the container entrypoint (`dockerentry.sh`) and are not used by the DaCollector server process itself.

| Variable | Default | Purpose |
| --- | --- | --- |
| `PUID` | `1000` | UID the `dacollector` OS user is created with. Files in `DACOLLECTOR_HOME` are owned by this UID. |
| `PGID` | `1000` | GID the `dacollector` OS group is created with. |
| `UMASK` | `002` | umask applied before the server process starts. |
| `TZ` | `Etc/UTC` | Container timezone (sets `/etc/localtime`). |
| `SKIP_CHOWN` | `false` | Set to `true` to skip the recursive `chown` on `DACOLLECTOR_HOME`. Useful on TrueNAS/ZFS where ownership is managed via ACLs. See [TrueNAS and ZFS](../getting-started/installation/docker.md#truenas-and-zfs). |

## Portable Instance Example

```powershell
$env:DACOLLECTOR_HOME = "D:\DaCollectorData"
$env:DACOLLECTOR_PORT = "38112"
.\DaCollector.exe
```

Open:

```text
http://127.0.0.1:38112/webui
```
