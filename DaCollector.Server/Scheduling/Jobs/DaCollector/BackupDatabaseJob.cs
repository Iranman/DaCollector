using System.Collections.Generic;
using System.Threading.Tasks;
using DaCollector.Server.Scheduling.Acquisition.Attributes;
using DaCollector.Server.Scheduling.Attributes;
using DaCollector.Server.Scheduling.Concurrency;
using DaCollector.Server.Services;

#pragma warning disable CS8618
#nullable enable
namespace DaCollector.Server.Scheduling.Jobs.DaCollector;

[DatabaseRequired]
[LimitConcurrency(1, 1)]
[JobKeyGroup(JobKeyGroup.Import)]
public class BackupDatabaseJob : BaseJob
{
    private readonly DatabaseBackupService _backupService;

    public override string TypeName => "Backup Database";

    public override string Title => "Backing Up Database";

    public override Dictionary<string, object> Details => new();

    public override Task Process()
    {
        _backupService.RunBackup();
        return Task.CompletedTask;
    }

    public BackupDatabaseJob(DatabaseBackupService backupService)
    {
        _backupService = backupService;
    }

    protected BackupDatabaseJob() { }
}
