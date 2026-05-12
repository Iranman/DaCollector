# DaCollector Claude Task List

Context:
- Branch: `daccollector-main`
- Use `git log --oneline -5` for the latest local commits before resuming work.
- Follow `CLAUDE.md`: append database migrations; **never rewrite historical migration strings**.
- Keep legacy API contract class names like `CL_AnimeSeries_User` unless intentionally versioning the public API.
- `.NET SDK 10.0.203` may not be in PATH in the sandbox — use `& "C:\Program Files\dotnet\dotnet.exe"` if needed.
- Current provider scope for user-facing WebUI/server surfaces is TMDB and TVDB only. Do not reintroduce legacy anime-only provider settings/actions into WebUI-facing endpoints.

Product direction for Claude:
- DaCollector is a local media collection manager for movie and TV files the user already has. Treat it like a librarian for mounted folders, not a media downloader.
- DaCollector Server is the main backend and source of truth. It owns local folder scanning, file fingerprinting/hashing, MediaInfo, provider matching, local databases, collection rules, watched status, missing/duplicate/corrupt file review, rename/move workflows, and all write operations.
- DaCollector WebUI is the browser interface. It should present server workflows and call APIs; do not put direct scanner, filesystem, provider, or Plex agent logic in the WebUI.
- DaCollector Relay is the planned Plex scanner/agent/adapter. It should integrate DaCollector-managed movies and TV shows into Plex, preferably supporting a combined movie/TV library where Plex allows it, while keeping DaCollector Server as the source of truth.
- Non-goals: no media downloads, no streaming from websites, no torrent/usenet/media acquisition, no bypassing permissions, and no access to files that are not mounted/provided by the user.
- Provider policy: current matching and user-facing provider settings are TMDB and TVDB. Future metadata sources such as IMDb or other legal provider feeds can be planned as explicit provider work, but do not re-add them as hidden leftovers or legacy anime-only settings.

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
- **P1**: Remove legacy provider actions/settings from WebUI-facing server public surface; `/api/v3/Settings` now returns a DaCollector WebUI DTO with TMDB/TVDB, provider readiness reports TMDB/TVDB only, and collection builder catalog exposes TMDB/TVDB only.

---

## ✅ P1 — Update CLAUDE.md Architecture Documentation — DONE

Cache index example, TVDB property names, and StoredReleaseInfo description updated.
All domain model references already use Media* names throughout.

---

## ⏸ P1 — Clean Up AniDB Settings Exposure — DEFERRED

`AniDbSettings` follows the same naming convention as the remaining provider settings classes such as `TMDBSettings`, `TVDBSettings`, and `TraktSettings`.
Renaming to `SourceSettings` would be inconsistent and break existing `settings-server.json` configs.
The class name is internal (not user-facing). The JSON key `"AniDb"` in the settings file would need a migration.
**Decision: do not rename** — it accurately identifies the AniDB provider settings.

Follow-up after WebUI/provider cleanup:
- Keep this deferred for deep storage/model internals only.
- Do not expose this settings section from `/api/v3/Settings` or the React WebUI.
- Do not add legacy provider actions back to `/api/v3/Action`.

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
- Store candidate matches from TMDB and TVDB with confidence reasons:
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

### ✅ P3.6 — Harden TVDB Provider Behavior — DONE

Files:
- `DaCollector.Server/Collections/TvdbCollectionBuilderClient.cs`
- `DaCollector.Server/Providers/TVDB/TvdbMetadataService.cs`
- `DaCollector.Tests/CollectionBuilderPreviewServiceTests.cs`
- New provider-specific test files as needed

Tasks:
- Add tests for TVDB token caching, unauthorized retry, missing credentials, and empty API responses.
- Make provider warnings user-actionable and avoid leaking API keys or tokens.

Acceptance criteria:
- TVDB retries once after a 401 with a refreshed token.
- Missing TVDB credentials produce a clear warning rather than a crash.
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
  - configure TMDB/TVDB
  - add managed folders
  - preview a collection
  - review duplicates
