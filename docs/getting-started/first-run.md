# First Run

After DaCollector starts, open the Web UI:

```text
http://127.0.0.1:38111/webui
```

If you changed the port, replace `38111` with your configured value. Then follow the steps below.

---

## Step 1 — Create the First Admin User

On a brand-new install, the setup wizard appears automatically. Enter a username and password for the first admin account. This account is stored in the local database; it is not linked to any external identity.

After setup is complete, sign in. The Web UI stores your API key in browser local storage and sends it with every request.

To add more users later, use `Settings → Users` or the `/api/v3/User` endpoints.

---

## Step 2 — Add Managed Folders

Managed folders are the root directories DaCollector scans for media files.

1. Go to `Settings → Managed Folders`.
2. Add each root folder that contains your movies or TV files.
3. Mark each folder as **Watch Source** if you want DaCollector to detect new files automatically.

After saving, DaCollector queues a scan. The import pipeline hashes files, queries release providers for episode mappings, and builds the local file-to-series index.

For Docker, the folder must be mounted into the container first:

```yaml
volumes:
  - dacollector-data:/home/dacollector/.dacollector
  - "/mnt/media/movies:/media/movies:ro"
  - "/mnt/media/tv:/media/tv:ro"
```

Then add `/media/movies` and `/media/tv` as managed folders inside DaCollector.

---

## Step 3 — Configure Metadata Providers

DaCollector uses TMDB, TVDB, and IMDb to build collection member lists. Configure each provider you intend to use.

### TMDB

Go to `Settings → TMDB` and enter your TMDB API key. Without it, TMDB collection builders will fail.

Get a key at [themoviedb.org/settings/api](https://www.themoviedb.org/settings/api). The free v3 API key is sufficient.

```yaml
TMDB_API_KEY: "your-tmdb-key"
```

### TVDB

Go to `Settings → TVDB` and enter your TVDB API key and optional subscriber PIN. Without it, TVDB collection builders will fail.

Get a key at [thetvdb.com/dashboard/account/apikeys](https://thetvdb.com/dashboard/account/apikeys).

```yaml
TVDB_API_KEY: "your-tvdb-key"
TVDB_PIN: ""
```

### IMDb

IMDb collection builders read local dataset files — no API key is needed, but you must download the files first. Get them from [datasets.imdbws.com](https://datasets.imdbws.com/):

- `title.basics.tsv.gz` — required for all IMDb builders
- `title.ratings.tsv.gz` — required only for chart-based builders (`imdb_chart`)

Extract or place them in a folder the server can read, then point `IMDB_DATASET_PATH` at that folder:

```yaml
IMDB_DATASET_PATH: "/metadata/imdb"
```

For Docker, mount the folder and configure the path:

```yaml
volumes:
  - "/mnt/media/imdb:/metadata/imdb:ro"
environment:
  IMDB_DATASET_PATH: "/metadata/imdb"
```

---

## Step 4 — Configure the Plex Target

DaCollector applies managed collection membership to Plex. You need a Plex token and the numeric library section key.

### Find Your Plex Token

In Plex Web, open any media item, click the three-dot menu, and choose **Get Info**. The URL bar will contain `X-Plex-Token=<value>`. Copy that value.

Alternatively, sign into [plex.tv](https://www.plex.tv/claim/) and inspect the network tab for requests to `plex.tv/users/sign_in.json` — the token is in the response JSON.

### Find the Library Section Key

Call the Plex API directly (replace the token):

```text
http://127.0.0.1:32400/library/sections?X-Plex-Token=<your-token>
```

The `key` attribute on each `<Directory>` element is the section key.

Or use DaCollector's Plex library endpoint after setting the base URL and token:

```http
GET /api/v3/PlexTarget/Library
```

### Apply the Settings

Go to `Settings → Plex` and enter:

- **Base URL**: `http://127.0.0.1:32400` (or the LAN address when Plex runs on a different machine)
- **Token**: the token you found above
- **Default Library Section Key**: the numeric section key

Or use environment variables:

```yaml
PLEX_TARGET_BASE_URL: "http://host.docker.internal:32400"
PLEX_TARGET_SECTION_KEY: "1"
PLEX_TARGET_TOKEN: "your-plex-token"
```

Test connectivity:

```http
GET /api/v3/PlexTarget/Identity
```

A `200` response with server name and version confirms Plex is reachable.

---

## Step 5 — Preview a Collection

1. Open `http://127.0.0.1:38111/webui/dacollector-collections.html`.
2. Click **New Collection**.
3. Give the collection a name, for example `MCU Movies`.
4. Add a rule — for example, builder `tmdb_collection` with option `id=131296`.
5. Click **Preview**. DaCollector resolves the rule locally and shows which titles it would include.
6. Review matched and missing items. Missing items appear when DaCollector cannot find a Plex library match for a provider ID.
7. Click **Dry Run** to see what would be added to or removed from Plex without writing anything yet.
8. When the dry run looks correct, click **Apply** to create or update the Plex collection.

See [Collection Management](../features/collection-management.md) for a full list of builder names and sync modes.

---

## Step 6 — Review Duplicates

1. Open `http://127.0.0.1:38111/webui/dacollector-duplicates.html`.
2. **Exact File Duplicates** — shows DaCollector's cleanup plan for files in managed folders. Review keep and remove candidates, confirm a dry-run, then optionally confirm a delete.
3. **Plex Media Duplicates** — enter a Plex library section key and click **Load** to see Plex library entries that appear to be duplicates based on provider IDs, title/year, and file hashes. This view is read-only; clean up in Plex after reviewing.

See [Duplicate Management](../features/duplicate-management.md) for the full workflow.

---

## Health Check Endpoints

After signing in, poll these to confirm the server is fully running:

| URL | Purpose |
| --- | --- |
| `/api/v3/DaCollectorStatus` | Provider, collection manager, and Plex readiness. |
| `/api/v3/DaCollectorStatus/Providers` | Provider readiness without secrets. |
| `/api/v3/DaCollectorStatus/Plex` | Plex target readiness without exposing the token. |
| `/api/v3/CollectionBuilder` | Available collection builders. |
| `/api/v3/ManagedCollection` | Saved collection definitions. |
| `/api/v3/Duplicates/Exact/Summary` | Exact duplicate summary. |

---

## Server Data Locations

| Platform | Data path |
| --- | --- |
| Windows (installer) | `C:\ProgramData\DaCollector` |
| Windows (custom) | Set `DACOLLECTOR_HOME` before starting. |
| Docker | `/home/dacollector/.dacollector/DaCollector` |

The data path contains settings, SQLite databases, logs, provider cache, images, plugins, and Web UI data.

---

## Change the Port

```powershell
$env:DACOLLECTOR_PORT = "38112"
.\DaCollector.exe
```

Then open `http://127.0.0.1:38112/webui`.

---

## Stop DaCollector

Close the tray app or stop the `DaCollector.exe` process. For Docker:

```powershell
docker compose down
```

Wait for the process to exit before moving or backing up the SQLite database files.

---

## Next Steps

- Run the [install verification checklist](verify-install.md) to confirm all endpoints are reachable.
- See [Configuration](configuration.md) for a full list of settings and environment variables.
- See [Troubleshooting](troubleshooting.md) if something does not start or connect correctly.
