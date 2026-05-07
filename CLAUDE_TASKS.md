# DaCollector Claude Task List

Context:
- Branch: `daccollector-main`
- Latest commits: `c287df2 Rename AniDB v3 API models, controller, and public JSON properties`
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

---

## P0 — Repair Historical Database Migrations (BLOCKER)

The mass rename passes changed strings inside historical `DatabaseCommand` entries in `SQLite.cs`, `MySQL.cs`, and `SQLServer.cs`. Historical migration strings must match the schema that existed when they were originally written. Changing them breaks **fresh installs** because:

- v1.52 now creates `MediaGroup` with column `MediaGroupParentID`
- v145.9 then tries to `RENAME COLUMN AnimeGroupParentID TO MediaGroupParentID` — but that column was never created with that name → **SQL error on fresh install**

**Required fix in `SQLite.cs`:**

1. Revert ALL historical migration strings (v1 through v143) that reference `Anime*` table/column names back to their original values. The rename pass changed:
   - `AnimeEpisode` → `MediaEpisode` (v1.49–51, v1.102–104, v140.x)
   - `AnimeSeries` → `MediaSeries` (v1.53–54, v13.1, v41.1, v44.x, v140.x)
   - `AnimeGroup` → `MediaGroup` (v1.52, v4.1, v44.x, v140.x, v143.x)
   - `AnimeGroupParentID` → `MediaGroupParentID` (v1.52)
   - `DefaultAnimeSeriesID` → `DefaultMediaSeriesID` (v4.1)
   - Column/index names in CREATE INDEX statements

2. The new append-only rename migrations already added (v145.1–10 for SQLite, v163.1–10 for MySQL, v158.1–10 for SQLServer) are correct — keep them.

3. Also verify that v144 (TVDB columns) targets `AnimeSeries` (the name before the v145 rename), OR is ordered after v145. Currently v144 targets `MediaSeries` which does NOT exist on old databases before v145 runs.

**Same fix applies to `MySQL.cs` and `SQLServer.cs`.**

Use `git show 0a74238:DaCollector.Server/Databases/SQLite.cs` to recover the original strings.

---

## P0 — Fix DatabaseFixes.cs Stale References (COMPILE BLOCKER)

File: `DaCollector.Server/Databases/DatabaseFixes.cs`

Search and replace:
- `RepoFactory.AnimeSeries_User` → `RepoFactory.MediaSeries_User`
- `RepoFactory.AnimeEpisode_User` → `RepoFactory.MediaEpisode_User`
- `RepoFactory.AnimeGroup_User` → `RepoFactory.MediaGroup_User`
- `AnimeGroupCreator` → `MediaGroupCreator`
- `AnimeSeriesService` → `MediaSeriesService`
- `AnimeGroupService` → `MediaGroupService`
- Any remaining `AnimeSeries`, `AnimeEpisode`, `AnimeGroup` type references (not SQL strings)

After fixing, run:
```powershell
rg -n "RepoFactory\.(AnimeSeries_User|AnimeGroup_User|AnimeEpisode_User|AnimeSeries|AnimeGroup|AnimeEpisode)\b|\bAnimeGroupCreator\b|\bAnimeSeriesService\b|\bAnimeGroupService\b" DaCollector.Server
```
Expected: zero matches (or only inside SQL string literals).

---

## P1 — Rename `AnimeType` Enum to `MediaType`

File: `DaCollector.Server/API/v3/Models/AniDB/AnimeType.cs` (and all references)

- Rename enum `AnimeType` → `MediaType`
- Rename physical file `AnimeType.cs` → `MediaType.cs`
- Update all references across the codebase (using statement aliases like `using AnimeType = ...` and direct usages)
- Add DB migration if `AnimeType` is persisted as a string/int column anywhere

---

## P1 — Clean Up AniDB Settings Exposure

File: `DaCollector.Server/Settings/AniDbSettings.cs` and `ServerSettings.cs`

- Rename `AniDbSettings` class → `SourceSettings` (or `MetadataSourceSettings`)
- Update the settings JSON key if it is serialized (check `[JsonProperty]` or `[StorageLocation]` attributes)
- Rename the property in `ServerSettings` that holds `AniDbSettings`
- Update all references in services, controllers, and the web UI that expose AniDB connection settings to end users

---

## P1 — Update CLAUDE.md Architecture Documentation

File: `CLAUDE.md`

- Update the "Domain Model Relationships" section: replace `AnimeSeries`, `AnimeGroup`, `AnimeEpisode` with `MediaSeries`, `MediaGroup`, `MediaEpisode` throughout
- Update the "Import Pipeline" job chain diagram references
- Keep references to `AniDB_Anime`, `AniDB_Episode` etc. — those are the external metadata cache models, not the domain
- Note TVDB as a new metadata provider alongside TMDB
- Update the API section to reflect `MetadataController` at `/api/v3/Metadata/`

---

## P1 — Stabilize TVDB Provider

- Decide whether `MediaSeries.TVDB_ShowID` / `TVDB_MovieID` store external TVDB IDs or internal DB row IDs
  - Current code uses them as external IDs via `RepoFactory.TVDB_Show.GetByTvdbShowID`
  - Consider renaming to `TvdbShowExternalID` / `TvdbMovieExternalID` to avoid confusion with `TVDB_Show.TVDB_ShowID` (internal PK)
- Add v3 API endpoints in `TvdbController` (if not already present):
  - Refresh TVDB show/movie by external ID
  - Link/unlink TVDB show/movie to a `MediaSeries`
  - Return cached TVDB show/movie/season/episode data

---

## P1 — Rename SignalR AniDB Event Emitter

File: `DaCollector.Server/API/SignalR/Aggregate/AnidbEventEmitter.cs`

- Rename class `AnidbEventEmitter` → `MetadataEventEmitter`
- Rename file `AnidbEventEmitter.cs` → `MetadataEventEmitter.cs`
- Update registration and references in `AggregateHub` and `APIExtensions.cs`

---

## P2 — Product Improvements

- Add install verification path (Windows + Docker):
  - Start server on port `38111`
  - `GET /api/v3/Init/Status` returns 200
  - `/webui` loads
  - No migration errors on clean SQLite database

- Expand collection management tests:
  - TMDB movie/show/collection rules
  - TVDB movie/show/list rules
  - Duplicate title handling
  - Plex preview and apply mode

- Improve duplicate management:
  - Separate exact file duplicates from duplicate media entries
  - Add scoring reasons (path hash, provider ID, title/year, Plex rating key)
  - Surface safe-delete candidates without auto-deleting

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
