using System.Text.Json.Serialization;

namespace Models;

public class LauncherVersionInfo
{
    [JsonPropertyName("RequiredVersion")]
    public VersionInfo RequiredVersion { get; set; } = new VersionInfo();

    [JsonPropertyName("Message")]
    public string Message { get; set; } = "Your launcher is out of date. Please download the latest version.";

    public bool IsCompatible(VersionInfo currentVersion)
    {
        var current = currentVersion.ToSystemVersion();
        var required = RequiredVersion.ToSystemVersion();

        return current.CompareTo(required) >= 0;
    }
}