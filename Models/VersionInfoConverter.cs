using Newtonsoft.Json;

namespace Models;

public class VersionInfoConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(VersionInfo);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String)
        {
            var versionString = (string)reader.Value;
            return (VersionInfo)versionString;
        }
        else if (reader.TokenType == JsonToken.StartObject)
        {
            return serializer.Deserialize<VersionInfo>(reader);
        }

        throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing VersionInfo");
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var versionInfo = (VersionInfo)value;
        serializer.Serialize(writer, versionInfo);
    }
}
