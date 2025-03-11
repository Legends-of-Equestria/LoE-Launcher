using System.Diagnostics;
using Models.Paths;
using Models.Utils;
using Newtonsoft.Json;

namespace Models;

public class VersionsControlFile
{
    public string Win32 { get; set; }
    public string Win64 { get; set; }
    public string Linux { get; set; }
    public string Mac { get; set; }
}
    
public class MainControlFile
{
    [JsonProperty("Version")]
    public VersionInfo Version { get; set; }

    [JsonProperty("Content")]
    public List<ControlFileItem> Content { get; set; } = [];

    [JsonProperty("RootUri")]
    public Uri RootUri { get; set; }

    public MainControlFile()
    {
    }
}

[DebuggerDisplay("{InstallPath} - {FileHash}")]
public class ControlFileItem
{
    public const string ZsyncExtension = ".zsync.jar";

    [JsonProperty("RelativeContentUrl")]
    public Uri RelativeContentUrl { get; set; }

    [JsonIgnore]
    public IRelativeFilePath? InstallPath { get; set; }

    [JsonProperty("FileHash")]
    public string FileHash { get; set; }

    [JsonProperty("_installPath")]
    public string _installPath
    {
        get => InstallPath?.ToString();
        set => InstallPath = value?.ToRelativeFilePathAuto();
    }
}