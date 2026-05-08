# DaCollector Claude Task List

Context:
- Branch: `daccollector-main`
- Latest commits: `becf507 Fix local Docker build startup version fallback`
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

## P3 — Recommended Next Improvements

Goal: turn the current prototype surfaces into a reliable first install that a Windows or Docker user can run, connect to Plex, and use for movie/TV collection and duplicate review without hand-editing internals.

### ✅ P3.1 — Restore Local Verification — DONE

Files:
- `global.json`
- `DaCollector.Tests/DaCollector.Tests.csproj`
- `.github/workflows/*` if workflows exist or are added
- `docs/development/building.md`
- `docs/getting-started/verify-install.md`

Tasks:
- Confirm whether `.NET SDK 10.0.203` is actually required, or update `global.json` to the nearest installed/supported SDK.
- Add a short `docs/development/building.md` note for installing the required SDK on Windows.
- Add or update CI so `dotnet restore`, `dotnet build`, and `dotnet test` run on every push.
- Keep the existing verification commands in this file current.

Acceptance criteria:
- `& "C:\Program Files\dotnet\dotnet.exe" --list-sdks` shows a compatible SDK on the dev machine.
- `dotnet build DaCollector.sln --no-restore` succeeds.
- `dotnet test DaCollector.Tests/DaCollector.Tests.csproj --no-restore` succeeds.
- If integration tests are not ready, document why and split them into a separate CI job.

### ✅ P3.2 — Add Install Smoke Test Script — DONE

Files:
- `scripts/verify-install.ps1` (new)
- `docs/getting-started/verify-install.md`
- `docs/getting-started/installation/windows.md`
- `docs/getting-started/installation/docker.md`

Tasks:
- Add a PowerShell smoke test script that checks:
  - `http://127.0.0.1:38111/api/v3/Init/Status`
  - `http://127.0.0.1:38111/webui`
  - optional Docker container health/log scan when `-Docker` is passed.
- Make the port configurable, defaulting to `38111`.
- Return a non-zero exit code on failed checks.
- Reference the script from the Windows and Docker install docs.

Acceptance criteria:
- Running `.\scripts\verify-install.ps1` prints each check and exits `0` when DaCollector is reachable.
- Running with the wrong port exits non-zero and explains the failed endpoint.
- The docs still include manual commands for users who do not want to run scripts.

### ✅ P3.3 — Surface Plex Media Duplicate Review in the Web UI — DONE

Files:
- `DaCollector.Server/webui/dacollector-duplicates.html`
- `DaCollector.Server/API/v3/Controllers/DuplicatesController.cs`
- `docs/features/duplicate-management.md`

Tasks:
- Add a clear mode switch for:
  - Exact file duplicates
  - Plex media duplicates
- For Plex media duplicates, let the user enter or select a Plex library section key.
- Render score, match type, scoring reasons, Plex rating keys, provider IDs, title/year, and file paths.
- Do not add delete buttons for media duplicates; show review-only language.
- Keep exact file duplicate delete behavior unchanged.

Acceptance criteria:
- Exact duplicate mode still calls `/api/v3/Duplicates/Exact/*`.
- Media duplicate mode calls `/api/v3/Duplicates/Media/Plex/Library/{sectionKey}`.
- Safe-delete candidates are visually distinct but still review-only.
- The page works with API keys stored in the same browser storage flow used today.

### ✅ P3.4 — Improve Plex Collection Sync Feedback — DONE

Files:
- `DaCollector.Server/Collections/ManagedCollectionSyncService.cs`
- `DaCollector.Server/Plex/PlexTargetService.cs`
- `DaCollector.Abstractions/Collections/CollectionSyncResult.cs`
- `DaCollector.Abstractions/MediaServers/Plex/PlexCollectionApplyResult.cs`
- `DaCollector.Tests/PlexTargetServiceTests.cs`

Tasks:
- Include an explicit planned diff in preview/apply responses:
  - matched items
  - missing items
  - add candidates
  - remove candidates
  - unchanged items
