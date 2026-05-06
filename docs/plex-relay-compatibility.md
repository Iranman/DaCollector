# Plex Relay Compatibility Inventory

This file tracks what the upstream Plex relay bundle expects from the inherited server API and how those flows should map to DaCollector.

Relay snapshot: `c06ec96`.

## Role In The Fork

The upstream Plex relay bundle is the reference for Plex-facing behavior:

- Scanner: turn local file paths into Plex episode/movie entries by asking DaCollector what each file represents.
- Agent: turn DaCollector metadata into Plex metadata fields.
- Scripts: automate collection posters, metadata refresh, watched/rating sync, and recent rescans.

The current upstream relay code is a legacy Plex bundle. DaCollector should preserve the behavior and data flow, but implement new code against the modern Plex integration path where possible.

## Existing Relay Endpoint Usage

Authentication:

- `POST /api/auth`

Scanner:

- `GET /api/v3/File/PathEndsWith/{path}?include=XRefs`
- `GET /api/v3/File/PathEndsWith?path={path}&include=XRefs&limit=0`
- `GET /api/v3/Series/{id}?includeDataFrom=AniDB,TMDB`
- `GET /api/v3/Episode/{id}?includeDataFrom=AniDB,TMDB`

Metadata agent:

- `GET /api/v3/Series/Search?query={query}&fuzzy=false&limit=10`
- `GET /api/v3/Series/{id}/AniDB`
- `GET /api/v3/Series/{id}?includeDataFrom=AniDB,TMDB`
- `GET /api/v3/Series/{id}/Cast`
- `GET /api/v3/Series/{id}/Cast?roleType=Studio`
- `GET /api/v3/Series/{id}/Cast?roleType=Staff&roleDetails=Work`
- `GET /api/v3/Series/{id}/Tags?filter=1&excludeDescriptions=true&orderByName=false&onlyVerified=true`
- `GET /api/v3/Series/{id}/Group`
- `GET /api/v3/Series/{id}/Images?includeDisabled=false`
- `GET /api/v3/Series/{id}/Episode?pageSize=0`
- `GET /api/v3/Series/{id}/TMDB/Season?include=Images`
- `GET /api/v3/Episode/{id}?includeDataFrom=AniDB,TMDB`
- `GET /api/v3/Image/{source}/{type}/{id}`
- `GET /api/v3/Image/TMDB/Logo{relativeFilepath}`

Collection poster automation:

- `GET /api/v3/Group?pageSize=1&page=1&includeEmpty=false&randomImages=false&topLevelOnly=true&startsWith={title}`
- `GET /api/v3/Image/{source}/Poster/{id}`

Watched and rating sync:

- `GET /api/v3/Episode?pageSize=0&page=1&includeWatched=Only&includeFiles=true`
- `GET /api/v3/Episode?pageSize=0&page=1&includeVoted=Only&includeFiles=true`
- `GET /api/v3/Series?pageSize=0&page=1`
- `GET /api/v3/Series/{id}/Episode?pageSize=0&page=1&includeFiles=true`
- `POST /api/v3/Episode/{id}/Watched/true`
- `POST /api/v3/Episode/{id}/Vote`
- `POST /api/v3/Series/{id}/Vote`

Recent rescan automation:

- `GET /api/v3/ImportFolder`
- `GET /api/v3/ImportFolder/{id}/Scan`
- `GET /api/v3/Action/RemoveMissingFiles/true`
- `GET /api/v3/Dashboard/RecentlyAddedSeries?pageSize={count}&page=1&includeRestricted=true`
- `GET /api/v3/Series/{id}/Episode?pageSize=1&page=1&includeFiles=true&includeMediaInfo=false&includeAbsolutePaths=true&fuzzy=true`

AnimeThemes support:

- Uses inherited `File/PathEndsWith` behavior to resolve AniDB IDs, then calls AnimeThemes externally.
- This should stay anime-specific or become an optional plugin, not part of the core movie/TV path.

## Movie/TV Replacement Endpoints

The new server should expose generic equivalents before a new Plex adapter is built:

- `GET /api/v3/Media/File/PathEndsWith`
- `GET /api/v3/Media/Movie/{id}`
- `GET /api/v3/Media/Show/{id}`
- `GET /api/v3/Media/Season/{id}`
- `GET /api/v3/Media/Episode/{id}`
- `GET /api/v3/Media/Search`
- `GET /api/v3/Media/{id}/Images`
- `GET /api/v3/Media/{id}/Cast`
- `GET /api/v3/Media/{id}/Tags`
- `GET /api/v3/Collections`
- `GET /api/v3/Collections/{id}`
- `POST /api/v3/Media/Movie/{id}/Watched/{watched}`
- `POST /api/v3/Media/Episode/{id}/Watched/{watched}`
- `POST /api/v3/Media/Movie/{id}/Vote`
- `POST /api/v3/Media/Show/{id}/Vote`
- `POST /api/v3/Media/Episode/{id}/Vote`

## Compatibility Notes

- The upstream relay assumes all local entries are series and episodes. Movie support needs a separate movie path rather than forcing movies through series semantics.
- Relay can map files containing multiple episodes and episodes spanning multiple files. Preserve that behavior for TV episodes.
- Relay uses TMDB season/episode ordering when available. Preserve this for TV shows.
- The upstream relay relies on the server as source of truth and uses Plex as a projection. Keep that model.
- The upstream relay's collection poster script maps Plex collections back to inherited groups. Replace this with collection definitions and collection image APIs.
- The upstream relay's watched sync is file-path driven. Prefer provider/media IDs when available, with path fallback for migration.
- The upstream relay repository has no license file in this checkout. Keep direct code reuse out of the server until licensing is resolved.
