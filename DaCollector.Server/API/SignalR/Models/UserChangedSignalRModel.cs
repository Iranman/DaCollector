using DaCollector.Abstractions.User.Events;

namespace DaCollector.Server.API.SignalR.Models;

public class UserChangedSignalRModel(UserChangedEventArgs args)
{
    /// <summary>
    /// The ID of the folder.
    /// </summary>
    public int UserID { get; } = args.User.ID;
}
