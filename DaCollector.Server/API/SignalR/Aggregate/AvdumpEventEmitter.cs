using System;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using DaCollector.Abstractions.Metadata.Anidb.Events;
using DaCollector.Server.API.SignalR.Models;
using DaCollector.Server.Utilities;

namespace DaCollector.Server.API.SignalR.Aggregate;

public class AvdumpEventEmitter : BaseEventEmitter, IDisposable
{
    public AvdumpEventEmitter(IHubContext<AggregateHub> hub) : base(hub)
    {
        DaCollectorEventHandler.Instance.AvdumpEvent += OnAVDumpEvent;
    }

    public void Dispose()
    {
        DaCollectorEventHandler.Instance.AvdumpEvent -= OnAVDumpEvent;
    }

    private async void OnAVDumpEvent(object sender, AnidbAvdumpEventArgs eventArgs)
    {
        await SendAsync("event", new AvdumpEventSignalRModel(eventArgs));
    }

    protected override object[] GetInitialMessages()
    {
        return [
            AVDumpHelper.GetActiveSessions()
                .Select(session => new AvdumpEventSignalRModel(session))
                .ToList()
        ];
    }
}
