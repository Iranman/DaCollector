# Docker Installation

DaCollector includes Dockerfiles and a Compose file for running the headless server container. Use Docker when you want the server, database, and Web UI hosted as a long-running service without the Windows tray app.

## Requirements

- Docker Engine or Docker Desktop with Docker Compose v2.
- Enough disk space for the .NET build image, runtime image, local database, metadata cache, and any mounted media folders.
- A persistent volume for `/home/dacollector/.dacollector`.

## Quick Start from GHCR

Use this path after the GitHub Container Registry image has been published.

From the repository root:

```powershell
copy .env.example .env
docker compose -f docker-compose.yml pull
docker compose -f docker-compose.yml up -d
```

Open:

```text
http://127.0.0.1:8111/webui
```

Check container status:

```powershell
docker compose -f docker-compose.yml ps
docker compose -f docker-compose.yml logs -f dacollector
```

Stop the container:

```powershell
docker compose -f docker-compose.yml down
```

The image name is:

```text
ghcr.io/iranman/dacollector:latest
```

Set `DACOLLECTOR_IMAGE_TAG` in `.env` if you want to run a different published tag, for example `sha-abcdef1`.

If GitHub returns an authentication or not found error when pulling the image, confirm that the package has been published and that the GHCR package visibility is public.

## Compose Example

Use `docker-compose.yml` when you want the standard prebuilt GHCR setup. Use [`docker-compose.example.yml`](https://github.com/Iranman/DaCollector/blob/main/docker-compose.example.yml) when you want a more guided starting point with comments and media mount examples.

To start from the example:

```powershell
copy docker-compose.example.yml docker-compose.yml
copy .env.example .env
```

Then edit `docker-compose.yml` and uncomment or add the media mounts for your machine. The example includes Windows and Linux patterns:

```yaml
volumes:
  - dacollector-data:/home/dacollector/.dacollector
  # - "D:/Media/Movies:/media/movies:ro"
  # - "D:/Media/TV:/media/tv:ro"
  # - "/mnt/media/movies:/media/movies:ro"
  # - "/mnt/media/tv:/media/tv:ro"
```

Keep media mounts read-only until you intentionally want DaCollector to delete duplicate files from that path.

## Quick Start from Local Build

From the repository root:

```powershell
copy .env.example .env
docker compose -f compose.yaml up -d --build
```

Open:

```text
http://127.0.0.1:8111/webui
```

Check container status:

```powershell
docker compose -f compose.yaml ps
docker compose -f compose.yaml logs -f dacollector
```

Stop the container:

```powershell
docker compose -f compose.yaml down
```

## Configure `.env`

The included `.env.example` is safe to commit because it contains only placeholders. Copy it to `.env` and edit your local values:

```text
PUID=1000
PGID=1000
UMASK=002
TZ=Etc/UTC
DACOLLECTOR_PORT=8111
DACOLLECTOR_IMAGE_TAG=latest
PLEX_TARGET_BASE_URL=http://host.docker.internal:32400
PLEX_TARGET_SECTION_KEY=
PLEX_TARGET_TOKEN=
TMDB_API_KEY=
IMDB_DATASET_PATH=
IMDB_CACHE_EXPIRATION_DAYS=7
TVDB_API_KEY=
TVDB_PIN=
TVDB_CACHE_EXPIRATION_DAYS=7
```

Do not commit `.env`; it is ignored by Git.

## Persistent Data

The Compose file creates a named volume:

```yaml
volumes:
  dacollector-data:
```

Inside the container, DaCollector uses:

```text
/home/dacollector/.dacollector/DaCollector
```

That path contains settings, SQLite databases, logs, provider cache, images, plugins, and Web UI data.

## Media Folders

Edit `docker-compose.yml`, [`docker-compose.example.yml`](https://github.com/Iranman/DaCollector/blob/main/docker-compose.example.yml), or `compose.yaml` and mount the folders DaCollector should scan. Keep read-only mounts unless you want DaCollector to delete duplicate files from that path.

```yaml
volumes:
  - dacollector-data:/home/dacollector/.dacollector
  - "D:/Media/Movies:/media/movies:ro"
  - "D:/Media/TV:/media/tv:ro"
```

Then add `/media/movies` and `/media/tv` as managed folders in the Web UI.

If you plan to use duplicate deletion, change the relevant mount from `:ro` to `:rw` only after your duplicate cleanup plan has been reviewed.

## Plex on the Docker Host

When Plex runs on the same Windows machine as Docker Desktop, use:

```text
PLEX_TARGET_BASE_URL=http://host.docker.internal:32400
```

When Plex runs on another machine, use its LAN URL:

```text
PLEX_TARGET_BASE_URL=http://192.168.1.50:32400
```

Set `PLEX_TARGET_TOKEN` in `.env`, then use the Plex Target APIs or Web UI status checks to confirm DaCollector can read libraries.

## Change the Port

Set the host-side port in `.env`:

```text
DACOLLECTOR_PORT=8112
```

Restart:

```powershell
docker compose -f docker-compose.yml up -d
```

Open `http://127.0.0.1:8112/webui`.

## ARM64 Build

For an ARM64 container build, set:

```text
DACOLLECTOR_DOCKERFILE=Dockerfile.aarch64
```

Then rebuild:

```powershell
docker compose -f compose.yaml build --no-cache dacollector
docker compose -f compose.yaml up -d
```

## Update

For the GHCR image:

```powershell
docker compose -f docker-compose.yml pull
docker compose -f docker-compose.yml up -d
```

For a local build:

From the repository root:

```powershell
git pull
docker compose -f compose.yaml build --pull dacollector
docker compose -f compose.yaml up -d
```

The named volume is preserved. Back up the volume before testing a branch that may run database migrations.

## Back Up Docker Data

Stop DaCollector before backing up the volume:

```powershell
docker compose -f docker-compose.yml down
docker run --rm -v dacollector_dacollector-data:/data -v "${PWD}:/backup" alpine tar czf /backup/dacollector-data.tgz -C /data .
```

Restore only into a stopped container and only when you intend to replace the current data.
