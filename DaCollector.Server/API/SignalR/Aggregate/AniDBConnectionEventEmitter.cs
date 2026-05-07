using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;
using DaCollector.Server.API.SignalR.Models;
using DaCollector.Server.Providers.AniDB;
using DaCollector.Server.Providers.AniDB.Interfaces;

namespace DaCollector.Server.API.SignalR.Aggregate;

public class AniDBConnectionEventEmitter : BaseEventEmitter, IDisposable
{
    private IUDPConnectionHandler UDPHandler { get; set; }
    private IHttpConnectionHandler HttpHandler { get; set; }

    public AniDBConnectionEventEmitter(IHubContext<AggregateHub> hub, IUDPConnectionHandler udp, IHttpConnectionHandler http) : base(hub)
    {
        HttpHandler = http;
        UDPHandler = udp;
        UDPHandler.AniDBStateUpdate += OnUDPStateUpdate;
        HttpHandler.AniDBStateUpdate += OnHttpStateUpdate;
    }

    public void Dispose()
    {
        UDPHandler.AniDBStateUpdate -= OnUDPStateUpdate;
        HttpHandler.AniDBStateUpdate -= OnHttpStateUpdate;
    }

    private async void OnUDPStateUpdate(object sender, AniDBStateUpdate e)
    {
        await SendAsync("udp.stateUpdate", new AniDBStatusUpdateSignalRModel(e));
    }

    private async void OnHttpStateUpdate(object sender, AniDBStateUpdate e)
    {
        await SendAsync("http.stateUpdate", new AniDBStatusUpdateSignalRModel(e));
    }

    protected override object[] GetInitialMessages()
    {
        return [
            new List<AniDBStatusUpdateSignalRModel>
            {
                new(UDPHandler.State),
                new(HttpHandler.State),
            },
        ];
    }
}