- Make `apply=false` return the same diff shape without writing to Plex.
- Add warnings when Plex library item GUIDs are missing provider IDs.
- Add tests for sync remove mode and missing-provider-ID handling.

Acceptance criteria:
- Users can see exactly what will be added or removed before applying.
- Append mode never removes existing Plex collection members.
- Sync mode removes only members that are absent from the evaluated target set.
- Tests cover preview, append, sync, missing IDs, and failed Plex responses.

### ✅ P3.5 — Build a Provider Match Queue for Movie/TV Imports — DONE

Files:
- `DaCollector.Server/Models/DaCollector/MediaSeries.cs`
- `DaCollector.Server/Providers/TMDB/*`
- `DaCollector.Server/Providers/TVDB/*`
- `DaCollector.Server/Collections/*`
- New service under `DaCollector.Server/Metadata` or `DaCollector.Server/Providers`
- New API controller or endpoints under `/api/v3/Metadata`

Tasks:
- Create a review queue for unmatched or low-confidence movie/TV titles.
- Store candidate matches from TMDB, TVDB, and IMDb with confidence reasons:
  - title similarity
  - year
  - provider IDs
  - Plex GUIDs
  - folder path hints
- Add approve/reject endpoints.
- Keep automatic linking conservative until a user approves uncertain matches.

Acceptance criteria:
- A scanned title can have multiple provider candidates without immediately overwriting links.
- Approved candidates update `MediaSeries` external IDs.
- Rejected candidates do not reappear unless provider data changes or the user refreshes manually.
- Candidate reasons are visible through API responses.

### ✅ P3.6 — Harden TVDB and IMDb Provider Behavior — DONE

Files:
- `DaCollector.Server/Collections/TvdbCollectionBuilderClient.cs`
- `DaCollector.Server/Providers/TVDB/TvdbMetadataService.cs`
- `DaCollector.Server/Collections/ImdbDatasetCollectionBuilderClient.cs`
- `DaCollector.Tests/CollectionBuilderPreviewServiceTests.cs`
- New provider-specific test files as needed

Tasks:
- Add tests for TVDB token caching, unauthorized retry, missing credentials, and empty API responses.
- Add tests for IMDb dataset missing files, malformed rows, cache expiry, and title type filtering.
- Make provider warnings user-actionable and avoid leaking API keys or tokens.

Acceptance criteria:
- TVDB retries once after a 401 with a refreshed token.
- Missing TVDB credentials produce a clear warning rather than a crash.
- IMDb dataset errors name the missing file or malformed row.
- No provider warning contains configured secrets.

### ✅ P3.7 — Release and Container Polish — DONE

Files:
- `docker-compose.yml`
- `docker-compose.example.yml`
- `compose.yaml`
- `compose.ghcr.yaml`
- Dockerfiles
- Installer files under `Installer/`
- `.github/workflows/*`
- `docs/getting-started/installation/docker.md`
- `docs/getting-started/installation/windows.md`

Tasks:
- Confirm every Docker/Compose file exposes host port `38111` by default.
- Add container healthcheck against `/api/v3/Init/Status`.
- Publish GHCR image from CI.
- Publish Windows installer and ZIP artifacts from CI.
- Document release artifact names exactly as produced by CI.

Acceptance criteria:
- `docker compose up -d` starts a container reachable on `http://127.0.0.1:38111/webui`.
- `docker compose ps` shows a healthy container when startup is complete.
- Release docs match actual artifact names.
- The installer, standalone ZIP, and Docker image all use the same default port.

### ✅ P3.8 — Documentation Quality Pass — DONE

Files:
- `docs/index.md`
- `docs/getting-started/*`
- `docs/features/*`
- `docs/reference/*`
- `mkdocs.yml`

Tasks:
- Add screenshots only after the Web UI is stable.
- Add a first-run walkthrough for:
  - create first admin user
  - configure Plex
  - configure TMDB/TVDB/IMDb
  - add managed folders
  - preview a collection
  - review duplicates
