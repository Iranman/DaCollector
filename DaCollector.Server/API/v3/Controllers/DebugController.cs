using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Quartz;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.Scheduling;
using DaCollector.Server.Scheduling.Jobs.Test;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.API.v3.Controllers;

#if DEBUG

/// <summary>
/// A controller with endpoints that should only be used while debugging.
/// Not for general use.
/// </summary>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize("admin")]
public class DebugController : BaseController
{
    private readonly ILogger<DebugController> _logger;

    private readonly ISchedulerFactory _schedulerFactory;

    public DebugController(ILogger<DebugController> logger, ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory) : base(settingsProvider)
    {
        _logger = logger;
        _schedulerFactory = schedulerFactory;
    }

    /// <summary>
    /// Schedule {<paramref name="count"/>} jobs that just wait for 60 seconds
    /// </summary>
    /// <param name="count"></param>
    /// <param name="seconds"></param>
    [HttpGet("ScheduleJobs/Delay/{count}")]
    public async Task<ActionResult> ScheduleTestJobs(int count, [FromQuery] int seconds = 60)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        for (var i = 0; i < count; i++)
            await scheduler.StartJob<TestDelayJob>(t => (t.DelaySeconds, t.Offset) = (seconds, i), prioritize: true).ConfigureAwait(false);

        return Ok();
    }

    /// <summary>
    /// Schedule {<paramref name="count"/>} jobs that just error
    /// </summary>
    /// <param name="count"></param>
    [HttpGet("ScheduleJobs/Error/{count}")]
    public async Task<ActionResult> ScheduleTestErrorJobs(int count)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        for (var i = 0; i < count; i++)
            await scheduler.StartJob<TestErrorJob>(t => t.Offset = i, prioritize: true).ConfigureAwait(false);

        return Ok();
    }

}

#endif
