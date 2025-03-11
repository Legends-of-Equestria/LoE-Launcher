using Newtonsoft.Json;

namespace Models.Paths.Json;

public class RelativeFilePathContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver
{
    protected override JsonConverter ResolveContractConverter(Type objectType)
    {
        if (typeof(IRelativeFilePath).IsAssignableFrom(objectType) && !objectType.IsAbstract)
        {
            return new RelativeFilePathConverter();
        }
                
        return base.ResolveContractConverter(objectType);
    }
}