- Add a troubleshooting page for ports, Docker networking, Plex token, provider credentials, and clean SQLite startup.
- Keep `.env` out of the recommended Docker path unless the user explicitly asks for it.

Acceptance criteria:
- `mkdocs build --strict` succeeds when MkDocs dependencies are installed.
- Every install page links to verification.
- Every feature page links to the related API page or endpoint table.
- Docker docs keep direct environment values in `docker-compose.yml`, not a recommended `.env` file.

### ✅ P3.9 — Fix Docker First-Boot Ownership Stall on TrueNAS — DONE

Context:
- A TrueNAS install can appear stuck after:
  - `Started DaCollector bootstrapping process...`
  - `Adding user dacollector and changing ownership of /home/dacollector and all its sub-directories...`
- `dockerentry.sh` later runs `chown -R $PUID:$PGID /home/dacollector/` when `DACOLLECTOR_HOME` ownership does not match `PUID:PGID`.
- Recursive `chown` can be extremely slow on TrueNAS/ZFS volumes, and the current log message does not say which path is being changed or how to skip it.

Files:
- `dockerentry.sh`
- `docker-compose.yml`
- `docker-compose.example.yml`
- `compose.yaml`
- `compose.ghcr.yaml`
- `docs/getting-started/installation/docker.md`
- `docs/getting-started/verify-install.md`
- `scripts/verify-install.ps1`

Tasks:
- Make ownership repair more targeted:
  - Prefer `chown` only on `$DACOLLECTOR_HOME`, not all of `/home/dacollector/`.
  - Avoid recursive ownership changes when the top-level directory already matches `PUID:PGID`.
  - Consider a `DACOLLECTOR_CHOWN=false` or `SKIP_CHOWN=true` escape hatch for TrueNAS/ACL-managed datasets.
- Improve startup logging:
  - Print `PUID`, `PGID`, `DACOLLECTOR_HOME`, current owner, desired owner, and whether recursive ownership will run.
  - Print before and after any long-running ownership step.
- Update Compose examples:
  - Add comments telling TrueNAS users to set `PUID`/`PGID` to the dataset owner.
  - Document that mounted media paths should stay read-only and should not be under `/home/dacollector`.
- Update Docker docs with TrueNAS troubleshooting:
  - Run `docker exec dacollector id dacollector`.
  - Run `docker exec dacollector stat -c '%u:%g %n' /home/dacollector/.dacollector/DaCollector`.
  - If ownership is wrong, stop the container and fix the host dataset owner or set the correct `PUID`/`PGID`.

Acceptance criteria:
- First boot on an empty Docker volume does not run a broad recursive `chown` over unrelated paths.
- Startup logs clearly show when ownership repair is being skipped, started, and finished.
- A TrueNAS user can tell whether the container is really hung or just changing ownership.
- Docker install docs explain the slow-start symptom and the preferred fix.

### ✅ P3.10 — Fix Local Docker Build Version Crash — DONE

Context:
- TrueNAS local Docker build continued past ownership repair, then crashed before server startup:
  - `System.NullReferenceException`
  - `DaCollector.Server.Plugin.PluginManager..ctor(...)`
  - `PluginManager.cs:line 50`
- The local Compose build used `DACOLLECTOR_BUILD_VERSION:-0.0.0-local`, producing invalid `0.0.0.0` assembly metadata.
- `PluginManager.GetVersionInformation()` returned `null` for the core server assembly, then `systemService.Version.AbstractionVersion` was dereferenced during startup.

Files:
- `DaCollector.Server/Plugin/PluginManager.cs`
- `compose.yaml`
- `Dockerfile`
- `Dockerfile.aarch64`

Fix:
- Core server version detection now falls back to a valid local version when assembly metadata is incomplete.
- Local Docker build defaults now use `0.0.1-local` instead of `0.0.0-local`.
- Dockerfile build args now have non-empty local defaults.
- `Dockerfile.aarch64` healthcheck/expose port was corrected to `38111`.

