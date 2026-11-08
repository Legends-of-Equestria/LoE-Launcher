using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Models.Utils;

namespace LoE_Launcher.Services;

public sealed record KnownFolders(string Home, IReadOnlyList<string> Personal, IReadOnlyList<string> System)
{
    public static KnownFolders ForCurrentUser(OS os)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var personal = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            string.IsNullOrEmpty(home) ? "" : Path.Combine(home, "Downloads")
        };
        var system = new List<string>();

        switch (os)
        {
            case OS.WindowsX64 or OS.WindowsX86:
                foreach (var name in new[] { "OneDrive", "OneDriveConsumer", "OneDriveCommercial" })
                {
                    var value = Environment.GetEnvironmentVariable(name);
                    if (!string.IsNullOrEmpty(value) && Directory.Exists(value))
                    {
                        personal.Add(value);
                    }
                }

                system.Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
                system.Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
                system.Add(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
                break;
            case OS.MacArm or OS.MacIntel:
                if (!string.IsNullOrEmpty(home))
                {
                    personal.Add(Path.Combine(home, "Library", "Mobile Documents"));
                    system.Add(Path.Combine(home, "Applications"));
                }

                system.AddRange(["/System", "/Library", "/usr", "/bin", "/etc", "/opt", "/var", "/Applications"]);
                break;
            default:
                system.AddRange(["/usr", "/bin", "/etc", "/opt", "/var"]);
                break;
        }

        return new KnownFolders(home, personal.Where(p => !string.IsNullOrEmpty(p)).ToList(),
            system.Where(p => !string.IsNullOrEmpty(p)).ToList());
    }
}

public enum FolderKind { Allowed, ParentOnly, Forbidden }

public sealed record FolderVerdict(FolderKind Kind, string? Reason);

public static class GameFolderRules
{
    public const string PersonalReason = "Pick a folder outside your Desktop, Documents, Downloads and cloud-synced folders.";
    public const string SystemReason = "That's a system folder. Pick a folder you own, like one on another drive.";
    public const string NetworkReason = "Network drives aren't supported. Pick a folder on this computer.";

    public static string Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? "";
        var current = root;

        foreach (var segment in full[root.Length..].Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            try
            {
                FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
                if (info.Exists && info.ResolveLinkTarget(true) is { } target)
                {
                    current = Path.GetFullPath(target.FullName);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return Trim(current);
    }

    public static FolderVerdict Classify(string path, KnownFolders known, OS os)
    {
        if (IsNetwork(path, os))
        {
            return new FolderVerdict(FolderKind.Forbidden, NetworkReason);
        }

        var comparison = Comparison(os);
        var normalized = Normalize(path);

        if (known.Personal.Any(p => Matches(normalized, p, comparison, true)))
        {
            return new FolderVerdict(FolderKind.Forbidden, PersonalReason);
        }

        if (known.System.Any(p => Matches(normalized, p, comparison, true)))
        {
            return new FolderVerdict(FolderKind.Forbidden, SystemReason);
        }

        return Path.GetPathRoot(normalized) == normalized || Matches(normalized, known.Home, comparison)
            ? new FolderVerdict(FolderKind.ParentOnly, null)
            : new FolderVerdict(FolderKind.Allowed, null);
    }

    public static bool IsSameOrInside(string path, string folder, OS os) =>
        IsSameOrInside(Normalize(path), Normalize(folder), Comparison(os));

    public static StringComparison Comparison(OS os) =>
        os is OS.X11 or OS.Other ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private static bool IsSameOrInside(string normalized, string folder, StringComparison comparison)
    {
        if (string.Equals(normalized, folder, comparison))
        {
            return true;
        }

        var prefix = folder.EndsWith(Path.DirectorySeparatorChar) || folder.EndsWith(Path.AltDirectorySeparatorChar)
            ? folder
            : folder + Path.DirectorySeparatorChar;
        return normalized.StartsWith(prefix, comparison);
    }

    private static bool IsNetwork(string path, OS os)
    {
        if (path.StartsWith(@"\\") || path.StartsWith("//"))
        {
            return true;
        }

        if (os is not (OS.WindowsX64 or OS.WindowsX86) || !OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path)) ?? "").DriveType == DriveType.Network;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool Matches(string normalized, string folder, StringComparison comparison, bool includeInside = false)
    {
        if (string.IsNullOrEmpty(folder))
        {
            return false;
        }

        try
        {
            var other = Normalize(folder);
            return includeInside ? IsSameOrInside(normalized, other, comparison) : string.Equals(normalized, other, comparison);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static string Trim(string path)
    {
        var root = Path.GetPathRoot(path);
        return path == root ? path : path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
