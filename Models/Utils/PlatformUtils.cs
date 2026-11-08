using System.Diagnostics;
using System.Runtime.InteropServices;
namespace Models.Utils;

public enum OS
{
    WindowsX86,
    WindowsX64,
    MacIntel,
    MacArm,
    X11,
    Other
}

public static class PlatformUtils
{
    public static OS OperatingSystem { get; }
    public static bool UseShellExecute => true;

    public static string GameExecutableName(OS os) => os switch
    {
        OS.MacArm or OS.MacIntel => "LoE.app",
        OS.X11 => "LoE.x86_64",
        _ => "LoE.exe"
    };

    public static bool GameExecutableExists(string folder, OS os)
    {
        var path = Path.Combine(folder, GameExecutableName(os));
        return os is OS.MacArm or OS.MacIntel ? Directory.Exists(path) : File.Exists(path);
    }

    public static bool IsFlatpak => Environment.GetEnvironmentVariable("FLATPAK_ID") != null;

    public static bool IsGameRunning(OS os)
    {
        var processes = Process.GetProcessesByName(os == OS.X11 ? "LoE.x86_64" : "LoE");
        foreach (var process in processes)
        {
            process.Dispose();
        }

        return processes.Length > 0;
    }

    static PlatformUtils()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            OperatingSystem = Environment.Is64BitOperatingSystem ? OS.WindowsX64 : OS.WindowsX86;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            OperatingSystem = IsAppleSilicon() ? OS.MacArm : OS.MacIntel;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            OperatingSystem = OS.X11;
        }
        else
        {
            OperatingSystem = OS.Other;
        }
    }

    private static bool IsAppleSilicon()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return false;
        }

        return RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    }
}