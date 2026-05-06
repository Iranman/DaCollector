using System;
using System.Collections.Generic;
using System.Linq;
using Quartz;
using Quartz.Util;
using DaCollector.Abstractions.Connectivity.Enums;
using DaCollector.Abstractions.Connectivity.Services;
using DaCollector.Server.Scheduling.Acquisition.Attributes;

#nullable enable
namespace DaCollector.Server.Scheduling.Acquisition.Filters;

public class NetworkRequiredAcquisitionFilter : IAcquisitionFilter
{
    private readonly Type[] _types;
    private readonly IConnectivityService _connectivityService;

    public NetworkRequiredAcquisitionFilter(IConnectivityService connectivityService)
    {
        _connectivityService = connectivityService;
        _connectivityService.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        _types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Where(a =>
            typeof(IJob).IsAssignableFrom(a) && !a.IsAbstract && ObjectUtils.IsAttributePresent(a, typeof(NetworkRequiredAttribute))).ToArray();
    }

    ~NetworkRequiredAcquisitionFilter()
    {
        _connectivityService.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
    }

    private void OnNetworkAvailabilityChanged(object? sender, EventArgs e)
    {
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    public IEnumerable<Type> GetTypesToExclude() =>
        _connectivityService.NetworkAvailability >= NetworkAvailability.PartialInternet
            ? []
            : _types;

    public event EventHandler? StateChanged;
}