Acceptance criteria:
- A local Docker build no longer crashes at `PluginManager.cs:line 50`.
- `docker compose up -d --build` reaches server startup after the entrypoint finishes.
- `/api/v3/Init/Status` becomes reachable on port `38111`, unless a later startup error appears.

### ✅ P3.11 — Claude Review: Double-Check Docker Startup Fix — DONE

Goal: independently review and verify the fix in commit `becf507` before treating Docker install as stable.

Context:
- TrueNAS log before the fix:
  - Entry point completed ownership setup.
  - DaCollector crashed with `System.NullReferenceException`.
  - Stack trace pointed at `DaCollector.Server.Plugin.PluginManager..ctor(...)` in `PluginManager.cs:line 50`.
- The suspected root cause was local Docker build metadata:
  - `compose.yaml` defaulted `DACOLLECTOR_BUILD_VERSION` to `0.0.0-local`.
  - That produced assembly version `0.0.0.0`.
  - `PluginManager.GetVersionInformation()` returned null for the core server assembly.
- Codex changed:
  - `DaCollector.Server/Plugin/PluginManager.cs`
  - `compose.yaml`
  - `Dockerfile`
  - `Dockerfile.aarch64`
  - `CLAUDE_TASKS.md`

Review tasks:
- Confirm the fallback in `PluginManager.ReadVersionInformationFromAssembly` is safe only for the core server assembly and does not accidentally load invalid plugin DLLs.
- Confirm `typeof(IPlugin).Assembly.GetName().Version` is the correct fallback source for `AbstractionVersion`.
- Confirm `0.0.1-local` is accepted by MSBuild `/p:Version` and produces a non-zero assembly version.
- Confirm Dockerfile build args remain compatible with CI workflows that pass explicit version/channel/commit/tag/date values.
- Confirm `Dockerfile.aarch64` changing `EXPOSE` and healthcheck from `8111` to `38111` matches the project-wide port decision.
- Run the verification commands if SDK is available.
- Rebuild locally on Docker and verify the TrueNAS crash no longer happens.

Suggested Docker verification:
```bash
docker compose down
docker compose build --no-cache dacollector
docker compose up -d
docker logs dacollector -f
curl -i http://127.0.0.1:38111/api/v3/Init/Status
curl -i http://127.0.0.1:38111/webui
```

Acceptance criteria:
- Claude either confirms the fix or proposes a narrower safer patch.
- No startup crash remains at `PluginManager.cs:line 50`.
- Server reaches Kestrel startup or reports a new, different startup issue.
- Any new issue is added as the next task with logs and file pointers.

---

## 🚧 P0 — Server Completion Readiness: Boot, Build, and First-Run Verification — IN PROGRESS

Goal: determine whether DaCollector is actually runnable as a first-install server and fix the blockers before moving to feature polish.

Current status:
- The codebase is not yet considered complete.
- A local .NET SDK `10.0.203` was bootstrapped to `F:\Collection manager\.dotnet-sdk`.
- `global.json` requires SDK `10.0.203` with `rollForward: latestFeature`.
- Docker is not installed in the local Windows shell, so Docker validation must run on a Docker host such as TrueNAS.
- The React `DaCollector-WebUI` build has been validated with `npm run build`.
- `compose.yaml` now targets `Dockerfile.combined`, which builds the adjacent `../DaCollector-WebUI` repo and copies its `dist/` output into `DaCollector.Server/webui`.

Files in scope:
- `global.json`
- `DaCollector.sln`
- `Dockerfile.combined`
- `compose.yaml`
- `docker-compose.yml`
- `docker-compose.example.yml`
- `.dockerignore`
- `docs/getting-started/installation/docker.md`
- `scripts/verify-install.ps1`
- Startup/logging code touched by any new crash.

### ✅ P0.1 — Restore Local .NET Build Verification — DONE

