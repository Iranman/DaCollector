# Troubleshooting

Common problems and how to fix them.

---

## Port and Connectivity

### Web UI or API is not reachable

1. Confirm DaCollector is running. On Windows, check Task Manager or the tray icon. In Docker, run `docker compose ps`.
2. Confirm no other process owns port `38111`:
   ```powershell
   Get-NetTCPConnection -LocalPort 38111 -State Listen
   ```
3. Confirm the firewall allows the port:
   ```powershell
   netsh advfirewall firewall show rule name="DaCollector"
   ```
   Add it if missing:
   ```powershell
   netsh advfirewall firewall add rule name="DaCollector" dir=in action=allow protocol=TCP localport=38111
   ```
4. If you changed the port, confirm you are using the correct value in the URL and that `DACOLLECTOR_PORT` matches.

### Server starts but the Web UI returns 404

The `webui` folder is missing from the publish output. Confirm the publish step ran correctly and that `webui/` is present in the same directory as `DaCollector.exe`.

---

## Docker Networking

### Container is running but `http://127.0.0.1:38111` does not respond

1. Confirm the port mapping is `38111:38111` in `docker-compose.yml`:
   ```powershell
   docker compose ps
   ```
2. Confirm Docker Desktop is running and the container is in the `running` state, not `exited`.
3. Try the container's IP directly if the localhost mapping has an issue:
   ```powershell
   docker inspect dacollector --format '{{.NetworkSettings.IPAddress}}'
   ```

### Plex on the Docker host is unreachable from inside the container

Use `host.docker.internal` instead of `127.0.0.1` or `localhost`:

```yaml
PLEX_TARGET_BASE_URL: "http://host.docker.internal:32400"
```

The `extra_hosts` entry in the Compose file maps this to the Docker host gateway:

```yaml
extra_hosts:
  - "host.docker.internal:host-gateway"
```

### Container appears stuck on first boot

See the [TrueNAS and ZFS section](installation/docker.md#truenas-and-zfs) for ownership repair stalls. The startup log should show `Ownership of ... repaired` or `Ownership ... is correct` before the server starts.

---

## Plex Token

### How to find your Plex token

**Method 1 — From an item URL in Plex Web:**

1. Open Plex Web and click any movie or show.
2. Click the three-dot menu and choose **Get Info**.
3. Look at the URL bar — it contains `X-Plex-Token=<value>`.

**Method 2 — From the Plex API:**

While signed into Plex Web, open the network tab in browser developer tools and look for a request to `plex.tv/users/sign_in.json`. The token appears in the JSON response as `authToken`.

**Method 3 — From a Plex config file (Linux/Docker Plex):**

```bash
cat "/var/lib/plexmediaserver/Library/Application Support/Plex Media Server/Preferences.xml" | grep -o 'PlexOnlineToken="[^"]*"'
```

### Token is rejected by DaCollector

- Confirm the token has not expired. Tokens tied to a Plex Home managed account or a shared library may have restricted access.
- Test directly:
  ```text
  http://127.0.0.1:32400/identity?X-Plex-Token=<your-token>
  ```
  A `200` response with server name confirms the token is valid.
- Confirm `PLEX_TARGET_BASE_URL` uses the correct host and port. Use `http://`, not `https://`, for local Plex on the LAN.

### DaCollector can read libraries but collections do not apply

- Confirm the token belongs to a Plex administrator, not a managed user.
- Confirm `PLEX_TARGET_SECTION_KEY` is set to the numeric section key from:
  ```text
  http://127.0.0.1:32400/library/sections?X-Plex-Token=<your-token>
  ```
- Confirm the collection sync was called with `apply=true`.

---

## Provider Credentials

### TMDB collection builders return no results

- Confirm `TMDB_API_KEY` is set and not empty.
- Test the key directly:
  ```text
  https://api.themoviedb.org/3/configuration?api_key=<your-key>
  ```
  A `200` response confirms the key is valid.
- Confirm DaCollector was restarted after setting the key — environment variables are read at startup.

### TVDB collection builders fail with "API key not configured"

- Confirm `TVDB_API_KEY` is set and not empty.
- If you have a subscriber PIN, set `TVDB_PIN` as well.
- Check the server log for the exact error. A `401` from TVDB means the key or PIN is wrong. A missing-key error means the setting was not picked up at startup.

### IMDb builders fail with "dataset path not found" or "title.basics.tsv not found"

- Confirm `IMDB_DATASET_PATH` points to the folder that contains `title.basics.tsv` or `title.basics.tsv.gz`.
- The path must be accessible by the server process. In Docker, the folder must be mounted into the container.
- `title.basics.tsv` is required. `title.ratings.tsv` is optional unless you use `imdb_chart` builders.
- Test from inside a Docker container:
  ```bash
  docker exec dacollector ls /metadata/imdb/
  ```

---

## SQLite Startup

### Server log shows migration errors or database blocked state

1. Stop DaCollector completely.
2. Back up the data directory before changing anything.
3. Confirm no other DaCollector process is running — two processes opening the same SQLite file will block each other.
4. Check for a `.lock` or `-journal` file in the SQLite folder and remove it only if the server is confirmed stopped.
5. Restart DaCollector and watch the startup log for the migration that fails.

### Clean first-boot test

To verify a clean first boot with no prior data:

```powershell
$env:DACOLLECTOR_HOME = "$env:TEMP\DaCollector-verify"
Remove-Item $env:DACOLLECTOR_HOME -Recurse -Force -ErrorAction SilentlyContinue
.\DaCollector.exe
```

The server should create the SQLite databases, run all migrations in order, and reach ready state without errors. After verifying, delete the temporary directory.

For Docker:

```powershell
docker compose down
docker volume rm dacollector_dacollector-data
docker compose up -d
docker compose logs -f dacollector
```

Remove the volume only when you intentionally want to reset the test instance.

---

## First-Boot and Ownership Issues (Docker)

### Container starts slowly or log shows "Starting ownership repair..."

The container is running a recursive `chown` on the data directory. On a large ZFS dataset (TrueNAS), this can take several minutes.

**Quickest fix:** match `PUID` and `PGID` to the dataset owner so the check passes immediately:

```bash
stat -c '%u:%g' /mnt/your-pool/dacollector-data
```

Set the matching values in `docker-compose.yml`:

```yaml
PUID: "1000"
PGID: "1000"
```

**Alternative:** skip the repair entirely for ACL-managed datasets:

```yaml
SKIP_CHOWN: "true"
```

See [TrueNAS and ZFS](installation/docker.md#truenas-and-zfs) for more detail.

### Server log shows "PluginManager" NullReferenceException

This was caused by an invalid local build version (`0.0.0-local`) and is fixed as of commit `becf507`. If you see it on a current build, confirm your Dockerfile and `compose.yaml` use `version: 0.0.1-local` or later as the default build arg.

---

## Log Locations

| Platform | Log path |
| --- | --- |
| Windows | `C:\ProgramData\DaCollector\logs\` |
| Windows (custom home) | `%DACOLLECTOR_HOME%\logs\` |
| Docker | `/home/dacollector/.dacollector/DaCollector/logs/` — or use `docker compose logs dacollector` |

The server log captures startup, migration, job, and error output. Always read the log before opening a bug report.