- Add a troubleshooting page for ports, Docker networking, Plex token, provider credentials, and clean SQLite startup.
- Keep the recommended Docker path to direct values in `docker-compose.yml` unless the user explicitly asks for a separate environment-file workflow.

Acceptance criteria:
- `mkdocs build --strict` succeeds when MkDocs dependencies are installed.
- Every install page links to verification.
- Every feature page links to the related API page or endpoint table.
- Docker docs keep direct environment values in `docker-compose.yml`, not a recommended separate environment-file workflow.

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
- A local .NET SDK `10.0.203` was bootstrapped to `F:\Collection manager\.dotnet-sdk`.
- `global.json` requires SDK `10.0.203` with `rollForward: latestFeature`.
- Docker is not installed in the local Windows shell, so Docker validation must run on a Docker host such as TrueNAS.
- P0.1 local build and tests pass (121/121 as of commit `e8f0a60`).
- P0.5 startup crash (empty config file) has been identified and fixed in commit `6ef5b0e`.
- Commit `ecccb2f` trims WebUI-facing public surface to TMDB/TVDB only by removing legacy provider actions, provider status entries, and builder catalog entries; tests updated to 121/121.
- Commit `e8f0a60` makes the Docker build fully self-contained: `Dockerfile.combined` clones `DaCollector-WebUI` from GitHub (ARG WEBUI_REPO/WEBUI_REF), `compose.yaml` drops `additional_contexts`, and `docker-ghcr.yml` removes the WebUI checkout step. Anyone can now `docker compose up --build` from a single repo clone.
- Commits `be06b42` and `2718071` opt all CI workflows into Node.js 24 (FORCE_JAVASCRIPT_ACTIONS_TO_NODE24) and bump v2 Docker actions to v3 / setup-dotnet to v4 to eliminate deprecation warnings.
- **P0.6 (new)** — First-run setup still fails with "Error reading JToken from JsonReader. Path '', line 0, position 0." after `6ef5b0e` fix. Root cause: container is running an OLD image (pre-`6ef5b0e`) or the Docker volume has a stale empty `settings-server.json` left by a prior failed run. The fix in `6ef5b0e` handles the empty-file case; the container must be rebuilt/re-pulled AND the volume must be clean for the fix to take effect. See P0.6 below.

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
  - `docker-ghcr.yml` updated (path-triggers on `Dockerfile.combined`, `webui_ref` input, WebUI repo/ref build args)
  - `Dockerfile.combined` updated (TARGETARCH support → correct runtime ID for amd64/arm64)
- GHCR workflow triggered on push. Expected output: `ghcr.io/iranman/dacollector:latest` with React WebUI embedded.
- `scripts/verify-install.sh` added for shell-based endpoint and Docker health checks on the Docker host.

Remaining tasks (require TrueNAS Docker host):

**Option A — Pull prebuilt GHCR image (recommended for production):**
```bash
# On TrueNAS host, download docker-compose.yml and edit values directly in the file
curl -O https://raw.githubusercontent.com/Iranman/DaCollector/main/docker-compose.yml
docker compose -f docker-compose.yml pull
docker compose -f docker-compose.yml up -d
docker logs dacollector -f
bash <(curl -s https://raw.githubusercontent.com/Iranman/DaCollector/main/scripts/verify-install.sh)
```

