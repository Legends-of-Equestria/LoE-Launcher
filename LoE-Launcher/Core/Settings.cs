using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LoE_Launcher;

public enum GameChannel
{
    Live,
    Dev
}

public class Settings
{
    public bool IgnoreSSLCertificates { get; set; } = false;

    public string ZsyncLocation { get; set; } = "https://patches.legendsofequestria.com/zsync/{version}/";

    public string Stream { get; set; } = "https://patches.legendsofequestria.com/zsync/versions3.json";

    public string LauncherVersionUrl { get; set; } = "https://patches.legendsofequestria.com/launcher-version.json";

    public bool CloseAfterLaunch { get; set; } = true;

    [JsonConverter(typeof(StringEnumConverter))]
    public GameChannel Channel { get; set; } = GameChannel.Live;

    // Stored by DevTokenStore, never in settings.json
    [JsonIgnore]
    public string DevToken { get; set; } = "";

    public string DevZsyncLocation { get; set; } = "https://loedata.legendsofequestria.com/dev/{version}/";

    public string DevStream { get; set; } = "https://loedata.legendsofequestria.com/dev/versions-dev.json";

    public string DevLoginUrl { get; set; } = "https://loedata.legendsofequestria.com/dev-auth/login.php";

    [JsonIgnore]
    public bool IsDevChannelActive => Channel == GameChannel.Dev && !string.IsNullOrWhiteSpace(DevToken);

    [JsonIgnore]
    public string ActiveStream => IsDevChannelActive ? DevStream : Stream;

    [JsonIgnore]
    public string ActiveInstallFolderName => IsDevChannelActive ? "Game-Dev" : "Game";

    internal string FormatZsyncLocation(string version)
    {
        var location = IsDevChannelActive ? DevZsyncLocation : ZsyncLocation;
        var formatted = location.Replace("{version}", version);
        return formatted.EndsWith('/') ? formatted : formatted + '/';
    }

    internal bool ShouldSendDevToken(Uri? requestUri) =>
        !string.IsNullOrWhiteSpace(DevToken) && IsSafeDevUri(requestUri);

    internal bool IsSafeDevUri(Uri? requestUri) =>
        IsSecureDevHost(requestUri, out var devBase)
        && requestUri!.AbsolutePath.StartsWith(devBase.AbsolutePath, StringComparison.Ordinal);

    // Forum passwords only go to the dev host
    internal bool IsSafeLoginUri(Uri? requestUri) => IsSecureDevHost(requestUri, out _);

    // Plain HTTP is only allowed for a local test server
    private bool IsSecureDevHost(Uri? requestUri, out Uri devBase)
    {
        var versionIndex = DevZsyncLocation.IndexOf("{version}", StringComparison.Ordinal);
        var devBaseUrl = versionIndex >= 0 ? DevZsyncLocation[..versionIndex] : DevZsyncLocation;
        devBase = null!;
        if (requestUri == null || !Uri.TryCreate(devBaseUrl, UriKind.Absolute, out var parsed))
        {
            return false;
        }
        devBase = parsed;

        if (!requestUri.IsLoopback && (requestUri.Scheme != Uri.UriSchemeHttps || IgnoreSSLCertificates))
        {
            return false;
        }

        return string.Equals(requestUri.Host, devBase.Host, StringComparison.OrdinalIgnoreCase)
               && requestUri.Port == devBase.Port;
    }

    internal void MigrateToHttps()
    {
        if (ZsyncLocation.StartsWith("http://patches.legendsofequestria.com"))
        {
            ZsyncLocation = ZsyncLocation.Replace("http://", "https://");
        }
        if (Stream.StartsWith("http://patches.legendsofequestria.com"))
        {
            Stream = Stream.Replace("http://", "https://");
        }
        if (LauncherVersionUrl.StartsWith("http://patches.legendsofequestria.com"))
        {
            LauncherVersionUrl = LauncherVersionUrl.Replace("http://", "https://");
        }
    }
}
