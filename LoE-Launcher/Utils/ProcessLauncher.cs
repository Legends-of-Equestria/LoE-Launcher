using System;
using System.Diagnostics;
using System.IO;
using Models.Utils;
using NLog;

namespace LoE_Launcher.Utils;

public static class ProcessLauncher
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static void LaunchUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    public static void LaunchGame(string gameInstallPath)
    {
        var currentOS = PlatformUtils.OperatingSystem;
        Logger.Info($"Launching game on {currentOS}");

        switch (currentOS)
        {
            case OS.WindowsX64:
            case OS.WindowsX86:
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(gameInstallPath, "loe.exe"),
                    UseShellExecute = PlatformUtils.UseShellExecute
                });
                break;

            case OS.MacIntel:
            case OS.MacArm:
                var macAppPath = Path.Combine(gameInstallPath, "LoE.app");

                var permissionProcess = new Process();
                permissionProcess.RunInlineAndWait(new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"-R 777 \"{macAppPath}\"",
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Minimized
                });

                Process.Start(new ProcessStartInfo
                {
                    FileName = macAppPath,
                    UseShellExecute = PlatformUtils.UseShellExecute
                });
                break;

            case OS.X11:
                var is64Bit = Environment.Is64BitProcess;
                var linuxExePath = Path.Combine(gameInstallPath, $"LoE.x86{(is64Bit ? "_64" : "")}");

                var linuxPermProcess = new Process();
                linuxPermProcess.RunInlineAndWait(new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"-R 777 \"{linuxExePath}\"",
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Minimized
                });

                Process.Start(new ProcessStartInfo
                {
                    FileName = linuxExePath,
                    UseShellExecute = PlatformUtils.UseShellExecute
                });
                break;

            case OS.Other:
            default:
                throw new PlatformNotSupportedException("This platform is not supported.");
        }

        Logger.Info("Game launched successfully");
    }
}
