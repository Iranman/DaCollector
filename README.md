# The Collector

The Collector is a self-hosted movie and TV collection manager. It combines a server-first media library backend with provider-driven collection automation and Plex-facing relay workflows.

The project direction is:

- Local movie and TV library scanning across managed folders.
- Metadata identity from TMDB, IMDb, and TVDB.
- Rule-driven collection management inspired by provider builders.
- Duplicate detection for exact file duplicates, duplicate media releases, and likely fuzzy matches.
- Plex adapter support based on a server-as-source-of-truth model.
- API-first backend for web UI, automation, and media-server integrations.

## Source Lineage

The Collector is being built from these upstream projects:

- `ShokoAnime/ShokoServer`: backend, API, managed folders, file hashing, TMDB cache, duplicate-file workflows, and server infrastructure.
- `Kometa-Team/Kometa`: collection-builder vocabulary and provider automation behavior.
- `natyusha/ShokoRelay.bundle`: Plex scanner, metadata-agent, poster sync, watched sync, and refresh workflow reference.

Upstream copyright, license, and attribution notices must remain intact. Public branding and new project-facing behavior should use **The Collector**.

## Development

This workspace uses a local .NET SDK installed at:

```powershell
F:\Collection manager\.dotnet\dotnet.exe
```

Build from this directory with:

```powershell
$env:DOTNET_CLI_HOME='F:\Collection manager\dotnet-home'
$env:DOTNET_ROOT='F:\Collection manager\.dotnet'
..\.dotnet\dotnet.exe build Shoko.Server.sln
```

## Current Implementation Notes

The first implementation slice adds generic media/provider identity and collection definitions while leaving existing backend behavior intact. This lets the project migrate from anime-specific models toward movie and TV models without breaking the inherited server.
