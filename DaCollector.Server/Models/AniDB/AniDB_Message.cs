using System;
using DaCollector.Server.Server;

# nullable enable
namespace DaCollector.Server.Models.AniDB;

public class AniDB_Message
{
    #region DB Columns

    public int AniDB_MessageID { get; set; }

    public int MessageID { get; set; }

    public int FromUserId { get; set; }

    public string FromUserName { get; set; } = string.Empty;

    public DateTime SentAt { get; set; }

    public DateTime FetchedAt { get; set; }

    public AniDBMessageType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public AniDBMessageFlags Flags { get; set; }

    #endregion

    #region Flags

    public bool IsReadOnAniDB
    {
        get
        {
            return Flags.HasFlag(AniDBMessageFlags.ReadOnAniDB);
        }
        set
        {
            if (value)
                Flags |= AniDBMessageFlags.ReadOnAniDB;
            else
                Flags &= ~AniDBMessageFlags.ReadOnAniDB;
        }
    }

    public bool IsReadOnDaCollector
    {
        get
        {
            return Flags.HasFlag(AniDBMessageFlags.ReadOnDaCollector);
        }
        set
        {
            if (value)
                Flags |= AniDBMessageFlags.ReadOnDaCollector;
            else
                Flags &= ~AniDBMessageFlags.ReadOnDaCollector;
        }
    }

    public bool IsFileMoved
    {
        get
        {
            return Flags.HasFlag(AniDBMessageFlags.FileMoved);
        }
        set
        {
            if (value)
                Flags |= AniDBMessageFlags.FileMoved;
            else
                Flags &= ~AniDBMessageFlags.FileMoved;
        }
    }

    public bool IsFileMoveHandled
    {
        get
        {
            return Flags.HasFlag(AniDBMessageFlags.FileMoveHandled);
        }
        set
        {
            if (value)
                Flags |= AniDBMessageFlags.FileMoveHandled;
            else
                Flags &= ~AniDBMessageFlags.FileMoveHandled;
        }
    }

    #endregion

}
