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

public static class GameFolderRules
{
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

    public static bool IsSharedPlace(string path, KnownFolders known, OS os)
    {
        var comparison = Comparison(os);
        var normalized = Normalize(path);

        return Path.GetPathRoot(normalized) == normalized
               || Matches(normalized, known.Home, comparison)
               || known.Personal.Any(p => Matches(normalized, p, comparison))
               || known.System.Any(p => Matches(normalized, p, comparison));
    }

    public static StringComparison Comparison(OS os) =>
        os is OS.X11 or OS.Other ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private static bool Matches(string normalized, string folder, StringComparison comparison)
    {
        if (string.IsNullOrEmpty(folder))
        {
            return false;
        }

        try
        {
            return string.Equals(normalized, Normalize(folder), comparison);
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
