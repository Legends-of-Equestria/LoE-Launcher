namespace LoE_Launcher
{
    public class Settings
    {
        public bool IgnoreSSLCertificates { get; set; } = false;

        public string ZsyncLocation { get; set; } = "http://patches.legendsofequestria.com/zsync/{version}/";
        
        public string Stream { get; set; } = "http://patches.legendsofequestria.com/zsync/versions3.json";

        internal string FormatZsyncLocation(string version)
            => ZsyncLocation.Replace("{version}", version);
    }
}
