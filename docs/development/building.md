# Building DaCollector

DaCollector uses .NET `10.0.203` from `global.json`.

## Restore and Build

From the repository root:

```powershell
dotnet restore DaCollector.sln
dotnet build DaCollector.sln
```

If you are using the workspace-local SDK:

```powershell
$env:DOTNET_CLI_HOME = "F:\Collection manager\DaCollector\dotnet-home"
$env:DOTNET_ROOT = "F:\Collection manager\.dotnet"
& "F:\Collection manager\.dotnet\dotnet.exe" build "F:\Collection manager\DaCollector\DaCollector.sln"
```

## Publish Windows Tray Service

Framework-dependent:

```powershell
dotnet publish DaCollector.TrayService\DaCollector.TrayService.csproj -c Release -r win-x64 --no-self-contained
```

Standalone:

```powershell
dotnet publish DaCollector.TrayService\DaCollector.TrayService.csproj -c Release -r win-x64 --self-contained true
```

Published files are placed under:

```text
DaCollector.Server\bin\Release\net10.0\win-x64\publish
```

## Build Installer

Install Inno Setup and make sure `iscc.exe` is on `PATH`.

```powershell
iscc /O".\" ".\Installer\DaCollector.iss"
```

The output is:

```text
DaCollector.Setup.exe
```

## Release Workflow Artifacts

The GitHub release workflow is configured to publish:

- `DaCollector.TrayService_Framework_win-x64.zip`
- `DaCollector.TrayService_Standalone_win-x64.zip`
- `DaCollector.Setup.exe`

Use the standalone ZIP when testing on a machine without installed .NET runtimes.

## Build Docker Image

```powershell
docker compose -f compose.yaml build dacollector
```

Or build directly:

```powershell
docker build -t dacollector:local .
```

For ARM64:

```powershell
docker build -f Dockerfile.aarch64 -t dacollector:local-arm64 .
```

## Publish Docker Image

The `Publish Docker Image to GHCR` workflow builds `linux/amd64` and `linux/arm64` images and publishes multi-architecture manifests to:

```text
ghcr.io/iranman/dacollector
```

On pushes to `main` or `daccollector-main`, it publishes:

- `ghcr.io/iranman/dacollector:latest`
- `ghcr.io/iranman/dacollector:sha-<commit>`

Pull requests build both architectures without pushing.
