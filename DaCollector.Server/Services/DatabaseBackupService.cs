using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using DaCollector.Server.Databases;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.Services;

public class DatabaseBackupService(
    ILogger<DatabaseBackupService> logger,
    DatabaseFactory databaseFactory,
    ISettingsProvider settingsProvider
)
{
    public DatabaseBackupResult RunBackup()
    {
        var db = databaseFactory.Instance;
        var name = db.GetScheduledBackupName();

        try
        {
            db.BackupDatabase(name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database backup failed: {Path}", name);
            return new DatabaseBackupResult { Success = false, Message = ex.Message };
        }

        // The backup file has an extension appended by BackupDatabase (e.g. .db3, .bak).
        // Find the file that was just created by looking for the name stem.
        var dir = db.GetBackupDirectory();
        var created = Directory.GetFiles(dir)
            .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == Path.GetFileName(name));

        logger.LogInformation("Database backup created: {Path}", created ?? name);
        ApplyRetention(db, dir);

        return new DatabaseBackupResult
        {
            Success = true,
            FileName = created is not null ? Path.GetFileName(created) : null,
            Message = "Backup completed successfully.",
        };
    }

    public IReadOnlyList<DatabaseBackupFile> GetBackupFiles()
    {
        var dir = databaseFactory.Instance.GetBackupDirectory();
        if (!Directory.Exists(dir))
            return [];

        return Directory.GetFiles(dir)
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new DatabaseBackupFile
                {
                    FileName = info.Name,
                    SizeBytes = info.Length,
                    CreatedAt = info.CreationTimeUtc,
                };
            })
            .OrderByDescending(f => f.CreatedAt)
            .ToList();
    }

    public bool DeleteBackup(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains('/') || fileName.Contains('\\'))
            return false;

        var dir = databaseFactory.Instance.GetBackupDirectory();
        var path = Path.Combine(dir, fileName);

        if (!File.Exists(path))
            return false;

        try
        {
            File.Delete(path);
            logger.LogInformation("Deleted database backup: {FileName}", fileName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete database backup: {FileName}", fileName);
            return false;
        }
    }

    private void ApplyRetention(IDatabase db, string dir)
    {
        var retention = settingsProvider.GetSettings().Database.BackupRetentionCount;
        if (retention <= 0 || !Directory.Exists(dir))
            return;

        var schema = settingsProvider.GetSettings().Database.Schema;
        var scheduledFiles = Directory.GetFiles(dir)
            .Where(f => Path.GetFileNameWithoutExtension(f).StartsWith($"{schema}_scheduled_", StringComparison.OrdinalIgnoreCase))
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTimeUtc)
            .ToList();

        foreach (var old in scheduledFiles.Skip(retention))
        {
            try
            {
                old.Delete();
                logger.LogDebug("Retention: deleted old backup {FileName}", old.Name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Retention: could not delete {FileName}", old.Name);
            }
        }
    }
}

public sealed class DatabaseBackupResult
{
    public bool Success { get; init; }
    public string? FileName { get; init; }
    public string? Message { get; init; }
}

public sealed class DatabaseBackupFile
{
    public string FileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTime CreatedAt { get; init; }
}
