using System;

namespace DaCollector.Server.Services.ErrorHandling;

[AttributeUsage(AttributeTargets.Class)]
public class SentryIncludeAttribute : Attribute { }
