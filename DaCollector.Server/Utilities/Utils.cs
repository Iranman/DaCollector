using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DaCollector.Abstractions.Utilities;
using DaCollector.Server.Server;
using DaCollector.Server.Settings;

namespace DaCollector.Server.Utilities;

public static partial class Utils
{
    // Legacy static accessors — intentional for database initialization, NHibernate type converters,
    // and OS-level file-watcher callbacks where constructor DI is unavailable. Do not add new usages.
    public static IServiceProvider ServiceContainer { get; set; }

    public static ISettingsProvider SettingsProvider { get; set; }

    private static string _applicationPath = null;

    public static string ApplicationPath
    {
        get
        {
            if (_applicationPath != null)
                return _applicationPath;

            var dacollectorHome = Environment.GetEnvironmentVariable("DACOLLECTOR_HOME");
            if (!string.IsNullOrWhiteSpace(dacollectorHome))
                return _applicationPath = Path.GetFullPath(dacollectorHome);

            if (!PlatformUtility.IsWindows)
                return _applicationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dacollector",
                    DefaultInstance);

            return _applicationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                DefaultInstance);
        }
    }

    public static string DefaultInstance { get; set; } = Assembly.GetEntryAssembly().GetName().Name;

    public static string DefaultImagePath => Path.Combine(ApplicationPath, "images");

    public static string AnimeXmlDirectory { get; set; } = Path.Combine(ApplicationPath, "Anime_HTTP");

    public static string MyListDirectory { get; set; } = Path.Combine(ApplicationPath, "MyList");

    public static string GetDistinctPath(string fullPath)
    {
        var parent = Path.GetDirectoryName(fullPath);
        return string.IsNullOrEmpty(parent) ? fullPath : Path.Combine(Path.GetFileName(parent), Path.GetFileName(fullPath));
    }

    private static string GetInstanceFromCommandLineArguments()
    {
        const int NotFound = -1;
        var args = Environment.GetCommandLineArgs();
        var idx = Array.FindIndex(args, x => string.Equals(x, "instance", StringComparison.InvariantCultureIgnoreCase));
        if (idx is NotFound)
            return null;
        if (idx >= args.Length - 1)
            return null;
        return args[idx + 1];
    }

    public static void SetInstance()
    {
        var instance = GetInstanceFromCommandLineArguments();
        if (string.IsNullOrWhiteSpace(instance) is false)
            DefaultInstance = instance;
    }

    public static int GetScheduledHours(ScheduledUpdateFrequency freq)
    {
        return freq switch
        {
            ScheduledUpdateFrequency.HoursSix => 6,
            ScheduledUpdateFrequency.HoursTwelve => 12,
            ScheduledUpdateFrequency.Daily => 24,
            ScheduledUpdateFrequency.WeekOne => 24 * 7,
            ScheduledUpdateFrequency.MonthOne => 24 * 30,
            _ => int.MaxValue,
        };
    }

    public static bool IsVideo(string fileName)
        => SettingsProvider.GetSettings().Import.VideoExtensions.Any(extName => fileName.EndsWith(extName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Determines an encoded string's encoding by analyzing its byte order mark (BOM).
    /// Defaults to ASCII when detection of the text file's endianness fails.
    /// </summary>
    /// <param name="data">Byte array of the encoded string</param>
    /// <returns>The detected encoding.</returns>
    public static Encoding GetEncoding(byte[] data)
    {
        if (data.Length < 4)
        {
            return Encoding.ASCII;
        }
        // Analyze the BOM
#pragma warning disable SYSLIB0001
        if (data[0] == 0x2b && data[1] == 0x2f && data[2] == 0x76)
        {
            return Encoding.UTF7;
        }

        if (data[0] == 0xef && data[1] == 0xbb && data[2] == 0xbf)
        {
            return Encoding.UTF8;
        }

        if (data[0] == 0xff && data[1] == 0xfe)
        {
            return Encoding.Unicode; //UTF-16LE
        }

        if (data[0] == 0xfe && data[1] == 0xff)
        {
            return Encoding.BigEndianUnicode; //UTF-16BE
        }

        if (data[0] == 0 && data[1] == 0 && data[2] == 0xfe && data[3] == 0xff)
        {
            return Encoding.UTF32;
        }

        return Encoding.ASCII;
#pragma warning restore SYSLIB0001
    }
}
