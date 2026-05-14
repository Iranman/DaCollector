using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using DaCollector.Abstractions.Extensions;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.API.v3.Models.DaCollector;
using DaCollector.Server.Models.Legacy;
using DaCollector.Server.Repositories;
using DaCollector.Server.Scheduling;
using DaCollector.Server.Scheduling.Jobs.DaCollector;
using DaCollector.Server.Server;
using DaCollector.Server.Settings;

namespace DaCollector.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize("admin")]
public class IntegrityCheckController : BaseController
{
    private readonly ISchedulerFactory _schedulerFactory;

    [HttpGet]
    public ActionResult<List<IntegrityCheck>> GetAllScans()
        => RepoFactory.Scan.GetAll().Select(ToDto).ToList();

    [HttpGet("{scanID}")]
    public ActionResult<IntegrityCheck> GetScan(int scanID)
    {
        var scan = RepoFactory.Scan.GetByID(scanID);
        if (scan is null) return NotFound();
        return ToDto(scan);
    }

    [HttpGet("{scanID}/File")]
    public ActionResult<List<IntegrityCheckFile>> GetScanFiles(int scanID, [FromQuery] ScanFileStatus? status = null)
    {
        var scan = RepoFactory.Scan.GetByID(scanID);
        if (scan is null) return NotFound();

        var files = RepoFactory.ScanFile.GetByScanID(scanID);
        if (status.HasValue)
            files = files.Where(f => f.Status == status.Value).ToList();

        return files.Select(ToFileDto).ToList();
    }

    [HttpPost]
    public ActionResult<IntegrityCheck> AddScan(IntegrityCheck check)
    {
        var scan = check.ID is > 0 ? RepoFactory.Scan.GetByID(check.ID) : new Scan
        {
            Status = check.Status,
            ImportFolders = check.ManagedFolderIDs.Select(a => a.ToString()).Join(','),
            CreationTIme = DateTime.Now,
        };
        if (scan.ScanID == 0)
            RepoFactory.Scan.Save(scan);

        var files = scan.ImportFolders.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .SelectMany(RepoFactory.VideoLocalPlace.GetByManagedFolderID)
            .Select(p => new { p, v = p.VideoLocal })
            .Select(t => new ScanFile
            {
                Hash = t.v.Hash,
                FileSize = t.v.FileSize,
                FullName = t.p.Path,
                ScanID = scan.ScanID,
                Status = ScanFileStatus.Waiting,
                ImportFolderID = t.p.ManagedFolderID,
                VideoLocal_Place_ID = t.p.ID
            }).ToList();
        RepoFactory.ScanFile.Save(files);

        return ToDto(scan);
    }

    [HttpPost("{scanID}/Start")]
    public async Task<ActionResult> StartScan(int scanID, [FromQuery] bool checkHash = false)
    {
        var scan = RepoFactory.Scan.GetByID(scanID);
        if (scan is null) return NotFound();
        if (scan.Status == ScanStatus.Running) return BadRequest("Scan is already running.");
        if (scan.Status == ScanStatus.Finished) return BadRequest("Scan already finished; delete and recreate to re-run.");

        scan.Status = ScanStatus.Running;
        RepoFactory.Scan.Save(scan);

        var waiting = RepoFactory.ScanFile.GetWaiting(scanID);
        if (waiting.Count == 0)
        {
            scan.Status = ScanStatus.Finished;
            RepoFactory.Scan.Save(scan);
            return Ok();
        }

        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var f in waiting)
            await scheduler.StartJob<IntegrityCheckFileJob>(c => { c.ScanFileID = f.ScanFileID; c.CheckHash = checkHash; });

        return Ok();
    }

    [HttpDelete("{scanID}")]
    public ActionResult DeleteScan(int scanID)
    {
        var scan = RepoFactory.Scan.GetByID(scanID);
        if (scan is null) return NotFound();

        RepoFactory.ScanFile.Delete(RepoFactory.ScanFile.GetByScanID(scanID));
        RepoFactory.Scan.Delete(scan);
        return Ok();
    }

    private IntegrityCheck ToDto(Scan scan)
    {
        var total = RepoFactory.ScanFile.GetByScanID(scan.ScanID).Count;
        var waiting = RepoFactory.ScanFile.GetWaitingCount(scan.ScanID);
        var errors = RepoFactory.ScanFile.GetWithError(scan.ScanID).Count;
        return new IntegrityCheck
        {
            ID = scan.ScanID,
            ManagedFolderIDs = scan.ImportFolders
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToList(),
            Status = scan.Status,
            CreatedAt = scan.CreationTIme,
            TotalFiles = total,
            WaitingFiles = waiting,
            ErrorFiles = errors,
            CompletedFiles = total - waiting - errors,
        };
    }

    private static IntegrityCheckFile ToFileDto(ScanFile f) => new()
    {
        ID = f.ScanFileID,
        ScanID = f.ScanID,
        ManagedFolderID = f.ImportFolderID,
        VideoLocalPlaceID = f.VideoLocal_Place_ID,
        FullName = f.FullName,
        FileSize = f.FileSize,
        Status = f.Status,
        CheckDate = f.CheckDate == default ? null : f.CheckDate,
        Hash = f.Hash,
        HashResult = f.HashResult,
    };

    public IntegrityCheckController(ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory) : base(settingsProvider)
    {
        _schedulerFactory = schedulerFactory;
    }
}
