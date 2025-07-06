using Newtonsoft.Json;

namespace Models;

public class LauncherVersionInfo
{
    [JsonProperty("RequiredVersion")]
    [JsonConverter(typeof(VersionInfoConverter))]
    public VersionInfo RequiredVersion { get; set; } = new VersionInfo();

    [JsonProperty("Message")]
    public string Message { get; set; } = "Your launcher is out of date. Please download the latest version.";

    public bool IsCompatible(VersionInfo currentVersion)
    {
        var current = currentVersion.ToSystemVersion();
        var required = RequiredVersion.ToSystemVersion();

        return current.CompareTo(required) == 0;
    }
}