# Docker Installation

DaCollector includes Dockerfiles and Docker Compose files for running the headless server container. Use Docker when you want the server, database, and Web UI hosted as a long-running service without the Windows tray app.

## Requirements

- Docker Engine or Docker Desktop with Docker Compose v2.
- A persistent volume for `/home/dacollector/.dacollector`.
- Media folders mounted into the container so DaCollector can scan them.

## Docker Compose (Recommended)

Docker Compose is the recommended Docker install method because it keeps the container, ports, persistent data, and media mounts in one file.

Start with the included [`docker-compose.example.yml`](https://github.com/Iranman/DaCollector/blob/main/docker-compose.example.yml) if you want comments while editing:

```powershell
copy docker-compose.example.yml docker-compose.yml
```

Then edit `docker-compose.yml` for your machine. At minimum, review:

- `PUID` and `PGID`: the user and group that should own DaCollector files.
- `TZ`: your timezone.
- `ports`: the host port for the Web UI.
- `volumes`: the host media folders DaCollector should scan.
- `PLEX_TARGET_BASE_URL`, `PLEX_TARGET_SECTION_KEY`, and `PLEX_TARGET_TOKEN` if you want Plex collection sync.
- `TMDB_API_KEY`, `TVDB_API_KEY`, and `IMDB_DATASET_PATH` if you use those providers.

### docker-compose.yml

```yaml
services:
  dacollector:
    image: ghcr.io/iranman/dacollector:latest
    shm_size: 256m
    container_name: dacollector
    restart: unless-stopped
    ports:
      - "38111:38111"
    environment:
      PUID: "1000"
      PGID: "1000"
      UMASK: "002"
      TZ: Etc/UTC
      DACOLLECTOR_HOME: /home/dacollector/.dacollector/DaCollector
      PLEX_TARGET_BASE_URL: http://host.docker.internal:32400
      PLEX_TARGET_SECTION_KEY: ""
      PLEX_TARGET_TOKEN: ""
      TMDB_API_KEY: ""
      IMDB_DATASET_PATH: ""
      IMDB_CACHE_EXPIRATION_DAYS: "7"
      TVDB_API_KEY: ""
      TVDB_PIN: ""
      TVDB_CACHE_EXPIRATION_DAYS: "7"
    volumes:
      - dacollector-data:/home/dacollector/.dacollector
      - "D:/Media/Movies:/media/movies:ro"
      - "D:/Media/TV:/media/tv:ro"
    extra_hosts:
      - "host.docker.internal:host-gateway"

volumes:
  dacollector-data:
```

Replace the media paths before running the container. On Linux, paths normally look like:

```yaml
volumes:
  - dacollector-data:/home/dacollector/.dacollector
  - "/mnt/media/movies:/media/movies:ro"
  - "/mnt/media/tv:/media/tv:ro"
```

Keep media mounts read-only until you intentionally want DaCollector to delete duplicate files from that path.

## Start DaCollector

From the folder containing `docker-compose.yml`:

```powershell
docker compose pull
docker compose up -d
```

Open:

```text
http://127.0.0.1:38111/webui
```

Check container status:

```powershell
docker compose ps
docker compose logs -f dacollector
```

Stop the container:

```powershell
docker compose down
```

If GitHub returns an authentication or not found error when pulling the image, confirm that the package has been published and that the GHCR package visibility is public.

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

## Plex on the Docker Host

When Plex runs on the same Windows machine as Docker Desktop, use:

```yaml
PLEX_TARGET_BASE_URL: http://host.docker.internal:32400
```

When Plex runs on another machine, use its LAN URL:

```yaml
PLEX_TARGET_BASE_URL: http://192.168.1.50:32400
```

Set `PLEX_TARGET_TOKEN` directly in `docker-compose.yml`, then use the Plex Target APIs or Web UI status checks to confirm DaCollector can read libraries.

## Change the Port

Change the host-side port in `docker-compose.yml`:

```yaml
ports:
  - "38112:38111"
```

Restart:

```powershell
docker compose up -d
```

Open `http://127.0.0.1:38112/webui`.

## Local Build

Use `compose.yaml` when you want to build the image from this repository instead of pulling the GHCR image:

```powershell
docker compose -f compose.yaml up -d --build
```

For an ARM64 container build, edit `compose.yaml` and set:

```yaml
dockerfile: Dockerfile.aarch64
```

Then rebuild:

```powershell
docker compose -f compose.yaml build --no-cache dacollector
docker compose -f compose.yaml up -d
```

## Update

For the recommended GHCR image:

```powershell
docker compose pull
docker compose up -d
```

For a local build:

```powershell
git pull
docker compose -f compose.yaml build --pull dacollector
docker compose -f compose.yaml up -d
```

The named volume is preserved. Back up the volume before testing a branch that may run database migrations.

## Back Up Docker Data

Stop DaCollector before backing up the volume:

```powershell
docker compose down
docker run --rm -v dacollector_dacollector-data:/data -v "${PWD}:/backup" alpine tar czf /backup/dacollector-data.tgz -C /data .
```

Restore only into a stopped container and only when you intend to replace the current data.
