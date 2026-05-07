<p align="center">
  <img src="icon.png" alt="DaCollector logo" width="160">
</p>

# DaCollector

DaCollector is a self-hosted movie and TV collection manager. It combines a server-first media library backend with provider-driven collection automation and Plex-facing relay workflows.

The project direction is:

- Local movie and TV library scanning across managed folders.
- Metadata identity from TMDB, IMDb, and TVDB.
- Rule-driven collection management inspired by provider builders.
- Duplicate detection for exact file duplicates, duplicate media releases, and likely fuzzy matches.
- Plex adapter support based on a server-as-source-of-truth model.
- API-first backend for web UI, automation, and media-server integrations.

## Source Lineage

The active project home is `Iranman/DaCollector`.

DaCollector is being built from upstream server, collection automation, and Plex relay references. Upstream copyright, license, and attribution notices are maintained in `NOTICE.md`. Public branding and new project-facing behavior should use **DaCollector**.

## Development

This workspace uses a local .NET SDK installed at:

```powershell
F:\Collection manager\.dotnet\dotnet.exe
```

Build from this directory with:

```powershell
$env:DOTNET_CLI_HOME='F:\Collection manager\dotnet-home'
$env:DOTNET_ROOT='F:\Collection manager\.dotnet'
..\.dotnet\dotnet.exe build DaCollector.sln
```

## Current Implementation Notes

The first implementation slice adds generic media/provider identity and collection definitions while leaving existing backend behavior intact. This lets the project migrate from anime-specific models toward movie and TV models without breaking the inherited server.

## Documentation

Start with [DaCollector Docs](docs/index.md).

- [Windows installation](docs/getting-started/installation/windows.md)
- [First run](docs/getting-started/first-run.md)
- [Configuration](docs/getting-started/configuration.md)
- [Collection management](docs/features/collection-management.md)
- [Duplicate management](docs/features/duplicate-management.md)
- [Plex target](docs/features/plex-target.md)
