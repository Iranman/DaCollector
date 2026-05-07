# Windows Installation

DaCollector targets Windows x64 for the tray service and server bundle. The easiest install path is the release installer when it is published. The fallback is a standalone ZIP or a local publish from source.

If you prefer a containerized headless server, use the [Docker installation guide](docker.md).

## Requirements

| Install type | Requirement |
| --- | --- |
| `DaCollector.Setup.exe` | Windows x64. The installer opens port `38111` if you select the firewall task. |
| `DaCollector.TrayService_Standalone_win-x64.zip` | Windows x64. The .NET runtime is bundled. |
| `DaCollector.TrayService_Framework_win-x64.zip` | Windows x64 plus .NET 10 Desktop Runtime and ASP.NET Core Runtime. |
| Source build | .NET SDK `10.0.203` or a compatible newer feature band. |

## Option 1: Install from GitHub Release

1. Go to the [DaCollector releases page](https://github.com/Iranman/DaCollector/releases).
2. Download `DaCollector.Setup.exe` when it is available.
3. Run the installer as an administrator.
4. Keep the firewall option selected if you want to open DaCollector from another device on your LAN.
5. Launch DaCollector from the Start menu or the installer finish screen.
6. Open the Web UI at `http://127.0.0.1:38111/webui`.

The installer places the application under `C:\Program Files\DaCollector\DaCollector` and stores server data under `C:\ProgramData\DaCollector` by default.

## Option 2: Run the Standalone ZIP

Use this path when you want to test without installing.

1. Download `DaCollector.TrayService_Standalone_win-x64.zip` from a release.
2. Extract it to a writable folder, for example `C:\DaCollector`.
3. Start `DaCollector.exe`.
4. Open `http://127.0.0.1:38111/webui`.

If Windows SmartScreen blocks the first launch, choose the advanced details option and allow the app only if you trust the artifact source.

## Option 3: Build from Source

From the repository root:

```powershell
dotnet restore DaCollector.sln
dotnet publish DaCollector.TrayService\DaCollector.TrayService.csproj -c Release -r win-x64 --self-contained true
```

Run the published app:

```powershell
cd DaCollector.Server\bin\Release\net10.0\win-x64\publish
.\DaCollector.exe
```

Then open `http://127.0.0.1:38111/webui`.

After first startup, run the [install verification checklist](../verify-install.md).

## Framework-Dependent Build

Use this only when the target machine already has the required .NET runtimes:

```powershell
dotnet publish DaCollector.TrayService\DaCollector.TrayService.csproj -c Release -r win-x64 --no-self-contained
```

Install these runtime families on the target machine:

- .NET 10 Desktop Runtime.
- ASP.NET Core Runtime 10.

## Build the Installer Locally

The installer is generated with Inno Setup. Install Inno Setup and make sure `iscc.exe` is on `PATH`, then publish a Windows build and run:

```powershell
iscc /O".\" ".\Installer\DaCollector.iss"
```

This creates `DaCollector.Setup.exe` in the repository root.

## Open the Firewall Manually

The installer can create the firewall rule for you. If you run from a ZIP or source build and want LAN access, open the port manually from an elevated PowerShell:

```powershell
netsh advfirewall firewall add rule name="DaCollector" dir=in action=allow protocol=TCP localport=38111
```

To remove the rule:

```powershell
netsh advfirewall firewall delete rule name="DaCollector" protocol=TCP localport=38111
```

## Uninstall

If you used the installer, uninstall from Windows Settings. If you used a ZIP or source publish, stop DaCollector and delete the extracted application folder.

Application data is stored separately. Back up or remove `C:\ProgramData\DaCollector` only when you intentionally want to preserve or reset the local database, settings, logs, cached metadata, and Web UI data.
