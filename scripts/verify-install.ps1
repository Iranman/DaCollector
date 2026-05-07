#Requires -Version 5.1
<#
.SYNOPSIS
    Verifies a DaCollector installation is reachable on the expected port.
.PARAMETER Port
    Host port DaCollector listens on. Defaults to 38111.
.PARAMETER Docker
    Also check the Docker container health status for the 'dacollector' container.
.EXAMPLE
    .\verify-install.ps1
.EXAMPLE
    .\verify-install.ps1 -Port 38112 -Docker
#>
[CmdletBinding()]
param (
    [int]$Port = 38111,
    [switch]$Docker
)

$baseUrl = "http://127.0.0.1:$Port"
$failed = 0

function Test-Endpoint {
    param (
        [string]$Url,
        [string]$Label
    )
    try {
        $response = Invoke-WebRequest $Url -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-Host "  [OK]   $Label" -ForegroundColor Green
        }
        else {
            Write-Host "  [FAIL] $Label — HTTP $($response.StatusCode) ($Url)" -ForegroundColor Red
            $script:failed++
        }
    }
    catch {
        Write-Host "  [FAIL] $Label — cannot connect ($Url): $($_.Exception.Message)" -ForegroundColor Red
        $script:failed++
    }
}

Write-Host ""
Write-Host "DaCollector Install Verification"
Write-Host "================================="
Write-Host "Base URL : $baseUrl"
Write-Host ""

Write-Host "HTTP endpoints:"
Test-Endpoint "$baseUrl/api/v3/Init/Status" "Startup API  ($baseUrl/api/v3/Init/Status)"
Test-Endpoint "$baseUrl/webui"              "Web UI       ($baseUrl/webui)"

if ($Docker) {
    Write-Host ""
    Write-Host "Docker container:"
    $health = docker inspect dacollector --format '{{.State.Health.Status}}' 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  [FAIL] Container 'dacollector' not found or Docker is not running." -ForegroundColor Red
        $failed++
    }
    elseif ($health -eq 'healthy') {
        Write-Host "  [OK]   Container health: $health" -ForegroundColor Green
    }
    elseif ($health -eq 'starting') {
        Write-Host "  [WARN] Container health: $health (still starting — wait and retry)" -ForegroundColor Yellow
    }
    else {
        Write-Host "  [FAIL] Container health: $health" -ForegroundColor Red
        $failed++
    }

    Write-Host ""
    Write-Host "Recent container logs (last 20 lines):"
    docker compose logs --tail 20 dacollector 2>$null
}

Write-Host ""
if ($failed -eq 0) {
    Write-Host "All checks passed." -ForegroundColor Green
    exit 0
}
else {
    Write-Host "$failed check(s) failed. See docs/getting-started/verify-install.md for troubleshooting." -ForegroundColor Red
    exit 1
}
