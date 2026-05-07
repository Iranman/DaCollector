# DaCollector Docs

DaCollector is a self-hosted movie and TV collection manager. It keeps a local server-side library database, connects that library to TMDB, IMDb, and TVDB identities, manages provider-driven collections, and exposes Plex-facing workflows for matching and applying collection membership.

## Start Here

| Goal | Page |
| --- | --- |
| Install DaCollector on Windows | [Windows Installation](getting-started/installation/windows.md) |
| Run DaCollector with Docker | [Docker Installation](getting-started/installation/docker.md) |
| View or publish the documentation site | [Publishing Docs](development/publishing-docs.md) |
| Start the server and open the Web UI | [First Run](getting-started/first-run.md) |
| Configure paths, providers, and Plex | [Configuration](getting-started/configuration.md) |
| Build collection rules | [Collection Management](features/collection-management.md) |
| Review duplicate files | [Duplicate Management](features/duplicate-management.md) |
| Connect to Plex | [Plex Target](features/plex-target.md) |

## What DaCollector Manages

- Movie and TV library files imported from managed folders.
- Local metadata and cross-reference records backed by a server database.
- TMDB, IMDb, and TVDB provider settings for identity and collection builders.
- Managed collections that can be previewed, synced, and applied to Plex.
- Exact duplicate cleanup plans, with a dry-run first and admin confirmation for deletes.

## Current Status

DaCollector is under active conversion into a general movie and TV collection manager. The core server, local database, Web UI hosting, provider settings, collection APIs, Plex target APIs, and exact duplicate review page are present. Some older internal model names may remain while the migration continues, but public project-facing documentation and new feature work should use DaCollector.

## Documentation Site

The docs are plain Markdown and can be read directly in GitHub. A `mkdocs.yml` file is included so the same content can later be served with MkDocs Material:

```powershell
pip install mkdocs-material
mkdocs serve
```

Then open `http://127.0.0.1:8000`.
