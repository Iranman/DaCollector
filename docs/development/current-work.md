# Current Work

DaCollector is in the middle of the conversion from the inherited media server into a movie and TV collection manager. This page tracks the current engineering priorities for contributors.

## Current Branch State

- The server defaults to port `38111`.
- The docs, Docker files, Windows install notes, and DaCollector branding are in place.
- Managed collections, Plex target APIs, and exact duplicate review pages exist.
- TVDB provider support has models, repositories, jobs, and a metadata service in progress.
- Internal domain names are being converted from `AnimeSeries`, `AnimeGroup`, and `AnimeEpisode` to `MediaSeries`, `MediaGroup`, and `MediaEpisode`.

## Immediate Priorities

1. Make the branch compile after the domain rename.
2. Repair database migrations so historical migration commands stay unchanged and new rename steps are append-only.
3. Add SQLite migration smoke tests for a fresh database and an upgraded database.
4. Add tests for TVDB token caching, retry behavior, missing credentials, show refresh, and movie refresh.
5. Update API documentation after the metadata and TVDB endpoint shape is stable.

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

If restore is required:

```powershell
dotnet restore DaCollector.sln
dotnet build DaCollector.sln
dotnet test DaCollector.Tests/DaCollector.Tests.csproj
```
