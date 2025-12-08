using System;
using Models.Utils;
using NLog;
using Velopack;
using Velopack.Sources;

namespace LoE_Launcher.Services;

/// <summary>
/// Helper class for Velopack cross-platform update management.
/// Each platform has a unique PackId to allow coexistence in a unified GitHub Release.
/// </summary>
public static class VelopackHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const string GitHubRepoUrl = "https://github.com/Legends-of-Equestria/LoE-Launcher";

    // Platform-specific pack IDs
    // These MUST match the --packId in build scripts
    private const string WindowsPackId = "LoE-Launcher";
    private const string MacPackId = "LoE-Launcher-Mac";
    private const string LinuxPackId = "LoE-Launcher-Linux";

    /// <summary>
    /// Gets the platform-specific Velopack PackId based on the current OS.
    /// </summary>
    public static string GetPackId()
    {
        var os = PlatformUtils.OperatingSystem;

        return os switch
        {
            OS.WindowsX86 or OS.WindowsX64 => WindowsPackId,
            OS.MacIntel or OS.MacArm => MacPackId,
            OS.X11 => LinuxPackId,
            _ => WindowsPackId
        };
    }

    /// <summary>
    /// Creates a Velopack UpdateManager configured for the current platform.
    /// Returns null if not running in a Velopack context (e.g., during development/tests).
    /// </summary>
    /// <remarks>
    /// Velopack automatically detects the correct package based on the app's embedded metadata.
    /// The platform-specific packId is baked into the app at build time via vpk pack --packId.
    /// The merged releases.stable.json contains entries for all platforms, and Velopack
    /// filters to find the matching packId automatically.
    /// </remarks>
    public static UpdateManager? CreateUpdateManager()
    {
        try
        {
            var packId = GetPackId();
            Logger.Info($"Expected platform PackId: {packId}");

            var options = new UpdateOptions
            {
                ExplicitChannel = "Stable"
            };

            var source = new GithubSource(GitHubRepoUrl, null, false);
            return new UpdateManager(source, options);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to create UpdateManager. This may be expected when running outside a Velopack context (e.g., during development or tests).");
            return null;
        }
    }
}
