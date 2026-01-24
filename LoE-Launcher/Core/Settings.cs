namespace LoE_Launcher;

public class Settings
{
    public bool IgnoreSSLCertificates { get; set; } = false;
    
    public string ZsyncLocation { get; set; } = "https://patches.legendsofequestria.com/zsync/{version}/";
        
    public string Stream { get; set; } = "https://patches.legendsofequestria.com/zsync/versions3.json";

    public string LauncherVersionUrl { get; set; } = "https://patches.legendsofequestria.com/launcher-version.json";

    public bool CloseAfterLaunch { get; set; } = true;

    internal string FormatZsyncLocation(string version)
    {
        var formatted = ZsyncLocation.Replace("{version}", version);
        return formatted.EndsWith('/') ? formatted : formatted + '/';
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