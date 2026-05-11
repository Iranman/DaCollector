# DaCollector Blueprint

DaCollector uses an upstream server base, a Plex-facing scanner/agent/automation reference, and a collection/provider behavior reference. The active fork and project home is `Iranman/DaCollector`.

Source snapshots:

- Server base reference: `56707a9` on `master`
- Plex relay reference: `c06ec96` on `master`
- Collection automation reference: `15b1e1f` on `master`

Licensing and attribution notes are maintained in `NOTICE.md`. Treat the Plex relay checkout as an integration reference until licensing or permission is confirmed. Do not directly copy relay code into the server without resolving that.

## Product Target

Build DaCollector as a self-hosted movie and TV show collection manager with:

- Local library scanning across managed folders.
- First-class movies, shows, seasons, episodes, files, collections, and duplicate sets.
- File fingerprinting, technical metadata extraction, and local database tracking.
- Matching against TMDB and TVDB for the first public provider surface.
- Future metadata enrichment from IMDb or other legal provider feeds only as explicit provider modules with settings, tests, and docs.
- Collection automation inspired by upstream provider builders.
- Exact and near-duplicate detection with safe review before deletion.
- Missing-file and corrupt-file review.
- Safe rename/move planning and execution.
- API-first design so a Web UI, Plex/Jellyfin integrations, and automation jobs can all use the same backend.

Product boundaries:

- DaCollector manages local files the user already has and has mounted/provided to the server.
- DaCollector does not download media, stream from websites, bypass filesystem permissions, or access files outside configured folders.
- Destructive file operations must be opt-in, reviewed, and admin-confirmed.
- DaCollector is not Radarr, Sonarr, qBittorrent, Plex, or Jellyfin. It is the local metadata brain between user-owned files and media applications.

Project split:

- **DaCollector Server** is the backend source of truth for scanning, fingerprinting, matching, metadata, watch state, duplicate/missing/corrupt review, rename/move operations, collections, and APIs.
- **DaCollector WebUI** is the browser interface over server workflows. It must not own filesystem scanning, provider matching, or Plex scanner/agent logic.
- **DaCollector Relay** is the planned Plex scanner/agent/adapter that projects DaCollector-managed movies and TV shows into Plex while keeping Server as the source of truth.

## Detailed MVP Direction

The first usable DaCollector version should stay smaller than the full integration vision. Build the base before plugins:

1. Docker container and first-run WebUI.
2. SQLite-backed server settings and health/status endpoints.
3. Movie and TV library folder setup.
4. Folder scanning and media-file records.
5. Filename parsing for movie, TV episode, date-based episode, multi-episode, quality, source, codec, HDR, and explicit provider IDs.
6. TMDB movie matching and metadata cache.
7. TVDB show/season/episode matching and metadata cache.
8. Unmatched files and suggested-match review.
9. Manual match, ignore, lock, and undo workflows.
10. Basic poster/backdrop download and local artwork cache.
11. Basic logs and backups.

Explicitly defer these until the base is reliable:

- Plex plugin, scanner, or metadata agent.
- Jellyfin plugin.
- Kodi addon.
- Watched-sync integrations.
- Trakt sync.
- Advanced duplicate cleanup.
- Complex collection automation beyond the existing managed-collection foundation.

The current server API is v3. Roadmap examples that use `/api/v1/...` should be translated into the existing `/api/v3/...` controller style unless the project deliberately introduces a new API version.

## Target User Workflows

Libraries and scanning:

- Add a movie library such as `/Media/Movies`.
- Add a TV library such as `/Media/TV Shows`.
- Scan folders for supported video extensions: `.mkv`, `.mp4`, `.avi`, `.mov`, `.m4v`, `.ts`, `.m2ts`, and `.wmv`.
- Ignore junk and partial files such as `.tmp`, `.partial`, `.!qB`, images, text files, and subtitles as primary media while still discovering subtitle sidecars.
- Detect missing files and renamed files without deleting anything automatically.

Parsing and matching:

