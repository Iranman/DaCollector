# The Collector Blueprint

The Collector uses `ShokoAnime/ShokoServer` as the server base, `natyusha/ShokoRelay.bundle` as the Plex-facing scanner/agent/automation reference, and `Kometa-Team/Kometa` as the collection/provider behavior reference.

Source snapshots:

- ShokoServer: `56707a9` on `master`
- ShokoRelay.bundle: `c06ec96` on `master`
- Kometa: `15b1e1f` on `master`

Upstream ShokoServer and upstream Kometa are MIT licensed. Upstream ShokoRelay.bundle does not include a license file in this checkout, so treat it as an integration reference until licensing or permission is confirmed. Do not directly copy Relay code into the server without resolving that.

## Product Target

Build The Collector as a self-hosted movie and TV show collection manager with:

- Local library scanning across managed folders.
- First-class movies, shows, seasons, episodes, files, collections, and duplicate sets.
- Metadata and cross-provider identity from TMDB, IMDb, and TVDB.
- Collection automation inspired by upstream provider builders.
- Exact and near-duplicate detection with safe review before deletion.
- API-first design so a Web UI, Plex/Jellyfin integrations, and automation jobs can all use the same backend.

## Base System Decision

Use the inherited server as The Collector's main codebase.

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

- Anime/AniDB-centric domain names in new code paths.
- AniDB-only release identification as the primary matching model.
- Anime group/series/episode assumptions in collection and duplicate APIs.
- Anime-specific filters where collection rules should operate on movies and TV shows.

Use upstream collection-builder behavior as a reference, not as an embedded Python runtime.

Port concepts:

- Collection files and rule-driven builders.
- TMDB, IMDb, TVDB, Trakt, Radarr, and Sonarr source vocabulary.
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
- Shared The Collector API client logic for scanner, metadata, watched sync, poster sync, and refresh jobs.
- Movie and TV libraries as first-class targets, instead of anime-only "TV Shows" assumptions.
- Optional compatibility mode for existing Plex relay users during migration.

## Target Domain Model

New generic media concepts should sit beside the inherited anime models until migration is safe.

- `MediaTitle`: base identity for a movie or show.
- `Movie`: one playable title.
- `Show`: parent title for seasons and episodes.
- `Season`: season grouping for a show.
- `Episode`: one TV episode.
- `MediaFile`: local video file and technical metadata.
- `MediaFileLocation`: one physical path for a file.
- `ExternalMediaId`: provider IDs for TMDB, IMDb, TVDB, Trakt, Plex, Jellyfin, and future providers.
- `CollectionDefinition`: user or automation-managed collection.
- `CollectionRule`: one builder/filter rule inside a collection definition.
- `CollectionItem`: title membership with source and sync state.
- `DuplicateSet`: files that represent the same media identity or exact same binary content.

## Provider Strategy

TMDB:

- Reuse `Shoko.Server/Providers/TMDB`.
- Reuse existing `TMDB_Movie`, `TMDB_Show`, `TMDB_Season`, `TMDB_Episode`, image, person, company, network, and collection models.
- Promote TMDB movie/show IDs to first-class media identity for the first version.

IMDb:

- Support IMDb IDs as external IDs on every media title.
- Start with list/chart/search builders modeled after the upstream `modules/imdb.py`.
- Prefer IMDb datasets or official/public structured endpoints over brittle page scraping.

TVDB:

- Support TVDB IDs as external IDs on movies and shows.
- Start with lookup and collection/list builders modeled after the upstream `modules/tvdb.py`.
- Prefer the TVDB API when credentials are available; reserve web parsing as an optional fallback.

## Collection Engine

Port collection-builder behavior into a .NET service layer:

- `ICollectionBuilder`: validates one builder and returns external IDs.
- `ICollectionRuleEvaluator`: applies filters, limits, sort rules, and media type checks.
- `ICollectionSyncService`: creates, updates, appends, or syncs collection membership.
- `ICollectionMetadataService`: updates collection title, summary, poster, background, labels, and sort behavior.

Initial builders:

- `tmdb_movie`, `tmdb_show`, `tmdb_collection`, `tmdb_popular`, `tmdb_top_rated`, `tmdb_trending_daily`, `tmdb_trending_weekly`, `tmdb_discover`
- `imdb_id`, `imdb_list`, `imdb_chart`, `imdb_search`
- `tvdb_movie`, `tvdb_show`, `tvdb_list`

Initial sync modes:

- `append`: add matching items and leave other collection members alone.
- `sync`: add matching items and remove managed items that no longer match.
- `preview`: compute changes without saving.

## Plex Relay Adapter

The Plex-facing layer should be built around Relay's proven data flow:

1. Authenticate to The Collector and cache an API key.
2. Resolve Plex file paths through `File/PathEndsWith` or the new generic media-file endpoint.
3. Fetch media metadata from The Collector by media ID.
4. Populate Plex title, sort title, original title, summary, rating, content rating, studio, genres, collections, posters, backdrops, roles, seasons, and episodes.
5. Run maintenance jobs for watched-state sync, collection posters, metadata refresh, and recent rescans.

Required server support for the adapter:

- Path lookup endpoint that works for movies and episodes.
- Generic media DTOs with TMDB, IMDb, and TVDB IDs.
- Collection DTOs with poster/backdrop image URLs.
- Duplicate-safe file identity data so Plex file matching does not choose the wrong item.
- Watched/rating endpoints for movies and episodes.

Required adapter support:

- Plex server/library credentials.
- The Collector credentials.
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
   - Add IMDb and TVDB settings.
   - Add provider result DTOs and cache tables.

4. Scanner and matcher
   - Keep managed folder scanning.
   - Add movie/TV filename parsing and provider search matching.
   - Store match confidence and manual overrides.

5. Collection engine
   - Implement rule parser and first TMDB builders.
   - Add collection preview and sync APIs.
   - Add IMDb/TVDB builders after TMDB path is stable.

6. Duplicate manager
   - Add exact duplicate APIs on generic media files.
   - Add media duplicate grouping and quality ranking.
   - Add dry-run delete jobs before any destructive action.

7. UI and integrations
   - Add Web UI screens for library, collections, matching queue, and duplicates.
   - Add the Plex adapter based on the relay data flow after backend behavior is testable.
   - Add Jellyfin collection export/import adapters after Plex support is stable.

## Immediate Next Slice

The safest first code slice is the generic media identity foundation:

- Add `MediaKind`, `ExternalProvider`, and `ExternalMediaId` abstractions.
- Add settings stubs for IMDb and TVDB credentials.
- Add collection rule DTOs without wiring destructive actions.
- Add tests for ID parsing and rule validation.

That slice is small enough to review and does not disturb inherited anime behavior.

The next adapter slice after that is a Relay compatibility API review:

- List every inherited v3 endpoint used by the Plex relay reference.
- Mark which endpoints already support movies and TV shows.
- Define replacements for anime-only endpoints before implementing a new Plex adapter.
