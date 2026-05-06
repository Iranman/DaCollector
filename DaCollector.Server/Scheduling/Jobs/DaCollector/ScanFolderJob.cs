using System.Collections.Generic;
using System.Threading.Tasks;
using DaCollector.Abstractions.Video.Services;
using DaCollector.Server.Repositories;
using DaCollector.Server.Scheduling.Acquisition.Attributes;
using DaCollector.Server.Scheduling.Attributes;

namespace DaCollector.Server.Scheduling.Jobs.DaCollector;

[DatabaseRequired]
[JobKeyMember("ScanFolder")]
[JobKeyGroup(JobKeyGroup.Import)]
internal class ScanFolderJob : BaseJob
{
    private readonly IVideoService _videoService;

    private string _managedFolder;

    [JobKeyMember]
    public int ManagedFolderID { get; set; }

    public string RelativePath { get; set; } = string.Empty;

    public bool OnlyNewFiles { get; set; }

    public bool SkipMyList { get; set; }

    public bool CleanUpStructure { get; set; }

    public bool CheckFileSize { get; set; }

    public override string TypeName => "Scan Managed Folder";

    public override string Title => "Scanning Managed Folder";

    public override Dictionary<string, object> Details
    {
        get
        {
            var details = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(_managedFolder))
                details["Managed Folder"] = _managedFolder;
            details["Managed Folder ID"] = ManagedFolderID;
            if (!string.IsNullOrEmpty(RelativePath)) details["Relative Path"] = RelativePath;
            if (OnlyNewFiles) details["Only New Files"] = true;
            if (!SkipMyList) details["Add to MyList"] = true;
            if (CleanUpStructure) details["Clean Up"] = true;
            return details;
        }
    }

    public override void PostInit()
    {
        _managedFolder = RepoFactory.DaCollectorManagedFolder?.GetByID(ManagedFolderID)?.Name;
    }

    public override async Task Process()
    {
        var managedFolder = _videoService.GetManagedFolderByID(ManagedFolderID);
        if (managedFolder == null)
            return;

        await _videoService.ScanManagedFolder(managedFolder, relativePath: RelativePath, onlyNewFiles: OnlyNewFiles, skipMylist: SkipMyList, cleanUpStructure: CleanUpStructure, checkFileSize: CheckFileSize);
    }

    public ScanFolderJob(IVideoService videoService)
    {
        _videoService = videoService;
    }

    protected ScanFolderJob() { }
}
