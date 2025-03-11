using Models.Utils;
using Newtonsoft.Json;
namespace Models.Paths.Json;

public class RelativeFilePathConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return typeof(IRelativeFilePath).IsAssignableFrom(objectType);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }
            
        var path = (string)reader.Value;
        return path.ToRelativeFilePathAuto();
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        var path = (IRelativeFilePath)value;
        writer.WriteValue(path.ToString());
    }
}