- Prefer explicit provider IDs in folders or filenames, for example `{tmdb-603}`, `{tvdb-81189}`, or IMDb IDs.
- Parse movie title/year from folder and filename.
- Parse TV `SxxExx`, multi-episode ranges, and date-based episodes.
- Persist first-pass parser guesses for scanned files in unmatched-review state so UI decisions can survive future scans.
- Generate file-level TMDB/TVDB match candidates from explicit IDs and cached provider metadata before user review.
- Allow opt-in online TMDB lookup for file candidates and explicit-ID refresh for TMDB/TVDB when cached metadata is missing.
- Score matches with explainable reasons: external ID, title, year, runtime, folder structure, episode number, and air date.
- Auto-match only high-confidence results. Send uncertain files to review and keep manual decisions stable across future scans.

Metadata and artwork:

- Movies default to TMDB for metadata, artwork, cast, crew, collections, and IMDb external IDs.
- TV defaults to TVDB for show structure, seasons, episodes, episode order, specials, and missing-episode calculations.
- TMDB remains the fallback for TV artwork and richer people/artwork data where useful.
- Cache provider responses so UI reads do not require live API calls and the app remains usable during network outages.
- Respect metadata and artwork locks.

Library health:

- Duplicate detection must report review data first and never auto-delete by default.
- Missing episode detection should compare local files to TVDB/TMDB episode data and classify missing, unaired, ignored, special, downloaded, duplicate, and unmatched states.
- Export NFO, local artwork, JSON, and CSV later so other apps can use DaCollector data even before plugins are complete.

## Current Conversion State

DaCollector is no longer just a rename of the inherited server. The repository now has:

- DaCollector project branding, docs, Docker files, and Windows install guidance.
- Default Web UI/API port `38111`.
- Hosted Web UI pages for managed collections and exact duplicate review.
- Managed collection APIs, collection-builder preview support, and Plex target APIs.
- Filename parser and unmatched-file review APIs for parsed guesses, ignore/unignore, and manual provider matches.
- File-level TMDB/TVDB match candidate APIs for scanning, reviewing, approving, and rejecting unmatched file suggestions.
- Opt-in online TMDB lookup and explicit TMDB/TVDB ID refresh for unmatched file candidate scans.
- Generic media read APIs under `/api/v3/Media` for movies, shows, seasons, episodes, and local files.
- TMDB and TVDB settings surfaces.
- Cached TVDB show/movie/season/episode models, repositories, jobs, and metadata service work in progress.
- Internal domain rename work from `AnimeSeries`/`AnimeGroup`/`AnimeEpisode` toward `MediaSeries`/`MediaGroup`/`MediaEpisode`.

The conversion is still active. Treat migration correctness and compile stability as higher priority than adding new product features.

## Base System Decision

Use the inherited server as DaCollector's main codebase.

Keep:

- ASP.NET Core API and authentication.
- Settings provider and JSON-backed configuration model.
- Managed folder scanning and file location model.
- Video hashing, MediaInfo extraction, and file-place tracking.
- NHibernate repositories and migration path for the first implementation pass.
- Quartz jobs and SignalR event infrastructure.
- Existing TMDB movie/show metadata cache as the initial provider cache.
- Existing duplicate-file release management as the first duplicate workflow reference.

Replace or generalize:

- Remaining Anime/AniDB-centric domain names in new code paths.
- AniDB-only release identification as the primary matching model.
- Anime group/series/episode assumptions in collection and duplicate APIs.
- Anime-specific filters where collection rules should operate on movies and TV shows.

Use upstream collection-builder behavior as a reference, not as an embedded Python runtime.

Port concepts:

- Collection files and rule-driven builders.
- TMDB and TVDB source vocabulary for the first public version.
- Builder validation, missing media reports, sync modes, and item filters.
- Collection metadata updates such as posters, summaries, labels, sort titles, and visibility flags.

Do not port directly:

- Plex-specific object model as the core domain.
- Python cache implementation.
- HTML scraping as a first-choice provider strategy when official APIs or datasets are available.

Use upstream Plex relay behavior as the Plex adapter reference, not as the server runtime.

Keep from Relay:

- Plex scanner flow that resolves files through the inherited v3 API.
- Plex metadata agent flow that maps inherited series, episodes, TMDB images, cast, crew, ratings, genres, and collections into Plex objects.
- Automation concepts from `collection-posters.py`, `force-metadata.py`, `watched-sync.py`, and `rescan-recent.py`.
- The important operational constraint that Plex metadata should be refreshable and recoverable from the server as source of truth.

