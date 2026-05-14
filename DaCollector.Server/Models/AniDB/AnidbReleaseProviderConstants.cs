namespace DaCollector.Server.Providers.AniDB.Release;

// Stub constants kept for database migration backward compatibility.
// The AniDB provider has been removed; these values preserve existing URI formats in the DB.
internal static class AnidbReleaseProvider
{
    internal const string ReleasePrefix = "anidb://";
    internal const string IdPrefix = "anidb-ed2k://";
}
