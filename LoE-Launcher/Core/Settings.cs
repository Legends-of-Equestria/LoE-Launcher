namespace LoE_Launcher;

public class Settings
{
    public bool IgnoreSSLCertificates { get; set; } = false;
    
    public string ZsyncLocation { get; set; } = "http://patches.legendsofequestria.com/zsync/{version}/";
        
    public string Stream { get; set; } = "http://patches.legendsofequestria.com/zsync/versions3.json";

    public string LauncherVersionUrl { get; set; } = "http://patches.legendsofequestria.com/launcher-version.json";

    internal string FormatZsyncLocation(string version)
    {
        var formatted = ZsyncLocation.Replace("{version}", version);
        return formatted.EndsWith('/') ? formatted : formatted + '/';
    }
}