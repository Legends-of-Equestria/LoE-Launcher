using Newtonsoft.Json;
namespace Models;

public class VersionInfo
{
    [JsonProperty("Major")]
    public int Major { get; set; } = 0;

    [JsonProperty("Minor")]
    public int Minor { get; set; } = 2;

    [JsonProperty("Build")]
    public int Build { get; set; } = -1;

    [JsonProperty("Revision")]
    public int Revision { get; set; } = -1;

    [JsonProperty("MajorRevision")]
    public int MajorRevision { get; set; } = -1;

    [JsonProperty("MinorRevision")]
    public int MinorRevision { get; set; } = -1;
        
    public VersionInfo()
    {
    }
        
    public Version ToSystemVersion()
    {
        if (Build >= 0 && Revision >= 0)
        {
            return new Version(Major, Minor, Build, Revision);
        }
        else if (Build >= 0)
        {
            return new Version(Major, Minor, Build);
        }
        else
        {
            return new Version(Major, Minor);
        }
    }
        
    public static VersionInfo FromSystemVersion(Version version)
    {
        return new VersionInfo
        {
            Major = version.Major,
            Minor = version.Minor,
            Build = version.Build,
            Revision = version.Revision,
            MajorRevision = version.MajorRevision,
            MinorRevision = version.MinorRevision
        };
    }
}
