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

Leave `TVDB_API_KEY`, `TVDB_PIN`, and `IMDB_DATASET_PATH` blank to disable those providers for now. You can add them later.

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

### Slow first-boot (ownership repair stall)

On a TrueNAS or ZFS-backed Docker volume, the container may appear stuck after:

```
Ownership mismatch on /home/dacollector/.dacollector/DaCollector
Starting ownership repair. This may be slow on large datasets or ZFS/TrueNAS volumes.
```

The container is recursively `chown`-ing the data directory to match `PUID:PGID`. On a ZFS dataset with thousands of inodes, this can take minutes.

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

On the next start, the ownership check passes immediately and `chown` is skipped.

**Alternative — skip `chown` entirely for ACL-managed datasets:**

```yaml
# In docker-compose.yml:
SKIP_CHOWN: "true"
```

The startup log will confirm:

```
Ownership repair skipped (SKIP_CHOWN=true). Owner of /home/dacollector/.dacollector/DaCollector: 1000:1000
```

### Diagnosing a stalled container

```bash
# Is the container alive?
docker compose -f docker-compose.yml ps

# What is it doing?
docker compose -f docker-compose.yml logs --tail 30 dacollector

# Check current ownership
docker exec dacollector stat -c '%u:%g %n' /home/dacollector/.dacollector/DaCollector
```

If the log shows `Starting ownership repair...` but nothing after it, the recursive `chown` is still running. Wait for it to finish, or stop the container and set `SKIP_CHOWN: "true"` in `docker-compose.yml`.

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

To build from source and include the React Web UI, clone both repos next to each other:

```text
/mnt/PLEX/Apps/
├── DaCollector/
└── DaCollector-WebUI/
```

Then run Compose from the server repo:

```bash
cd /mnt/PLEX/Apps/DaCollector
docker compose up -d --build
```

The source compose file uses `Dockerfile.combined`. That build:

1. Builds `../DaCollector-WebUI` with `npm run build`.
2. Copies the Web UI `dist/` files into `DaCollector.Server/webui`.
3. Builds the DaCollector server image.
4. Serves the Web UI from the same container at `http://<server-ip>:38111/webui`.

If your Web UI repo is not next to the server repo, pass the path inline:

```bash
DACOLLECTOR_WEBUI_CONTEXT=/mnt/PLEX/Apps/DaCollector-WebUI docker compose up -d --build
```

No separate Web UI container is required.

For ARM64 (e.g. Raspberry Pi, Apple Silicon), the combined Dockerfile selects the correct Linux runtime automatically:

```bash
docker compose build --no-cache dacollector
docker compose up -d
```