Tasks:
- Install or bootstrap SDK `10.0.203` without requiring global machine changes if possible.
- Run:
  ```powershell
  dotnet restore DaCollector.sln
  dotnet build DaCollector.sln -c Release --no-restore
  ```
- If tests are buildable, run:
  ```powershell
  dotnet test DaCollector.Tests/DaCollector.Tests.csproj -c Release --no-build
  dotnet test DaCollector.IntegrationTests/DaCollector.IntegrationTests.csproj -c Release --no-build
  ```
- Record any SDK/build/test failure with exact command output and file pointers.

Acceptance criteria:
- Release build succeeds locally, or the exact missing SDK/build blocker is documented.
- Test status is known, not assumed.

Result:
- Bootstrapped SDK:
  ```powershell
  F:\Collection manager\.dotnet-sdk\dotnet.exe --info
  ```
  SDK `10.0.203` is available locally.
- Restore passed:
  ```powershell
  & 'F:\Collection manager\.dotnet-sdk\dotnet.exe' restore DaCollector.sln
  ```
- Release build passed:
  ```powershell
  & 'F:\Collection manager\.dotnet-sdk\dotnet.exe' build DaCollector.sln -c Release --no-restore
  ```
- Unit tests passed: `120/120`.
  ```powershell
  & 'F:\Collection manager\.dotnet-sdk\dotnet.exe' test DaCollector.Tests/DaCollector.Tests.csproj -c Release --no-build
  ```
- Integration tests passed: `1/1`.
  ```powershell
  & 'F:\Collection manager\.dotnet-sdk\dotnet.exe' test DaCollector.IntegrationTests/DaCollector.IntegrationTests.csproj -c Release --no-build
  ```
- Build/test fixes made:
  - `DaCollector.Tests/TvdbCollectionBuilderClientTests.cs`: replaced invalid interpolated raw string with a normal interpolated JSON string.
  - `DaCollector.Tests/DaCollectorStatusServiceTests.cs`: supplied `NullLogger<PlexTargetService>.Instance`.
  - `DaCollector.Tests/PlexTargetServiceTests.cs`: supplied `NullLogger<PlexTargetService>.Instance`.

Notes:
- Release build still emits one analyzer warning in `MediaDuplicateReviewServiceTests.cs` about using `Assert.Single` instead of `Assert.Equal` for collection size.
- Integration test logs include repeated ASP.NET Data Protection DPAPI decrypt warnings, but the migration/startup test still passed. Investigate separately if these appear in normal installs.

### ◐ P0.2 — Verify Combined Docker Build on TrueNAS/Linux Docker — GHCR TRIGGERED, HOST PENDING

GitHub Actions status:
- Committed and pushed `904d0fe` to `main` which includes:
  - `docker-ghcr.yml` updated (path-triggers on `Dockerfile.combined`, WebUI checkout step, `webui_ref` input, `dacollector_webui` build-context)
  - `Dockerfile.combined` updated (TARGETARCH support → correct runtime ID for amd64/arm64)
- GHCR workflow triggered on push. Expected output: `ghcr.io/iranman/dacollector:latest` with React WebUI embedded.
- `scripts/verify-install.sh` added for shell-based endpoint and Docker health checks on the Docker host.

Remaining tasks (require TrueNAS Docker host):

**Option A — Pull prebuilt GHCR image (recommended for production):**
```bash
# On TrueNAS host, place docker-compose.yml alongside a .env
curl -O https://raw.githubusercontent.com/Iranman/DaCollector/main/docker-compose.yml
docker compose -f docker-compose.yml pull
docker compose -f docker-compose.yml up -d
docker logs dacollector -f
bash <(curl -s https://raw.githubusercontent.com/Iranman/DaCollector/main/scripts/verify-install.sh)
```

**Option B — Local build from source (for dev/testing):**
```bash
# Repos must be side by side:
# /mnt/PLEX/Apps/DaCollector/
# /mnt/PLEX/Apps/DaCollector-WebUI/
cd /mnt/PLEX/Apps/DaCollector
docker compose down
docker compose build --no-cache dacollector
docker compose up -d
docker logs dacollector -f
./scripts/verify-install.sh --docker
```

