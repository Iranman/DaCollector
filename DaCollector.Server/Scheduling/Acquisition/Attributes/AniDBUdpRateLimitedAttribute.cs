using System;

namespace DaCollector.Server.Scheduling.Acquisition.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class AniDBUdpRateLimitedAttribute : NetworkRequiredAttribute { }
