# Updating

DaCollector stores application binaries and server data separately. Update the binaries without deleting the data directory unless you intentionally want a clean instance.

## Installer Updates

1. Stop DaCollector.
2. Back up `C:\ProgramData\DaCollector`.
3. Download the newer `DaCollector.Setup.exe`.
4. Run the installer.
5. Start DaCollector.
6. Open `http://127.0.0.1:38111/webui` and confirm the server loads.

The installer is configured to uninstall the previous installed app before installing the new files.

## ZIP Updates

1. Stop DaCollector.
2. Back up the data directory.
3. Extract the new ZIP to a new application folder.
4. Start the new `DaCollector.exe`.
5. Confirm it uses the expected data directory.

If you run multiple ZIP builds side by side, set `DACOLLECTOR_HOME` explicitly for each instance.

## Rollback

1. Stop DaCollector.
2. Restore the previous application folder or reinstall the previous installer.
3. Restore the backed-up data directory if the newer version performed database migrations you do not want to keep.
4. Start DaCollector.

Always keep a data backup before testing a build from a different branch.
