using System.Text.Json;
using System.Text.Json.Serialization;

namespace Models;

public class VersionInfoConverter : JsonConverter<VersionInfo>
{
    public override VersionInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString()!;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Unexpected token {reader.TokenType} when parsing VersionInfo");
        }

        var version = new VersionInfo { Major = 0, Minor = 0, Build = 0, Revision = 0, MajorRevision = 0, MinorRevision = 0 };
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            if (reader.TokenType != JsonTokenType.Number)
            {
                reader.Skip();
                continue;
            }

            var value = reader.GetInt32();
            switch (name?.ToLowerInvariant())
            {
                case "major": version.Major = value; break;
                case "minor": version.Minor = value; break;
                case "build": version.Build = value; break;
                case "revision": version.Revision = value; break;
                case "majorrevision": version.MajorRevision = value; break;
                case "minorrevision": version.MinorRevision = value; break;
            }
        }

        return version;
    }

    public override void Write(Utf8JsonWriter writer, VersionInfo value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("Major", value.Major);
        writer.WriteNumber("Minor", value.Minor);
        writer.WriteNumber("Build", value.Build);
        writer.WriteNumber("Revision", value.Revision);
        writer.WriteNumber("MajorRevision", value.MajorRevision);
        writer.WriteNumber("MinorRevision", value.MinorRevision);
        writer.WriteEndObject();
    }
}
