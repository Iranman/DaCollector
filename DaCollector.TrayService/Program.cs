using System;
using Avalonia;
using Sentry;
using DaCollector.Server.Server;

namespace DaCollector.TrayService;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Initialize Sentry as early as possible to capture pre-host crashes.
        // The opt-out check in UseSentryConfig covers the main ASP.NET Core runtime.
        using var sentry = Constants.SentryDsn.StartsWith("https://")
            ? SentrySdk.Init(options =>
            {
                options.Dsn = Constants.SentryDsn;
                options.AutoSessionTracking = true;
            })
            : null;

        try
        {
            UnhandledExceptionManager.AddHandler();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}
