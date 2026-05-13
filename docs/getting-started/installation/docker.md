# Docker Installation

DaCollector ships a self-contained Docker image on GHCR and a `docker-compose.yml` that is the recommended way to run it as a long-running server service. The image includes the server and the React Web UI in one container.

## Requirements

- Docker Engine 24+ or Docker Desktop with Compose v2.
- A Linux or Windows host with Docker and at least 512 MB free RAM.
- Your media libraries mounted or accessible to the host.

---

## Step 1 — Get the files

Download `docker-compose.yml` from the repository to a permanent folder on your server. You do not need to clone the source repos for a normal install.

```bash
mkdir ~/dacollector && cd ~/dacollector

curl -O https://raw.githubusercontent.com/Iranman/DaCollector/main/docker-compose.yml
```

Or clone the repo and copy the compose file:

```bash
cp docker-compose.yml ~/dacollector/
cd ~/dacollector
```

---

## Step 2 — Edit `docker-compose.yml`

```bash
nano docker-compose.yml      # or any editor
```

Fill in at minimum:

| Variable | What to set |
| --- | --- |
| `PUID` / `PGID` | Owner of your media folders (`id <user>` to find them) |
| `TZ` | Your timezone, e.g. `America/New_York` |
| `PLEX_TARGET_BASE_URL` | `http://host.docker.internal:32400` if Plex is on the same machine, or `http://192.168.1.50:32400` for another machine on your LAN |
| `PLEX_TARGET_TOKEN` | Your Plex token (see [Finding your Plex token](../../getting-started/troubleshooting.md#how-to-find-your-plex-token)) |
| `PLEX_TARGET_SECTION_KEY` | Numeric library section key (see below) |
| `TMDB_API_KEY` | Free key from [themoviedb.org/settings/api](https://www.themoviedb.org/settings/api) |

Leave `TVDB_API_KEY` and `TVDB_PIN` blank to disable TVDB for now. You can add them later.

### Finding your Plex library section key

While Plex is running, open this URL in a browser (replace token and host):

```text
http://127.0.0.1:32400/library/sections?X-Plex-Token=<your-token>
```

The `key` attribute on each `<Directory>` element is the section key. Use the numeric key for your movies or TV library.

---

## Step 3 — Add your media paths

Open `docker-compose.yml` and uncomment the media volume lines that apply to you, then replace the example host paths with your actual paths:

```yaml
# In docker-compose.yml, uncomment and edit these lines:
- "/mnt/media/movies:/media/movies:ro"
- "/mnt/media/tv:/media/tv:ro"
```

Keep `:ro` (read-only) unless you intentionally want DaCollector to delete duplicate files from that location.

---

## Step 4 — Start the container

```bash
docker compose -f docker-compose.yml pull
docker compose -f docker-compose.yml up -d
```

The first pull downloads the image. The first start creates the data volume, runs database migrations, and starts the server.

Check that the container is running:

```bash
docker compose -f docker-compose.yml ps
```

Watch the startup logs until the server is ready:

```bash
docker compose -f docker-compose.yml logs -f dacollector
```

You should see a startup summary block followed by the Kestrel listener line:

```
-------------------------------------
User ID:   1000
Group ID:  1000
UMASK set: 002
Directory: "/home/dacollector/.dacollector/DaCollector"
Owner:     1000:1000
-------------------------------------
Now listening on: http://0.0.0.0:38111
```

---

## Step 5 — Open the Web UI

```text
http://<server-ip>:38111/webui
```

The setup wizard runs on first boot. Create your admin account, then follow the [First Run](../../getting-started/first-run.md) guide to connect Plex and configure providers.

No separate Web UI container is required. The GHCR image already contains the built DaCollector WebUI files and serves them from the same port as the API.

---

## Daily log access

Server logs are written to `./logs/` in the same directory as `docker-compose.yml` — no `docker exec` required:

```bash
# Watch live
tail -f logs/DaCollector.log

# Or read today's log file (JSONL format)
ls logs/
```

You can also stream container stdout (entrypoint messages only):

```bash
docker compose -f docker-compose.yml logs --tail 100 dacollector
```

---

## Update

```bash
docker compose -f docker-compose.yml pull
docker compose -f docker-compose.yml up -d
```

The named data volume is preserved. Database migrations run automatically on startup. Back up the data volume before testing a release that may run migrations:

```bash
docker compose -f docker-compose.yml down
docker run --rm \
  -v dacollector_dacollector-data:/data \
  -v "$(pwd)":/backup \
  alpine tar czf /backup/dacollector-data.tgz -C /data .
docker compose -f docker-compose.yml up -d
```

---

## Change the port

In `docker-compose.yml`, change the host side of the port mapping:

```yaml
ports:
  - "38112:38111"
```

Then restart:

```bash
docker compose -f docker-compose.yml up -d
```

---

## TrueNAS and ZFS

### How ownership repair works

On startup the entrypoint checks whether `DACOLLECTOR_HOME` is owned by `PUID:PGID`.

- If ownership is already correct, nothing changes.
- If it is wrong, the entrypoint always fixes the top-level directory itself (one fast `chown`) so the server can write new files. Whether it also recurses into existing data depends on `SKIP_CHOWN`.

On ZFS/NFS/CIFS volumes the recursive step is skipped automatically — one `chown` per directory is instant; recursing into thousands of inodes is not.

### Slow first-boot (ownership repair stall)

On a non-ZFS volume the container may appear stuck after:

```
Ownership mismatch on /home/dacollector/.dacollector/DaCollector
Starting recursive ownership repair (this may be slow on large datasets).
```

The container is recursively `chown`-ing the data directory to match `PUID:PGID`. On a dataset with many inodes this can take minutes.

**Preferred fix — match `PUID`/`PGID` to the dataset owner:**

```bash
stat -c '%u:%g' /mnt/your-pool/dacollector-data
```

Set the matching values in `docker-compose.yml`:

```yaml
environment:
  PUID: "1000"
  PGID: "1000"
```

On the next start, the ownership check passes immediately and no `chown` is needed.

**Alternative — skip recursive `chown` for ACL-managed datasets:**

```yaml
# In docker-compose.yml:
SKIP_CHOWN: "true"
```

With `SKIP_CHOWN=true` the entrypoint still fixes the top-level `DACOLLECTOR_HOME` directory so the server can start cleanly, but it does not recurse into existing data files. The startup log will confirm:

```
Top-level directory ownership set to 1000:100.
Recursive chown skipped (SKIP_CHOWN=true). Existing files inside ... keep their previous ownership.
```

### Running as root (PUID=0)

If you prefer to run the container as root, set `PUID=0` in `docker-compose.yml`:

```yaml
environment:
  PUID: "0"
  PGID: "0"
```

Root bypasses all user creation and ownership repair — the server process can write to `DACOLLECTOR_HOME` regardless of its ownership. The default data path (`/home/dacollector/.dacollector/DaCollector`) works with `PUID=0`; no custom path is required.

### Diagnosing a permission error

If the server fails to start with `Access to the path '...' is denied`, check that `DACOLLECTOR_HOME` is writable by the configured `PUID`:

```bash
# Check current ownership of the data directory
docker exec dacollector stat -c '%u:%g %n' /home/dacollector/.dacollector/DaCollector

# Check which user the server process runs as
docker exec dacollector id dacollector
```

Fix options:

- **Match `PUID`/`PGID` to the directory owner** (preferred — no permissions change needed).
- **Fix host ownership** — on TrueNAS: `chown -R 1000:1000 /mnt/your-pool/dacollector-data`.
- **Use `PUID=0`** — root can always write, no ownership changes required.

### Diagnosing a stalled container

```bash
# Is the container alive?
docker compose -f docker-compose.yml ps

# What is it doing?
docker compose -f docker-compose.yml logs --tail 30 dacollector

# Check current ownership
docker exec dacollector stat -c '%u:%g %n' /home/dacollector/.dacollector/DaCollector
```

If the log shows `Starting recursive ownership repair...` but nothing after it, the recursive `chown` is still running. Wait for it to finish, or stop the container and set `SKIP_CHOWN: "true"` in `docker-compose.yml`.

### Media mounts on TrueNAS

Mount media under `/media/movies`, `/media/tv`, etc. — not under `/home/dacollector`. DaCollector's ownership repair only targets `$DACOLLECTOR_HOME`; paths outside that scope are not affected.

---

## Back up and restore

Stop the container before backing up:

```bash
docker compose -f docker-compose.yml down
docker run --rm \
  -v dacollector_dacollector-data:/data \
  -v "$(pwd)":/backup \
  alpine tar czf /backup/dacollector-data.tgz -C /data .
```

Restore (replaces current data):

```bash
docker compose -f docker-compose.yml down
docker run --rm \
  -v dacollector_dacollector-data:/data \
  -v "$(pwd)":/backup \
  alpine sh -c "cd /data && tar xzf /backup/dacollector-data.tgz"
docker compose -f docker-compose.yml up -d
```

---

## Local build

This section is for developers only. Server installs should use the prebuilt image above.

To build from source and include the React Web UI, clone the server repo and run Compose from the repo root:

```bash
git clone https://github.com/Iranman/DaCollector.git
cd DaCollector
docker compose up -d --build
```

The source compose file uses `Dockerfile.combined`. That build:

1. Clones `Iranman/DaCollector-WebUI` during the Docker build.
2. Builds the Web UI with `npm run build`.
3. Copies the Web UI `dist/` files into `DaCollector.Server/webui`.
4. Builds the DaCollector server image.
5. Serves the Web UI from the same container at `http://<server-ip>:38111/webui`.

To build a fork or a specific Web UI branch/tag/SHA, pass the repo and ref inline:

```bash
DACOLLECTOR_WEBUI_REPO=https://github.com/Iranman/DaCollector-WebUI.git DACOLLECTOR_WEBUI_REF=main docker compose up -d --build
```

No separate Web UI container is required.

For ARM64 (e.g. Raspberry Pi, Apple Silicon), the combined Dockerfile selects the correct Linux runtime automatically:

```bash
docker compose build --no-cache dacollector
docker compose up -d
```