Replace or modernize from Relay:

- Legacy Plex agent/scanner APIs, which Relay's README identifies as maintenance-mode.
- AniDB-specific naming, genre, season, and episode assumptions.
- Python 2 Plex plugin code inside `Contents/Code` and `Contents/Scanners`.
- Direct script-level URL construction spread across files.

Target Relay replacement:

- A maintained Plex adapter package that consumes the new generic media APIs.
- Shared DaCollector API client logic for scanner, metadata, watched sync, poster sync, and refresh jobs.
- Movie and TV libraries as first-class targets, instead of anime-only "TV Shows" assumptions.
- Optional compatibility mode for existing Plex relay users during migration.

## Target Domain Model

The active transition model uses `MediaSeries`, `MediaGroup`, and `MediaEpisode` for the local DaCollector domain while inherited AniDB provider models remain under `Models/AniDB`.

Current local model names:

- `MediaSeries`: local title/series wrapper with inherited AniDB identity where present plus direct TMDB/TVDB IDs for movie and TV metadata.
- `MediaGroup`: local grouping container for series and collection-style hierarchy.
- `MediaEpisode`: local episode wrapper used by inherited release matching and watch-state flows.
- `VideoLocal`: unique local file identity by hash and file size.
- `VideoLocal_Place`: one physical location for a local file.

Longer-term generic media concepts should be introduced without breaking the existing migration path:

- `MediaTitle`: base identity for a movie or show.
- `Movie`: one playable title.
- `Show`: parent title for seasons and episodes.
- `Season`: season grouping for a show.
- `Episode`: one TV episode.
- `MediaFile`: local video file and technical metadata.
- `MediaFileLocation`: one physical path for a file.
- `ExternalMediaId`: provider IDs for TMDB, TVDB, Plex, Jellyfin, and future providers.
- `CollectionDefinition`: user or automation-managed collection.
- `CollectionRule`: one builder/filter rule inside a collection definition.
- `CollectionItem`: title membership with source and sync state.
- `DuplicateSet`: files that represent the same media identity or exact same binary content.

## Provider Strategy

TMDB:

- Reuse `DaCollector.Server/Providers/TMDB`.
- Reuse existing `TMDB_Movie`, `TMDB_Show`, `TMDB_Season`, `TMDB_Episode`, image, person, company, network, and collection models.
- Promote TMDB movie/show IDs to first-class media identity for the first version.

TVDB:

- Support TVDB IDs as external IDs on movies and shows.
- Use the cached `TVDB_Show`, `TVDB_Movie`, `TVDB_Season`, and `TVDB_Episode` tables for provider data.
- Use TVDB API credentials from `TVDB_API_KEY` and `TVDB_PIN` when calling TVDB APIs.
- Keep collection/list builders modeled after the upstream `modules/tvdb.py`.
- Reserve web parsing as an optional fallback only after API and structured-data paths are exhausted.

## Collection Engine

Port collection-builder behavior into a .NET service layer:

- `ICollectionBuilder`: validates one builder and returns external IDs.
- `ICollectionRuleEvaluator`: applies filters, limits, sort rules, and media type checks.
- `ICollectionSyncService`: creates, updates, appends, or syncs collection membership.
- `ICollectionMetadataService`: updates collection title, summary, poster, background, labels, and sort behavior.

Initial builders:

- `tmdb_movie`, `tmdb_show`, `tmdb_collection`, `tmdb_popular`, `tmdb_top_rated`, `tmdb_trending_daily`, `tmdb_trending_weekly`, `tmdb_discover`
- `tvdb_movie`, `tvdb_show`, `tvdb_list`

Initial sync modes:

- `append`: add matching items and leave other collection members alone.
- `sync`: add matching items and remove managed items that no longer match.
- `preview`: compute changes without saving.

## Plex Relay Adapter

The Plex-facing layer should be built around Relay's proven data flow:

1. Authenticate to DaCollector and cache an API key.
2. Resolve Plex file paths through `File/PathEndsWith` or the new generic media-file endpoint.
3. Fetch media metadata from DaCollector by media ID.
4. Populate Plex title, sort title, original title, summary, rating, content rating, studio, genres, collections, posters, backdrops, roles, seasons, and episodes.
5. Run maintenance jobs for watched-state sync, collection posters, metadata refresh, and recent rescans.

