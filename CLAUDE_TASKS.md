# DaCollector Claude Task List

Context:
- Branch: `daccollector-main`
- Latest commits: `60b429d Mark P2 product improvements complete`
- Follow `CLAUDE.md`: append database migrations; **never rewrite historical migration strings**.
- Keep legacy API contract class names like `CL_AnimeSeries_User` unless intentionally versioning the public API.
- `.NET SDK 10.0.203` may not be in PATH in the sandbox — use `& "C:\Program Files\dotnet\dotnet.exe"` if needed.

---

## ✅ Completed

- Add TVDB metadata provider (`TVDB_Show`, `TVDB_Season`, `TVDB_Episode`, `TVDB_Movie` models, repo, service)
- Fix 6 pre-existing bugs (see commit `0a74238`)
- Rename domain entities: `AnimeSeries`→`MediaSeries`, `AnimeEpisode`→`MediaEpisode`, `AnimeGroup`→`MediaGroup` (+ `_User` variants)
- Rename physical `.cs` files to match new class names (23 files)
- Rename DB column properties: `AnimeGroupParentID`→`MediaGroupParentID`, `DefaultAnimeSeriesID`→`DefaultMediaSeriesID`, `TopLevelAnimeGroup`→`TopLevelMediaGroup`
- Rename v3 API controller: `AniDBController`→`MetadataController` (route: `/api/v3/Metadata/`)
- Rename v3 API model classes: `AnidbAnime`→`MetadataAnime`, `AnidbEpisode`→`MetadataEpisode`, etc.
- Rename public JSON properties: `Series.AniDB`→`Series.Source`, `Episode.AniDB`→`Episode.Source`, `IDs.AniDB`→`IDs.SourceID`
- **P0**: Repair historical DB migrations — restored all three DB files to pre-rename state; added v145/163/158 rename blocks (20 entries each)
- **P0**: Fix `DatabaseFixes.cs` stale type/property references (`AnimeSeries_User`→`MediaSeries_User`, `AnimeEpisodeID`→`MediaEpisodeID`, service renames)
- **P1**: Rename `AnimeType` enum → `MediaType` in both Abstractions and Server API v3; rename physical files; preserve v1 JSON contract with `[JsonProperty("AnimeType")]`; NHibernate column mapping override so DB column stays as `AnimeType`
- **P1**: Rename `MediaSeries.TVDB_ShowID/TVDB_MovieID` → `TvdbShowExternalID/TvdbMovieExternalID`; NHibernate Column() override keeps DB columns unchanged
- **P1**: Rename `AnidbEventEmitter` → `AniDBConnectionEventEmitter`; update CLAUDE.md
- **P1**: Add `TvdbController` at `/api/v3/Tvdb/` — show/movie GET, Refresh, Link/Unlink endpoints

---

## ✅ P1 — Update CLAUDE.md Architecture Documentation — DONE

Cache index example, TVDB property names, and StoredReleaseInfo description updated.
All domain model references already use Media* names throughout.

---

## ⏸ P1 — Clean Up AniDB Settings Exposure — DEFERRED

`AniDbSettings` follows the same naming convention as `TMDBSettings`, `TVDBSettings`, `TraktSettings`, `IMDbSettings`.
Renaming to `SourceSettings` would be inconsistent and break existing `settings-server.json` configs.
The class name is internal (not user-facing). The JSON key `"AniDb"` in the settings file would need a migration.
**Decision: do not rename** — it accurately identifies the AniDB provider settings.

---

## ✅ P1 — Stabilize TVDB Provider — DONE

- `MediaSeries.TvdbShowExternalID` / `TvdbMovieExternalID` store external TVDB IDs (DB columns still `TVDB_ShowID`/`TVDB_MovieID`)
- `TvdbController` added at `/api/v3/Tvdb/` with show/movie GET, Refresh, Link/Unlink

---

## ✅ P1 — Rename AniDB Connection Event Emitter — DONE

`AnidbEventEmitter` → `AniDBConnectionEventEmitter` (file + class + registration).
Note: `MetadataEventEmitter` already existed as a separate emitter for metadata domain events.

---

## ✅ P2 — Product Improvements — DONE

- Install verification documented for Windows and Docker at `docs/getting-started/verify-install.md`:
  - Port `38111`
  - `GET /api/v3/Init/Status`
  - `/webui`
  - Clean SQLite startup and migration-log checks
- Collection management tests expanded:
  - TMDB movie/show/collection rules
  - TVDB movie/show/list rules
  - Duplicate external ID collapse
  - Duplicate title preservation across different external IDs
  - Plex preview and append apply mode
- Duplicate management improved:
  - Exact file duplicate cleanup remains under `/api/v3/Duplicates/Exact`
  - Plex media duplicate review added under `/api/v3/Duplicates/Media/Plex/Library/{sectionKey}`
  - Scoring reasons include path hash, provider ID, title/year, and Plex rating keys
  - Safe-delete candidates are surfaced as review data without auto-deleting

---

## Verification Commands

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build DaCollector.sln --no-restore
& "C:\Program Files\dotnet\dotnet.exe" test DaCollector.Tests/DaCollector.Tests.csproj --no-restore
& "C:\Program Files\dotnet\dotnet.exe" test DaCollector.IntegrationTests/DaCollector.IntegrationTests.csproj --no-restore
```

If restore is required first:
```powershell
& "C:\Program Files\dotnet\dotnet.exe" restore DaCollector.sln
```
