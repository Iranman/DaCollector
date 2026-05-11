using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

#nullable enable
namespace DaCollector.Server.Media;

public sealed record NfoSidecarData(string? ImdbId, int? RuntimeMinutes, int? Year);

public static class NfoSidecarParser
{
    public static NfoSidecarData? TryParse(string? videoFilePath)
    {
        if (string.IsNullOrWhiteSpace(videoFilePath))
            return null;

        var nfoPath = Path.ChangeExtension(videoFilePath, ".nfo");
        if (!File.Exists(nfoPath))
            return null;

        try
        {
            var root = XDocument.Load(nfoPath).Root;
            if (root is null)
                return null;

            // Kodi-style: <uniqueid type="imdb">tt0133093</uniqueid>
            var imdbId = root.Elements("uniqueid")
                .FirstOrDefault(e => string.Equals(e.Attribute("type")?.Value, "imdb", StringComparison.OrdinalIgnoreCase))
                ?.Value?.Trim();

            // Older Kodi/MediaElch: <id>tt0133093</id>
            if (string.IsNullOrWhiteSpace(imdbId))
                imdbId = root.Element("id")?.Value?.Trim();

            // Only keep well-formed IMDb IDs (tt followed by digits)
            if (!string.IsNullOrWhiteSpace(imdbId) && !imdbId.StartsWith("tt", StringComparison.OrdinalIgnoreCase))
                imdbId = null;

            int? runtimeMinutes = null;
            if (int.TryParse(root.Element("runtime")?.Value?.Trim(), out var rt) && rt > 0)
                runtimeMinutes = rt;

            int? year = null;
            if (int.TryParse(root.Element("year")?.Value?.Trim(), out var y) && y is >= 1880 and <= 2100)
                year = y;

            return imdbId is null && runtimeMinutes is null && year is null
                ? null
                : new NfoSidecarData(imdbId, runtimeMinutes, year);
        }
        catch
        {
            return null;
        }
    }
}
