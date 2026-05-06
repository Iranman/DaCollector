using System;
using System.Globalization;
using System.IO;
using System.Linq;
using DaCollector.Abstractions.Extensions;
using DaCollector.Server.Repositories;
using DaCollector.Server.Server;

namespace DaCollector.Server.Models.Legacy;

public class Scan
{
    public int ScanID { get; set; }

    public DateTime CreationTIme { get; set; }

    public string ImportFolders { get; set; }

    public ScanStatus Status { get; set; }

    public string TitleText =>
        CreationTIme.ToString(CultureInfo.CurrentUICulture) + " (" + string.Join(" | ",
            this.ImportFolders.Split(',')
                .Select(int.Parse)
                .Select(RepoFactory.DaCollectorManagedFolder.GetByID)
                .WhereNotNull()
                .Select(a => a.Path
                    .Split(
                        new[] { Path.PathSeparator, Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                        StringSplitOptions.RemoveEmptyEntries)
                    .LastOrDefault())
                .ToArray()) + ")";
}
