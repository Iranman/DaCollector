using System.Collections.Generic;
using System.IO;

namespace DaCollector.Server.MediaInfo.Subtitles;

public interface ISubtitles
{
    List<TextStream> GetStreams(FileInfo file);

    bool IsSubtitleFile(string path);
}