Acceptance criteria:
- Container starts and `docker logs dacollector` shows ownership repair completing without crash.
- `/api/v3/Init/Status` returns HTTP 200.
- `/webui` returns the React WebUI (not the old static HTML fallback).
- Startup reaches Kestrel on port `38111`.

### ◐ P0.3 — Verify First-Run Endpoints — LOCAL PASS, DOCKER PENDING

Tasks:
- Run from the Docker host:
  ```bash
  curl -i http://127.0.0.1:38111/api/v3/Init/Status
  curl -i http://127.0.0.1:38111/webui
  ./scripts/verify-install.sh --docker
  ```
- Run the install verification script where PowerShell is available:
  ```powershell
  ./scripts/verify-install.ps1 -BaseUrl http://127.0.0.1:38111
  ```
- Open `http://<server-ip>:38111/webui`.
- Complete first-run admin setup if the server reports setup is required.

Acceptance criteria:
- `/api/v3/Init/Status` returns a valid status response.
- `/webui` returns the React WebUI.
- First-run setup can create the admin account and reach login/dashboard.

Local result:
- Port `38111` was already occupied by a Node mock server (`node mock-server.mjs`), so local smoke used `DACOLLECTOR_PORT=38112`.
- Started `DaCollector.CLI.dll` from `DaCollector.Server/bin/Release/net10.0` with a temporary `DACOLLECTOR_HOME`.
- `scripts/verify-install.ps1 -BaseUrl http://127.0.0.1:38112` passed:
  - `/api/v3/Init/Status` returned HTTP `200`.
  - `/webui` returned HTTP `200`.
- The temporary first-run server reported `State=Waiting`, which is expected before admin setup.

Remaining:
- Repeat this against the TrueNAS Docker container on port `38111`.
- Confirm `/webui` is the React WebUI from `DaCollector-WebUI`, not only the bundled static fallback.
- Complete first-run admin setup in a browser and confirm login/dashboard.

### ◐ P0.4 — Verify Minimal Plex Path — LOCAL PLEX PASS, DOCKER PENDING

Tasks:
- Configure Plex base URL, token, and section key.
- Confirm Plex connectivity from the container.
- Run one collection sync preview or safe sync.
- Run duplicate review scan in read-only mode.

Acceptance criteria:
- DaCollector can authenticate to Plex.
- At least one Plex library section can be read.
- Collection and duplicate pages show real data or a clear actionable error.
- No duplicate deletion behavior is enabled by default.

Local result:
- Confirmed Plex is listening on local port `32400`.
- Direct Plex API calls to `/identity` and `/library/sections` succeeded.
- Plex server reported version `1.43.2.10687-563d026ea`.
- Plex returned one library section: key `4`, type `movie`, title `Movies`.
- Started DaCollector locally on port `38112` with a temporary `DACOLLECTOR_HOME`.
- Completed first-run setup using a temporary admin account and waited until `/api/v3/Init/Status` reported `State=Started`.
- DaCollector Plex target endpoints successfully read Plex identity and library sections through the API.

Remaining:
- Repeat the Plex connectivity test from inside the TrueNAS Docker container.
- In Docker, do not use `http://127.0.0.1:32400` for Plex unless Plex is running inside the same container. Use a host-reachable URL such as `http://host.docker.internal:32400` when available, or the TrueNAS/Plex LAN IP.
- Run a collection preview/safe sync against section key `4`.
- Run duplicate review in read-only mode and verify it reports results without enabling deletion behavior.

### P0.5 — Document Any New Startup Issue

Tasks:
- If a new Docker/server crash appears, add a new P0.x section with:
  - exact log excerpt
  - failing endpoint or command
  - suspected files
  - acceptance criteria
- Do not move to P1 polish until P0.1-P0.4 are either passing or explicitly blocked.

Acceptance criteria:
- Server readiness status is factual and reproducible.

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
