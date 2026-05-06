using System;
using Newtonsoft.Json;
using DaCollector.Server.Plex.Models.Collection;
using DaCollector.Server.Plex.Models.Libraries;
using DaCollector.Server.Plex.Models.TVShow;
using DaCollector.Server.Plex.Collection;
using DaCollector.Server.Plex.Libraries;
using DaCollector.Server.Plex.TVShow;

namespace DaCollector.Server.Plex;

internal class PlexConverter : JsonConverter
{
    private readonly PlexHelper _helper;

    public PlexConverter(PlexHelper helper)
    {
        _helper = helper;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotSupportedException();
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
        JsonSerializer serializer)
    {
        object instance = null;
        if (objectType == typeof(Directory))
        {
            instance = new SVR_Directory(_helper);
        }
        else if (objectType == typeof(Episode))
        {
            instance = new SVR_Episode(_helper);
        }
        else if (objectType == typeof(PlexLibrary))
        {
            instance = new SVR_PlexLibrary(_helper);
        }

        //var instance = objectType.GetConstructor(new[] { typeof(PlexHelper) })?.Invoke(new object[] { _helper });
        serializer.Populate(reader, instance);
        return instance;
    }

    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(Directory) || objectType == typeof(Episode) ||
               objectType == typeof(PlexLibrary);
    }
}
