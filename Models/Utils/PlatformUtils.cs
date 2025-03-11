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
        
    public static bool UseShellExecute => OperatingSystem is OS.WindowsX64 or OS.WindowsX86;

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