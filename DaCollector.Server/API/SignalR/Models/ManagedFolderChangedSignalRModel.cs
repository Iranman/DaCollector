
using DaCollector.Abstractions.Video.Events;

namespace DaCollector.Server.API.SignalR.Models;

public class ManagedFolderChangedSignalRModel(ManagedFolderChangedEventArgs args)
{
    /// <summary>
    /// The ID of the folder.
    /// </summary>
    public int FolderID { get; } = args.Folder.ID;
}
