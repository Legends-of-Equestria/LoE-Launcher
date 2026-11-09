using System.Diagnostics;
using Models.Paths;
using Models.Utils;
using System.Text.Json.Serialization;

namespace Models;

public class VersionsControlFile
{
    public string? Win64 { get; set; }
    public string? Linux { get; set; }
    public string? Mac { get; set; }
    public string? Mac_Intel { get; set; }
    public string? Mac_Arm { get; set; }

    // Only on the dev stream
    public BuildInfo? Build { get; set; }

    public static bool IsSupported(OS os) => os is OS.WindowsX64 or OS.X11 or OS.MacArm or OS.MacIntel;

    public string? ForPlatform(OS os) => os switch
    {
        OS.WindowsX64 => Win64,
        OS.X11 => Linux,
        OS.MacArm => FirstSet(Mac, Mac_Arm),
        OS.MacIntel => FirstSet(Mac, Mac_Intel),
        _ => null
    };

    public static string? VersionName(string? folder) =>
        string.IsNullOrEmpty(folder) || folder == "default" ? null : folder[(folder.LastIndexOf('_') + 1)..];

    private static string? FirstSet(string? preferred, string? fallback) =>
        string.IsNullOrEmpty(preferred) ? fallback : preferred;
}

public class BuildInfo
{
    public string? Id { get; set; }
    public string? Branch { get; set; }
    public string? Commit { get; set; }
    public string? Time { get; set; }

    public override string ToString()
    {
        var source = string.Join("@", new[] { Branch, Commit }.Where(part => !string.IsNullOrEmpty(part)));
        return string.Join(" · ", new[] { Id, source, Time }.Where(part => !string.IsNullOrEmpty(part)));
    }
}
    
public class MainControlFile
{
    [JsonPropertyName("Version")]
    public VersionInfo? Version { get; set; }

    [JsonPropertyName("Content")]
    public List<ControlFileItem> Content { get; set; } = [];

    [JsonPropertyName("RootUri")]
    public Uri? RootUri { get; set; }
}

[DebuggerDisplay("{InstallPath} - {FileHash}")]
public class ControlFileItem
{
    public const string ZsyncExtension = ".zsync.jar";

    [JsonPropertyName("RelativeContentUrl")]
    public Uri? RelativeContentUrl { get; set; }

    [JsonIgnore]
    public IRelativeFilePath? InstallPath { get; set; }

    [JsonPropertyName("FileHash")]
    public string? FileHash { get; set; }

    [JsonPropertyName("_installPath")]
    public string? _installPath
    {
        get => InstallPath?.ToString();
        set => InstallPath = value?.ToRelativeFilePathAuto();
    }

    public IRelativeFilePath RequireInstallPath() =>
        InstallPath ?? throw new InvalidDataException($"Control file entry {RelativeContentUrl} has no install path");
}