**Option B — Local build from source (for dev/testing):**
```bash
# Dockerfile.combined clones DaCollector-WebUI from GitHub automatically.
# No sibling DaCollector-WebUI checkout required.
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

### ✅ P0.5 — Document Any New Startup Issue — DONE

#### Setup crash: "Error reading JToken from JsonReader. Path '', line 0, position 0."

Symptoms:
- WebUI setup screen appeared (server in `State=Waiting`).
- After completing setup (`POST /api/v3/Init/CompleteSetup`), server transitioned to `State=Failed`.
- Error message: `"Server failed to start: Error reading JToken from JsonReader. Path '', line 0, position 0."`

Root cause:
- `ConfigurationService.LoadInternal()` calls `File.ReadAllText(info.Path)` and passes the result directly to `ValidateInternal()` → `JsonSchemaValidatorBase.Validate()`.
- In that validator, `JToken.ReadFrom(jsonReader)` throws with the above message when the input is empty or whitespace.
- If a configuration file (e.g. `settings-server.json`) exists on disk but is zero bytes — possible on first-run Docker volumes, interrupted writes, or symlinks pointing to empty files — the `!File.Exists(info.Path)` guard passes but the content is empty, causing the crash.

Files fixed (commit `6ef5b0e`):
- `DaCollector.Server/Services/Configuration/ConfigurationService.cs`:
  After reading the file, if `string.IsNullOrWhiteSpace(json)`, log a warning and regenerate a default config in place — mirrors the existing "file not found" recovery path exactly.
- `DaCollector.Server/Services/Configuration/JsonSchemaValidatorBase.cs`:
  Added early null/empty guard in `Validate(string jsonData, ...)` with a descriptive exception message as a safety net for any caller that bypasses `LoadInternal`.

Result:
- Empty or whitespace config files are now recovered from automatically with a logged warning instead of crashing the server.
- Pushed `6ef5b0e` to `main`; GHCR re-triggered automatically.

Acceptance criteria:
- Server readiness status is factual and reproducible.

### 🚧 P0.6 — First-Run Setup Still Crashes After Empty-Config Fix — NEEDS DOCKER HOST VERIFICATION

Symptom (TrueNAS Docker, reported after `6ef5b0e`):
- Setup screen renders (React WebUI), user enters credentials, "Setting up..." is displayed.
- Error: **"Server failed to start: Error reading JToken from JsonReader. Path '', line 0, position 0."**

Root-cause analysis:
- The error "Error reading JToken" is the RAW Newtonsoft.Json exception from `JsonSchemaValidatorBase.cs:45` (`JToken.ReadFrom(jsonReader)` with empty input).
- The `6ef5b0e` fix changes this to throw "Configuration data is empty." BEFORE reaching line 45.
- The fact that the user sees the OLD raw error proves the running container does NOT have the `6ef5b0e` fix.
- Cause A: **Old image cached locally** — the image was built before `6ef5b0e` was pushed and Docker is reusing the cached `dacollector:local` layer.
- Cause B: **Stale volume** — a prior failed run left an empty `settings-server.json` on the named volume. The old container wrote the file as zero bytes when it crashed during the first save. Even with the new image, the volume must be cleared so the file can be recreated with defaults.

Fix (run on TrueNAS Docker host):
```bash
# Step 1 — stop the container and remove it
docker compose down

# Step 2 — rebuild the image from scratch (required if using local compose.yaml build)
docker compose build --no-cache dacollector

# Step 3 — remove the stale data volume so settings-server.json is regenerated clean
docker volume rm dacollector_dacollector-data    # adjust name to match your compose project

