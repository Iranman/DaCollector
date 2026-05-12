using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.Scheduling;
using DaCollector.Server.Scheduling.Jobs.DaCollector;
using DaCollector.Server.Services;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize("admin")]
public class DatabaseController(
    ISettingsProvider settingsProvider,
    DatabaseBackupService backupService,
    ISchedulerFactory schedulerFactory
) : BaseController(settingsProvider)
{
    /// <summary>
    /// List all database backup files in the configured backup directory.
    /// </summary>
    [HttpGet("Backups")]
    public ActionResult<IReadOnlyList<DatabaseBackupFile>> GetBackups()
        => Ok(backupService.GetBackupFiles());

    /// <summary>
    /// Trigger an immediate database backup.
    /// The backup runs synchronously for the response, and also schedules the next periodic backup.
    /// </summary>
    [HttpPost("Backups")]
    public ActionResult<DatabaseBackupResult> CreateBackup()
    {
        var result = backupService.RunBackup();
        return result.Success ? Ok(result) : StatusCode(500, result);
    }

    /// <summary>
    /// Queue an immediate database backup as a background job.
    /// </summary>
    [HttpPost("Backups/Queue")]
    public async Task<ActionResult> QueueBackup()
    {
        var scheduler = await schedulerFactory.GetScheduler();
        await scheduler.StartJob<BackupDatabaseJob>();
        return Ok();
    }

    /// <summary>
    /// Delete a backup file by name. Only the filename is accepted — no path separators.
    /// </summary>
    /// <param name="fileName">Exact backup filename (e.g. <c>DaCollector_scheduled_20260511_143022.db3</c>).</param>
    [HttpDelete("Backups/{fileName}")]
    public ActionResult DeleteBackup([FromRoute, Required] string fileName)
    {
        if (!backupService.DeleteBackup(fileName))
            return NotFound();
        return NoContent();
    }
}