Required server support for the adapter:

- Path lookup endpoint that works for movies and episodes.
- Generic media DTOs with TMDB and TVDB IDs.
- Collection DTOs with poster/backdrop image URLs.
- Duplicate-safe file identity data so Plex file matching does not choose the wrong item.
- Watched/rating endpoints for movies and episodes.

Required adapter support:

- Plex server/library credentials.
- DaCollector credentials.
- Movie library mode.
- TV show library mode.
- Mixed movie/show library mode if Plex supports the target metadata provider path.
- Dry-run modes for poster sync, watched sync, and metadata refresh.

## Duplicate Strategy

Phase 1 exact duplicates:

- Use file hash and file size.
- Treat multiple `MediaFileLocation` rows for the same hash/size as exact duplicate locations.
- Reuse the inherited duplicate-file controller behavior as a reference for preview and auto-remove candidates.

Phase 2 release duplicates:

- Group files by resolved media identity.
- Rank candidates by resolution, codec, HDR/DV, audio channels, preferred language, subtitle coverage, file size, and user pin/keep markers.
- Never delete automatically without an explicit delete job.

Phase 3 fuzzy duplicates:

- Identify likely duplicates where hash differs but media identity, runtime, season/episode, and filename confidence match.
- Return confidence and reason codes for review.

## API Shape

Proposed v3 endpoints:

- `GET /api/v3/Media/Movie`
- `GET /api/v3/Media/Movie/{id}`
- `GET /api/v3/Media/Show`
- `GET /api/v3/Media/Show/{id}`
- `GET /api/v3/Media/Show/{id}/Seasons`
- `GET /api/v3/Media/Show/{id}/Episodes`
- `GET /api/v3/Collections`
- `POST /api/v3/Collections`
- `POST /api/v3/Collections/{id}/Preview`
- `POST /api/v3/Collections/{id}/Sync`
- `GET /api/v3/Duplicates/Exact`
- `GET /api/v3/Duplicates/Media`
- `POST /api/v3/Duplicates/{id}/PreviewRemove`
- `POST /api/v3/Duplicates/{id}/Remove`

## Implementation Phases

1. Fork hygiene and naming
   - Choose project/product name.
   - Preserve MIT license notices.
   - Update package metadata only after the domain foundation exists.

2. Generic media identity foundation
   - Add media type enums and external ID value objects.
   - Add persistence models for media title, file identity links, collections, and duplicate sets.
   - Add migrations without modifying existing migrations.

3. Provider layer
   - Wrap existing TMDB services behind generic media provider interfaces.
   - Add TVDB settings.
   - Add provider result DTOs and cache tables.

4. Scanner and matcher
   - Keep managed folder scanning.
   - Add movie/TV filename parsing and provider search matching.
   - Store match confidence and manual overrides.

5. Collection engine
   - Implement rule parser and first TMDB builders.
   - Add collection preview and sync APIs.
   - Add TVDB builders after TMDB path is stable.

6. Duplicate manager
   - Add exact duplicate APIs on generic media files.
   - Add media duplicate grouping and quality ranking.
   - Add dry-run delete jobs before any destructive action.

7. UI and integrations
   - Add Web UI screens for library, collections, matching queue, and duplicates.
   - Add the Plex adapter based on the relay data flow after backend behavior is testable.
   - Add Jellyfin collection export/import adapters after Plex support is stable.

## Immediate Next Slice

The safest next code slice is stabilizing the current conversion:

- Make the current branch compile after the `Anime*` to `Media*` rename.
- Repair database migrations so historical commands are not rewritten and new renames are append-only.
- Add SQLite migration smoke tests for fresh and upgraded databases.
- Add tests around TVDB login caching, retry behavior, missing credentials, show refresh, and movie refresh.
- Update API documentation after the renamed metadata controller and TVDB endpoints are stable.

The next adapter slice after compile and migration stability is a Relay compatibility API review:

- List every inherited v3 endpoint used by the Plex relay reference.
- Mark which endpoints already support movies and TV shows.
- Define replacements for anime-only endpoints before implementing a new Plex adapter.
