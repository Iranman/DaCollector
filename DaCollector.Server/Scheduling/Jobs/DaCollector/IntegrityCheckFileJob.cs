using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DaCollector.Abstractions.Video.Hashing;
using DaCollector.Server.Hashing;
using DaCollector.Server.Models.Legacy;
using DaCollector.Server.Repositories;
using DaCollector.Server.Scheduling.Attributes;
using DaCollector.Server.Scheduling.Acquisition.Attributes;
using DaCollector.Server.Scheduling.Concurrency;
using DaCollector.Server.Server;

#pragma warning disable CS8618
#nullable enable
namespace DaCollector.Server.Scheduling.Jobs.DaCollector;

[DatabaseRequired]
[LimitConcurrency(2)]
[JobKeyGroup(JobKeyGroup.Import)]
public class IntegrityCheckFileJob : BaseJob
{
    private readonly CoreHashProvider _hashProvider;

    public int ScanFileID { get; set; }
    public bool CheckHash { get; set; }

    public override string TypeName => "Integrity Check File";
    public override string Title => "Checking File Integrity";
    public override Dictionary<string, object> Details => new() { { "ScanFile ID", ScanFileID } };

    protected IntegrityCheckFileJob() { }

    public IntegrityCheckFileJob(CoreHashProvider hashProvider)
    {
        _hashProvider = hashProvider;
    }

    public override async Task Process()
    {
        var scanFile = RepoFactory.ScanFile.GetByID(ScanFileID);
        if (scanFile is null) return;

        try
        {
            // Existence check
            if (!File.Exists(scanFile.FullName))
            {
                FinalizeScanFile(scanFile, ScanFileStatus.ErrorFileNotFound);
                return;
            }

            // Size check
            var actualSize = new FileInfo(scanFile.FullName).Length;
            if (actualSize != scanFile.FileSize)
            {
                FinalizeScanFile(scanFile, ScanFileStatus.ErrorInvalidSize);
                return;
            }

            // Hash check (optional — slow, reads full file)
            if (CheckHash)
            {
                if (string.IsNullOrEmpty(scanFile.Hash))
                {
                    FinalizeScanFile(scanFile, ScanFileStatus.ErrorMissingHash);
                    return;
                }

                var hashes = await _hashProvider.GetHashesForVideo(new HashingRequest
                {
                    File = new FileInfo(scanFile.FullName),
                    EnabledHashTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ED2K" },
                });
                var ed2k = hashes.FirstOrDefault(h => string.Equals(h.Type, "ED2K", StringComparison.OrdinalIgnoreCase))?.Value;
                if (!string.Equals(ed2k, scanFile.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    scanFile.HashResult = ed2k ?? string.Empty;
                    FinalizeScanFile(scanFile, ScanFileStatus.ErrorInvalidHash);
                    return;
                }
            }

            FinalizeScanFile(scanFile, ScanFileStatus.ProcessedOK);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "IO error while checking integrity of {File}", scanFile.FullName);
            FinalizeScanFile(scanFile, ScanFileStatus.ErrorIOError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking integrity of {File}", scanFile.FullName);
        }
    }

    private void FinalizeScanFile(ScanFile scanFile, ScanFileStatus status)
    {
        scanFile.Status = status;
        scanFile.CheckDate = DateTime.Now;
        RepoFactory.ScanFile.Save(scanFile);

        var remaining = RepoFactory.ScanFile.GetWaitingCount(scanFile.ScanID);
        if (remaining > 0) return;

        var scan = RepoFactory.Scan.GetByID(scanFile.ScanID);
        if (scan is null) return;
        scan.Status = ScanStatus.Finished;
        RepoFactory.Scan.Save(scan);
        _logger.LogInformation("Integrity check scan #{ScanID} completed", scan.ScanID);
    }
}
