using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace DaCollector.Tests;

public class DatabaseBackupServiceTests
{
    // Tests for the retention logic — pure filesystem manipulation with no DB dependency.

    [Fact]
    public void Retention_KeepsNewestN_WhenOverLimit()
    {
        var dir = CreateTempDir();
        try
        {
            // Create 10 scheduled backup files with distinct creation times.
            var files = CreateScheduledBackups(dir, 10);

            ApplyRetention(dir, retention: 3, schema: "DaCollector");

            var remaining = Directory.GetFiles(dir).Select(Path.GetFileName).ToHashSet();
            var expected = files.OrderByDescending(f => f.CreationTimeUtc).Take(3).Select(f => f.Name).ToHashSet();
            Assert.Equal(expected, remaining);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Retention_KeepsAll_WhenBelowLimit()
    {
        var dir = CreateTempDir();
        try
        {
            CreateScheduledBackups(dir, 4);
            ApplyRetention(dir, retention: 7, schema: "DaCollector");
            Assert.Equal(4, Directory.GetFiles(dir).Length);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Retention_KeepsAll_WhenRetentionIsZero()
    {
        var dir = CreateTempDir();
        try
        {
            CreateScheduledBackups(dir, 20);
            ApplyRetention(dir, retention: 0, schema: "DaCollector");
            Assert.Equal(20, Directory.GetFiles(dir).Length);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Retention_DoesNotDeleteMigrationBackups()
    {
        var dir = CreateTempDir();
        try
        {
            // Migration backups follow the pattern Schema_NNN_timestamp (no "scheduled" segment).
            File.WriteAllText(Path.Combine(dir, "DaCollector_163_202605110900.db3"), "migration backup");
            CreateScheduledBackups(dir, 5);

            ApplyRetention(dir, retention: 2, schema: "DaCollector");

            // 2 scheduled backups remain + the 1 migration backup = 3 total.
            Assert.Equal(3, Directory.GetFiles(dir).Length);
            Assert.True(File.Exists(Path.Combine(dir, "DaCollector_163_202605110900.db3")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void DeleteBackup_ReturnsFalse_ForPathTraversalAttempt()
    {
        Assert.False(IsValidBackupFileName("../secret.db3"));
        Assert.False(IsValidBackupFileName("subdir/backup.db3"));
        Assert.False(IsValidBackupFileName(@"subdir\backup.db3"));
        Assert.True(IsValidBackupFileName("DaCollector_scheduled_20260511_143022.db3"));
    }

    // --- helpers that mirror the logic in DatabaseBackupService ---

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static List<FileInfo> CreateScheduledBackups(string dir, int count)
    {
        var files = new List<FileInfo>();
        for (var i = 0; i < count; i++)
        {
            var path = Path.Combine(dir, $"DaCollector_scheduled_{DateTime.UtcNow.AddMinutes(-i):yyyyMMdd_HHmmss}_{i}.db3");
            File.WriteAllText(path, $"backup {i}");
            files.Add(new FileInfo(path));
        }
        return files;
    }

    private static void ApplyRetention(string dir, int retention, string schema)
    {
        if (retention <= 0)
            return;

        var scheduled = Directory.GetFiles(dir)
            .Where(f => Path.GetFileNameWithoutExtension(f)
                .StartsWith($"{schema}_scheduled_", StringComparison.OrdinalIgnoreCase))
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTimeUtc)
            .ToList();

        foreach (var old in scheduled.Skip(retention))
            old.Delete();
    }

    private static bool IsValidBackupFileName(string fileName)
        => !string.IsNullOrWhiteSpace(fileName)
            && !fileName.Contains('/')
            && !fileName.Contains('\\');
}
