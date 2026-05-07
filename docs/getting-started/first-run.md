# First Run

After DaCollector starts, open:

```text
http://127.0.0.1:8111/webui
```

The server hosts the Web UI from the same process. If you changed the port, replace `8111` with your configured value.

## Initial Checklist

1. Create the first admin user when prompted.
2. Confirm the server status page loads.
3. Add one or more managed folders for your movie and TV files.
4. Configure provider settings for TMDB, IMDb, and TVDB as needed.
5. Configure the Plex target URL, token, and library section key if you want DaCollector to apply collections to Plex.
6. Run an import or scan job.
7. Open the duplicate review page at `http://127.0.0.1:8111/webui/dacollector-duplicates.html`.

## Server Data

On Windows, DaCollector stores its data under:

```text
C:\ProgramData\DaCollector
```

Set `DACOLLECTOR_HOME` before starting the process if you want a separate instance or portable data directory:

```powershell
$env:DACOLLECTOR_HOME = "D:\DaCollectorData"
.\DaCollector.exe
```

## Change the Port

The default port is `8111`. Set `DACOLLECTOR_PORT` before starting the process to override it:

```powershell
$env:DACOLLECTOR_PORT = "8112"
.\DaCollector.exe
```

Then open:

```text
http://127.0.0.1:8112/webui
```

## Health Checks

Use these URLs after signing in:

| URL | Purpose |
| --- | --- |
| `/api/v3/DaCollectorStatus` | Provider, collection manager, and Plex readiness. |
| `/api/v3/DaCollectorStatus/Providers` | Provider readiness without secrets. |
| `/api/v3/DaCollectorStatus/Plex` | Plex target readiness without exposing the token. |
| `/api/v3/CollectionBuilder` | Available collection builders. |
| `/api/v3/Duplicates/Exact/Summary` | Exact duplicate summary. |

## Stop DaCollector

Close the tray app or stop the `DaCollector.exe` process. Wait for the process to exit before moving or backing up the SQLite database files.
