# Verify an Install

Use this checklist after installing DaCollector on Windows or with Docker. It verifies the server process, the Web UI, and a clean SQLite startup path.

## What Passing Looks Like

| Check | Expected result |
| --- | --- |
| Port | DaCollector listens on TCP `38111`. |
| Startup API | `GET http://127.0.0.1:38111/api/v3/Init/Status` returns HTTP `200`. |
| Web UI | `http://127.0.0.1:38111/webui` returns HTTP `200` and loads in a browser. |
| SQLite startup | A clean data directory creates SQLite databases without migration errors in the logs. |

## Quick Check with the Verification Script

For a one-command check, run the included PowerShell script from the repository root:

```powershell
.\scripts\verify-install.ps1
```

Pass `-Port` to use a non-default port, or `-Docker` to also check the container health and print recent logs:

```powershell
.\scripts\verify-install.ps1 -Port 38112 -Docker
```

The script exits `0` when all checks pass and `1` when any check fails.

---

## Windows Verification

Start DaCollector from the installer, ZIP, or local publish. Then run:

```powershell
$baseUrl = "http://127.0.0.1:38111"
Invoke-WebRequest "$baseUrl/api/v3/Init/Status" -UseBasicParsing
Invoke-WebRequest "$baseUrl/webui" -UseBasicParsing
```

Both commands should return `StatusCode : 200`.

Check the port listener:

```powershell
Get-NetTCPConnection -LocalPort 38111 -State Listen
```

For a clean SQLite startup test, run DaCollector with a temporary data directory:

```powershell
$env:DACOLLECTOR_HOME = "$env:TEMP\DaCollector-verify"
Remove-Item $env:DACOLLECTOR_HOME -Recurse -Force -ErrorAction SilentlyContinue
.\DaCollector.exe
```

After the Web UI loads, inspect the log folder under `$env:DACOLLECTOR_HOME`. There should be no startup failure, database blocked state, or migration exception.

## Docker Compose Verification

Start the recommended Compose service:

```powershell
docker compose up -d
docker compose ps
```

The `dacollector` service should be running and should publish `38111:38111`.

Verify the HTTP endpoints:

```powershell
$baseUrl = "http://127.0.0.1:38111"
Invoke-WebRequest "$baseUrl/api/v3/Init/Status" -UseBasicParsing
Invoke-WebRequest "$baseUrl/webui" -UseBasicParsing
```

Check logs for startup or migration errors:

```powershell
docker compose logs --tail 200 dacollector
```

For a clean SQLite startup test, stop the service, remove only the DaCollector data volume for this test instance, then start it again:

```powershell
docker compose down
docker volume rm dacollector_dacollector-data
docker compose up -d
docker compose logs -f dacollector
```

Only remove the volume when you intentionally want to reset the test instance.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| `Invoke-WebRequest` cannot connect | Confirm DaCollector is running and no other process owns port `38111`. |
| `/api/v3/Init/Status` returns an error | Read the latest server log and look for startup or migration exceptions. |
| `/webui` returns 404 | Confirm the published output includes the `webui` folder. |
| Docker starts but Windows cannot connect | Confirm the Compose file maps `"38111:38111"` and Docker Desktop is running. |
| Clean SQLite startup fails | Save the logs before deleting the temporary data directory or Docker volume. |
| Docker appears stuck after `Starting ownership repair...` | The container is running `chown -R` on the data directory. On TrueNAS/ZFS with many files this takes minutes. Set `SKIP_CHOWN=true` in `docker-compose.yml` to bypass it if you manage permissions externally. See [TrueNAS and ZFS](installation/docker.md#truenas-and-zfs). |