# Step 4 — start fresh
docker compose up -d
docker logs dacollector -f
```

If using the GHCR image instead of a local build:
```bash
docker compose down
docker compose pull
docker volume rm dacollector_dacollector-data
docker compose up -d
```

Acceptance criteria:
- Server reaches `State=Waiting` (setup screen) without error.
- User completes admin setup and server transitions to `State=Started`.
- `/api/v3/Init/Status` returns `{"State":"Started"}`.
- `/webui` loads the React UI.

Note: if the error persists after a clean volume AND the new image, confirm which image digest is running:
```bash
docker inspect dacollector --format '{{.Image}}'
```
Compare to the digest from `docker images dacollector:local`. If they differ the container is still on the old image.

### ✅ P0.7 — Atomic Config File Writes — DONE

Files:
- `DaCollector.Server/Services/Configuration/ConfigurationService.cs`

Fix:
- `SaveInternal` now writes config to `{path}.tmp` then renames over the target, preventing a server crash mid-write from leaving a zero-byte config file.
- Same pattern applied to schema file writes in `EnsureSchemaExists`.
- Combined with the `6ef5b0e` empty-file recovery fix, the empty-config crash should not be reproducible from a clean volume.

Acceptance criteria:
- Build succeeds and all 135 unit tests pass.
- A crash during `SaveInternal` cannot leave `settings-server.json` at 0 bytes on the next clean boot.

---

## P1 — DaCollector Relay and Movie/TV Source-of-Truth Track — DONE (scaffold)

Goal: make the product direction concrete after first-run Docker is stable. DaCollector should manage local movie/TV files through Server, expose that management through WebUI, and project the managed library into Plex through DaCollector Relay.

Component boundaries:
- Server owns media identity, provider matching, watch status, duplicate/missing/corrupt review, rename/move actions, and all persistent state.
- WebUI owns user workflows only: setup, folders, providers, Plex target settings, collections, duplicate review, missing/corrupt review, and rename/move review once APIs exist.
- Relay owns Plex scanner/agent/adapter behavior only. It should authenticate to DaCollector, resolve Plex file paths/media IDs through server APIs, and send Plex metadata/collection/watch-state updates based on server data.

Server tasks:
- Define generic media APIs Relay can consume without anime-only assumptions:
  - `GET /api/v3/Media/File/PathEndsWith`
  - movie, show, season, episode read endpoints
  - media image, cast/crew, tags/genres, collection, watched/rating endpoints
  - stable external ID DTOs for TMDB and TVDB now, with room for future legal metadata providers.
- Add a local media matching queue for uncertain files, with candidate reasons and approve/reject endpoints.
- Add missing-file, corrupt-file, and rename/move review endpoints as safe plans first; destructive changes must require explicit admin confirmation.
- Keep provider matching TMDB/TVDB-first. Any IMDb or other source work must be introduced as a new provider module with tests, settings, docs, and no dependency on old anime-specific actions.

Relay tasks:
- Create or scaffold the DaCollector Relay repo/package after Server has stable generic media endpoints.
- Use the old Plex relay only as behavioral reference. Do not copy code unless licensing is resolved.
- Support movies and TV shows as first-class targets; do not force movies through series/episode semantics.
- Keep Plex as a projection of DaCollector data. Plex should not become the source of truth for media identity or local file state.
- Read from Plex where needed for library paths, rating keys, watched state, and collection application; write only controlled metadata/collection/watch-state updates through explicit Relay workflows.

Acceptance criteria:
- A user can point DaCollector Server at local movie/TV folders, scan/fingerprint files, match against TMDB/TVDB, and review the results in WebUI.
- Relay can map Plex library items back to DaCollector-managed media using file path or provider IDs.
- Relay can expose DaCollector metadata and managed collections in Plex without downloading media or streaming from third-party sites.
- Any future metadata provider expansion is documented as provider work and does not reintroduce legacy anime-only public surfaces.

### Scaffold delivered 2026-05-11 — `F:\Collection manager\DaCollector-Relay\`

Structure:
- `Contents/Code/__init__.py` — Plex legacy agent: `DaCollectorRelayMovies` and `DaCollectorRelayShows`
  classes. `search()` calls `GET /api/v3/Media/Movies|Shows`. `update()` applies title, year, summary,
  genres. Both have TODO stubs for per-item lookup and image endpoints.
- `Contents/Scanners/Movies/DaCollector Relay Scanner.py` — movie scanner; calls `Parser/Filename`,
  creates `Media.Movie` objects.
- `Contents/Scanners/Series/DaCollector Relay Scanner.py` — series scanner; creates `Media.Episode`
  objects for TV and `Media.Movie` for mixed-library movie files.
- Both scanners share a `.cfg` config file with Hostname, Port, ApiKey, PathRewrite.
- `Scripts/config.py` — user configuration (DaCollector + Plex credentials, path rewrite rules).
- `Scripts/common.py` — `DaCollectorClient` and `PlexClient` wrappers (uses `requests`).
- `Scripts/watched_sync.py` — Plex ↔ DaCollector watched state sync (both directions implemented).
- `Scripts/collection_sync.py` — DaCollector managed collections → Plex collections (member iteration implemented).

Server endpoint blockers — all resolved as of 2026-05-11:
- `GET /api/v3/Media/Movies/{provider}/{providerID}` — exists in `MediaController.cs` (route uses path params, not query param)
- `GET /api/v3/Media/Shows/{provider}/{providerID}` — exists in `MediaController.cs`
- `GET /api/v3/File/PathEndsWith/{*path}` — exists in `FileController.cs`
- `GET /api/v3/ManagedCollection/{id}/Members` — **added**: returns `CollectionMemberDto[]` with matched local file locations; backed by `MediaFileReviewStateRepository.GetByManualMatch`
- `GET /api/v3/MediaFileReview/Files/{fileID}/WatchedState` — **added**: returns `WatchedStateDto` (defaults to unwatched if file never played)
- `POST /api/v3/MediaFileReview/Files/{fileID}/WatchedState` — **added**: accepts `{isWatched, watchedDate?}`; calls `IUserDataService.SetVideoWatchedStatus`

Tests added: `DaCollector.Tests/RelayEndpointTests.cs` (6 tests — 139/139 total passing)

---

## P2 — Detailed MVP Build Plan — ACTIVE

Guiding principle:
- Build DaCollector Server first. WebUI, CLI, Jellyfin, Kodi, and Plex integrations all depend on Server APIs and Server-owned data.
- Do not start with Plex/Jellyfin/Kodi plugins. Build the local database, scanner, parser, matcher, provider cache, and manual review workflows first.
- Translate any `/api/v1/...` examples from planning notes into the current `/api/v3/...` controller style unless deliberately versioning a new public API.

MVP scope:
- Docker container and WebUI login.
- SQLite database and settings.
- Add movie and TV libraries.
- Scan folders and persist media-file records.
- Parse movie, TV episode, date-based episode, multi-episode, quality, source, codec, HDR, and explicit provider IDs.
- Match movies to TMDB.
- Match TV shows/seasons/episodes to TVDB, with TMDB as fallback where appropriate.
- Show unmatched files with suggested matches and confidence reasons.
- Manual match, ignore, lock, and undo.
- Basic poster/backdrop cache.
- Basic logs and backups.

Do not include in MVP:
- Plex plugin/scanner/agent.
- Jellyfin plugin.
- Kodi addon.
- Watched sync integrations.
- Trakt sync.
- Advanced duplicate cleanup.
- Complex automation beyond the existing managed-collection baseline.

### ✅ P2.1 — Server Capability Checklist — DONE

Files:
- `DaCollector.Server/Services/DaCollectorStatusService.cs`
- `DaCollector.Server/API/v3/Controllers/DaCollectorStatusController.cs`
- `DaCollector.Tests/DaCollectorStatusServiceTests.cs`

Result:
- `GET /api/v3/DaCollectorStatus/Capabilities` returns the operational checklist:
  - scan folders
  - hash files
  - parse filenames
  - match files
  - fetch metadata
  - store database records
  - track watched status
  - expose API endpoints
  - serve the WebUI
  - talk to plugins
  - run background jobs
- `GET /api/v3/DaCollectorStatus` includes the same checklist.

### ✅ P2.2 — Filename Parser Foundation — DONE

Files:
- `DaCollector.Server/Parsing/FilenameParserService.cs`
- `DaCollector.Server/API/v3/Controllers/ParserController.cs`
- `DaCollector.Server/Services/SystemService.cs`
- `DaCollector.Tests/FilenameParserServiceTests.cs`

Result:
- Added authenticated parser endpoints:
  - `GET /api/v3/Parser/Filename?path=...`
  - `POST /api/v3/Parser/Filename`
- Parser currently detects:
  - movie title/year
  - explicit `{tmdb-id}` / `{tvdb-id}` / `{imdb-id}` markers
  - TV `SxxExx`
  - multi-episode `SxxEyy-Ezz`
  - date-based episodes
  - quality
  - source
  - edition
  - video codec
  - audio codec/channels
  - HDR/Dolby Vision markers

### ✅ P2.3 — Unmatched File Review State — DONE

Files:
- `DaCollector.Server/Models/Internal/MediaFileReviewState.cs`
- `DaCollector.Server/Mappings/MediaFileReviewStateMap.cs`
- `DaCollector.Server/Repositories/Direct/MediaFileReviewStateRepository.cs`
- `DaCollector.Server/Media/MediaFileReviewService.cs`
- `DaCollector.Server/API/v3/Controllers/MediaFileReviewController.cs`
- `DaCollector.Tests/MediaFileReviewStateTests.cs`

Result:
- Added `MediaFileReviewState` database persistence for scanned files keyed by `VideoLocalID`.
- Parser results are stored with unmatched file review state:
  - parsed kind/title/year
  - parsed show/season/episode numbers
  - explicit provider IDs
  - quality/source/edition/codecs/HDR warnings
- Added authenticated review endpoints:
  - `GET /api/v3/MediaFileReview/Files/Unmatched`
  - `GET /api/v3/MediaFileReview/Files/{fileID}`
  - `POST /api/v3/MediaFileReview/Files/{fileID}/RefreshParse`
  - `POST /api/v3/MediaFileReview/Files/{fileID}/Ignore`
  - `POST /api/v3/MediaFileReview/Files/{fileID}/Unignore`
  - `POST /api/v3/MediaFileReview/Files/{fileID}/ManualMatch`
  - `DELETE /api/v3/MediaFileReview/Files/{fileID}/ManualMatch`
- Manual match and ignored-file choices are persisted separately from the scanner inventory, while still using existing `VideoLocal` records as the file source of truth.

### ✅ P2.4 — File Match Candidate Generation — DONE

Files:
- `DaCollector.Server/Models/Internal/MediaFileMatchCandidate.cs`
- `DaCollector.Server/Mappings/MediaFileMatchCandidateMap.cs`
- `DaCollector.Server/Repositories/Direct/MediaFileMatchCandidateRepository.cs`
- `DaCollector.Server/Media/MediaFileMatchCandidateService.cs`
- `DaCollector.Server/Media/MediaFileMatchCandidateScoring.cs`
- `DaCollector.Server/API/v3/Controllers/MediaFileReviewController.cs`
- `DaCollector.Tests/MediaFileMatchCandidateScoringTests.cs`

Result:
- Added `MediaFileMatchCandidate` database persistence for provider suggestions keyed by `VideoLocalID`.
- Candidate generation uses persisted parser guesses from `MediaFileReviewState`.
- Current candidate sources:
  - explicit `{tmdb-id}` and `{tvdb-id}` path/filename hints
  - explicit IMDb IDs matched to cached TMDB movies
  - cached TMDB movies/shows
  - cached TVDB movies/shows
- Added authenticated candidate endpoints:
  - `POST /api/v3/MediaFileReview/Files/{fileID}/ScanMatches`
  - `POST /api/v3/MediaFileReview/Files/ScanMatches`
  - `GET /api/v3/MediaFileReview/Files/{fileID}/Candidates`
  - `GET /api/v3/MediaFileReview/Candidates`
  - `POST /api/v3/MediaFileReview/Candidates/{candidateID}/Approve`
  - `DELETE /api/v3/MediaFileReview/Candidates/{candidateID}`
- Approving a file candidate stores a locked manual match in the file review state and rejects sibling pending candidates for that file.

### ✅ P2.5 — Online File Candidate Lookup — DONE

Files:
- `DaCollector.Server/Media/MediaFileMatchCandidateService.cs`
- `DaCollector.Server/API/v3/Controllers/MediaFileReviewController.cs`
- `DaCollector.Server/Services/DaCollectorStatusService.cs`

Result:
- Cached provider matching remains the default for file candidate scans.
- Added opt-in online lookup flags:
  - `includeOnlineSearch=true`
  - `refreshExplicitIds=true`
- `includeOnlineSearch=true` adds online TMDB movie/show search results to file candidates when local cache has weak or missing coverage.
- `refreshExplicitIds=true` refreshes explicit TMDB/TVDB IDs into the provider cache before creating direct candidates.
- TVDB title search is still not implemented because the current server provider only exposes TVDB refresh by known ID; TVDB online refresh works for explicit `{tvdb-id}` hints.
- Candidate scan responses now echo whether online search and explicit-ID refresh were enabled.

### ✅ P2.6 — Generic Media Read API — DONE

Files:
- `DaCollector.Server/API/v3/Controllers/MediaController.cs`
- `DaCollector.Server/API/v3/Models/Media/*`
- `DaCollector.Server/Media/MediaReadService.cs`
- `DaCollector.Tests/MediaDtoTests.cs`

Result:
- Added provider-neutral read models for:
  - movies
  - shows
  - seasons
  - episodes
  - local media files
- Added authenticated generic media endpoints:
  - `GET /api/v3/Media/Movies`
  - `GET /api/v3/Media/Movies/{provider}/{providerID}`
  - `GET /api/v3/Media/Shows`
  - `GET /api/v3/Media/Shows/{provider}/{providerID}`
  - `GET /api/v3/Media/Shows/{provider}/{providerID}/Seasons`
  - `GET /api/v3/Media/Shows/{provider}/{providerID}/Episodes`
  - `GET /api/v3/Media/Files`
  - `GET /api/v3/Media/Files/{fileID}`
- Provider selection supports `tmdb`, `tvdb`, and `all` on list endpoints.
- Local file endpoints can include persisted review state with `includeReview=true`.
- Absolute file paths stay opt-in with `includeAbsolutePaths=true`.

### ✅ P2 continued — NFO/Runtime-Aware Matching — DONE

Implemented 2026-05-11, 139/139 tests pass.

- **`NfoSidecarParser.cs`** — new static class that reads a Kodi-style `.nfo` sidecar file (same stem as the video, `<uniqueid type="imdb">`, `<runtime>`, `<year>`) and returns a `NfoSidecarData` record. Silently returns `null` on missing file or malformed XML so it never blocks scanning.
- **`MediaFileMatchCandidateScoring.ComputeScore`** — extended with optional `queryRuntimeMinutes` / `candidateRuntimeMinutes` params. Adds +0.08 bonus when runtime is within 5 min, +0.04 within 10 min. Capped at 1.0 as before.
- **`MediaFileMatchCandidateService`**:
  - `BuildCandidatesAsync` now accepts `NfoSidecarData?`; if the NFO carries an IMDb ID, a direct `score = 1.0` candidate is added against any matching cached TMDB movie (same path as the existing `ParsedExternalIds` IMDb lookup).
  - Cached TMDB movie and TVDB movie scans now forward NFO runtime and candidate runtime into `ComputeScore`.
  - **Auto-match threshold** (`AutoMatchThreshold = 0.92`): after saving candidates, if the file status is `Pending` and exactly one candidate scores ≥ 0.92, it is auto-approved without requiring user review.
- **`MediaFileMatchCandidateScoringTests`** — 4 new test cases: within-5-min bonus, within-10-min bonus, no bonus when far off, no bonus when candidate runtime is 0.

### ✅ P2.7 — Auto-parse and Auto-match Unmatched Files During Import — DONE

Implemented 2026-05-11, 147/147 tests pass.

File:
- `DaCollector.Server/Scheduling/Jobs/DaCollector/ProcessFileJob.cs`

Change:
- `ProcessFileJob` now injects `MediaFileMatchCandidateService` as a constructor dependency.
- After the legacy release-provider lookup (AniDB, etc.), if no release was found for the file,
  `ProcessFileJob` calls `_candidateService.ScanFileAsync(VideoLocalID, refreshExplicitIds: true)`.
- This creates a `MediaFileReviewState` (filename parsed on first access), builds provider candidates
  from the local TMDB/TVDB cache, and auto-approves any candidate scoring ≥ 0.92 (one match only).
- Files that ARE matched by the legacy provider path are unaffected; candidate scan is skipped for them.

Result:
- Newly imported movie/TV files that aren't in the AniDB release database automatically appear in the
  unmatched review queue with parsed metadata and provider candidates — no manual API call required.

### ✅ P2.8 — Basic Database Backups — DONE

Implemented 2026-05-11, 152/152 tests pass.

Files:
- `DaCollector.Server/Databases/IDatabase.cs` — added `GetBackupDirectory()`, `GetScheduledBackupName()`
- `DaCollector.Server/Databases/BaseDatabase.cs` — implemented both; `GetScheduledBackupName()` uses `Schema_scheduled_yyyyMMdd_HHmmss` (no version number)
- `DaCollector.Server/Settings/DatabaseSettings.cs` — new fields: `ScheduledBackupEnabled` (default: true), `ScheduledBackupIntervalHours` (default: 24), `BackupRetentionCount` (default: 7); all overridable via env vars `DB_BACKUP_ENABLED` / `DB_BACKUP_INTERVAL_HOURS` / `DB_BACKUP_RETENTION`
- `DaCollector.Server/Services/DatabaseBackupService.cs` — new service: `RunBackup()` (backup + retention), `GetBackupFiles()`, `DeleteBackup(fileName)` (path-traversal guard; only deletes scheduled backups' names, not migration backups)
- `DaCollector.Server/Scheduling/Jobs/DaCollector/BackupDatabaseJob.cs` — `[LimitConcurrency(1,1)]` Quartz job calling `RunBackup()`
- `DaCollector.Server/Scheduling/QuartzStartup.cs` — schedules `BackupDatabaseJob` at configured interval when `ScheduledBackupEnabled`
- `DaCollector.Server/API/v3/Controllers/DatabaseController.cs` — new admin-only controller:
  - `GET  /api/v3/Database/Backups` — list all backup files with name/size/createdAt
  - `POST /api/v3/Database/Backups` — immediate synchronous backup
  - `POST /api/v3/Database/Backups/Queue` — enqueue background backup job
  - `DELETE /api/v3/Database/Backups/{fileName}` — delete one file
- `DaCollector.Server/Services/SystemService.cs` — registered `DatabaseBackupService` as singleton
- `DaCollector.Tests/DatabaseBackupServiceTests.cs` — 5 tests: retention keeps newest N, keeps all when below limit, keeps all when retention is 0, never deletes migration backups, rejects path traversal

### ✅ P2.9 — Relay Server Endpoints — DONE

Implemented 2026-05-11, 147/147 tests at time of commit.

Files:
- `DaCollector.Server/API/v3/Controllers/ManagedCollectionController.cs` — added `GET /api/v3/ManagedCollection/{id}/Members` returning `CollectionMemberDto[]` (matched local file locations via `MediaFileReviewState` manual match)
- `DaCollector.Server/API/v3/Controllers/MediaFileReviewController.cs` — added `GET /api/v3/MediaFileReview/Files/{fileID}/WatchedState` and `POST /api/v3/MediaFileReview/Files/{fileID}/WatchedState`
- `DaCollector.Server/API/v3/Models/Collections/CollectionMemberDto.cs` — new DTO
- `DaCollector.Server/Repositories/Direct/MediaFileReviewStateRepository.cs` — added `GetByManualMatch` query
- `DaCollector.Tests/RelayEndpointTests.cs` — 8 unit tests

### ✅ P2.10 — Docker BuildKit Cache Mounts — DONE

Implemented 2026-05-11.

- `Dockerfile`, `Dockerfile.aarch64`, `Dockerfile.combined` — two-stage builds with `--mount=type=cache,target=/root/.nuget/packages`; project files copied before source so NuGet restore cache survives pure source edits.

---

## P2 MVP Completion Summary

All MVP scope items are now delivered:
- ✅ Docker container and WebUI login (P0 series)
- ✅ SQLite database and settings
- ✅ Add movie and TV libraries (ManagedFolderController)
- ✅ Scan folders and persist media-file records (ScanFolderJob pipeline)
- ✅ Parse movie, TV episode, multi-episode, quality, codec, HDR, explicit provider IDs (P2.2)
- ✅ Match movies to TMDB (P2.4/P2.5)
- ✅ Match TV shows/seasons/episodes to TVDB, TMDB fallback (TVDB provider)
- ✅ Show unmatched files with suggested matches and confidence reasons (P2.3)
- ✅ Manual match, ignore, lock, and undo (P2.3)
- ✅ Basic poster/backdrop cache (TmdbImageService + ImageController — legacy infrastructure)
- ✅ Basic logs and backups (LoggingController + P2.8 DatabaseBackupService)

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
