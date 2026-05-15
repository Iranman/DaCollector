# Current Work

DaCollector is past the initial server MVP conversion and is currently focused on first-install readiness. This page tracks the current engineering priorities for contributors.

## Current Branch State

- The server defaults to container port `38111`.
- Docker Desktop local verification passed on default host port `38111`.
- The combined Docker build embeds the React WebUI and starts successfully with local build metadata.
- First-run setup completed in the local Docker container and `/api/v3/Init/Status` reached `State=Started`.
- Managed collections, Plex target APIs, generic media APIs, unmatched file review, provider match candidates, database backups, and exact/media duplicate review surfaces exist.
- Domain model names have been converted to `MediaSeries`, `MediaGroup`, and `MediaEpisode` for local DaCollector concepts.

## Immediate Priorities

1. Repeat Docker verification on the TrueNAS/Linux Docker host using default host port `38111`.
2. Verify Plex connectivity from inside the Docker container with a host-reachable Plex URL and token.
3. Run a collection preview/safe sync and Plex media duplicate review from Docker once Plex is reachable.
4. Keep release/first-run docs aligned with the exact Docker command, port, and verification status.
5. Continue Relay work after server first-install readiness is stable; current Relay image and per-item metadata work is still scaffold-level.

## Docker WebUI Release Status

Status recorded 2026-05-14:

- Server packaging commit pushed: `8b6f11b8c87fd28caf9efb81e36134e0769a330c` (`Bundle updated WebUI into latest container image`).
- Server startup fix pushed: `b8bf0e496a4fbbb260974f3d7cd257ad84eb2d32` (`Allow startup without AniDB UDP jobs`).
- Current Server `main` observed after a follow-up push: `af2f2436b7b0dee65b13a594e4b6cbf2b001f792`.
- WebUI commit pushed: `d8629ec1f423e332495fecce7a9e431ba5a7d7bc` (`Trigger server image rebuild after WebUI build`).
- WebUI CI run: https://github.com/Iranman/DaCollector-WebUI/actions/runs/25884566213. Checkout, install, and build passed; `Dispatch server Docker image build` failed because `DACOLLECTOR_SERVER_DISPATCH_TOKEN` was missing or inaccessible.
- Server unit-test runs passed for `8b6f11b`, `b8bf0e4`, and `af2f243`.
- Server GHCR runs passed:
  - https://github.com/Iranman/DaCollector/actions/runs/25884513329 (`8b6f11b`)
  - https://github.com/Iranman/DaCollector/actions/runs/25885872189 (`b8bf0e4`)
  - https://github.com/Iranman/DaCollector/actions/runs/25885888318 (`af2f243`)
- Latest image verified locally: `ghcr.io/iranman/dacollector@sha256:49e27538144dc8e89d5a1999170dbbbe3d73647e48151346e4003f2fda84273d`, created `2026-05-14T21:11:43Z`.
- Compose pull/up verification passed locally with `docker-compose.yml`; container reached `healthy`, `/webui` returned HTTP 200, `/api/v3/Init/Status` returned HTTP 200 in setup mode, and `/app/webui` was copied into `/home/dacollector/.dacollector/DaCollector/webui`.
- Existing-install WebUI replacement was verified locally by changing installed `webui/version.json` to an older metadata revision with the same semantic version and recreating the container; startup restored the bundled metadata.
- `DACOLLECTOR_SERVER_DISPATCH_TOKEN` was added to `Iranman/DaCollector-WebUI` and verified by a successful WebUI dispatch after a permission correction. The PAT expiration is not visible from Actions or this checkout; track the renewal date in the GitHub token owner account or the repo maintenance notes.
- WebUI dispatch verification commit pushed: `93d3f0a995996a19b9ba1b2dde30a9a02ff76a48` (`Retry WebUI dispatch after permission update`).
- WebUI CI dispatch run passed: https://github.com/Iranman/DaCollector-WebUI/actions/runs/25898228039.
- Server `repository_dispatch` GHCR run passed: https://github.com/Iranman/DaCollector/actions/runs/25898239460.
- Server metadata packaging fix pushed: `d715ff8c094190c61d7875206d37129a6208e683` (`Preserve WebUI build metadata in container image`).
- Server GHCR run for the metadata fix passed: https://github.com/Iranman/DaCollector/actions/runs/25898966060.
- Final latest image verified locally: `ghcr.io/iranman/dacollector@sha256:1eb3de57dd111cb857806fb84dce71d241418632120c2d1f88cb8e14e2c78c45`, created `2026-05-15T03:43:19Z`.
- Final image `/app/webui/version.json` and installed `/home/dacollector/.dacollector/DaCollector/webui/version.json` both reported WebUI git `93d3f0a995996a19b9ba1b2dde30a9a02ff76a48`, package `1.0.0`, minimum server `0.0.1`, and channel `Stable`.
- Final compose pull/up verification passed locally with `docker-compose.yml`; container reached `healthy`, `/webui` returned HTTP 200, and `/api/v3/Init/Status` returned HTTP 200.

## Migration Rules

- Do not edit historical database migration commands to use new names.
- Add new migration steps at the end of each backend's command list.
- Keep provider names such as `AniDB_Anime`, `AniDB_Episode`, `TMDB_Show`, and `TVDB_Show` when referring to external provider cache rows.
- Use `MediaSeries`, `MediaGroup`, and `MediaEpisode` for DaCollector's local domain in new code and docs.
- Keep legacy v1 DTO names such as `CL_AnimeSeries_User` unless intentionally versioning the API.

## Validation

The repo currently expects .NET SDK `10.0.203` from `global.json`.

After the SDK is available, run:

```powershell
dotnet build DaCollector.sln --no-restore
dotnet test DaCollector.Tests/DaCollector.Tests.csproj --no-restore
dotnet test DaCollector.IntegrationTests/DaCollector.IntegrationTests.csproj --no-restore
git diff --check daccollector/main..HEAD
```

For Docker startup verification on the default local port:

```powershell
docker compose -f compose.yaml up -d --build --force-recreate
.\scripts\verify-install.ps1 -Port 38111 -Docker
```

If local port `38111` is already in use by a debug server, stop that process or set `DACOLLECTOR_PORT` to an alternate host port before running Compose.

If restore is required:

```powershell
dotnet restore DaCollector.sln
dotnet build DaCollector.sln
dotnet test DaCollector.Tests/DaCollector.Tests.csproj
```
