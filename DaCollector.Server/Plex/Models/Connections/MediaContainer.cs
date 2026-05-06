using System.Xml.Serialization;

namespace DaCollector.Server.Plex.Models.Connections;

[XmlRoot(ElementName = "MediaContainer")]
public class MediaContainer
{
    [XmlElement(ElementName = "Device")] public MediaDevice[] Device { get; set; }
    [XmlAttribute(AttributeName = "size")] public string Size { get; set; }
}
