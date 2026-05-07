namespace DaCollector.Server.API.v1.Models;

public class CL_AnimeGroup_Save_Request
{
    public int? MediaGroupID { get; set; }
    public int? MediaGroupParentID { get; set; }
    public string GroupName { get; set; }
    public string Description { get; set; }
    public int IsFave { get; set; }
    public int IsManuallyNamed { get; set; }
    public string SortName { get; set; }
    public int OverrideDescription { get; set; }
}
