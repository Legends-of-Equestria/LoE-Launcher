using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Models.Utils;
using NLog;

namespace LoE_Launcher.Services;

public enum ImportCheck
{
    Ok,
    GameNotFound,
    SameFolder,
    AlreadyInstalled
}

public static class GameImporter
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static bool ContainsGame(string folder, OS os) => PlatformUtils.GameExecutableExists(folder, os);

    public static string? FindGameFolder(string picked, OS os)
    {
        var full = Path.GetFullPath(picked).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidates = new[]
        {
            full,
            Path.Combine(full, "Game"),
            Path.Combine(full, "Contents", "MacOS", "Game"),
            Path.GetFileName(full).Equals("LoE.app", StringComparison.OrdinalIgnoreCase)
                ? Path.GetDirectoryName(full)
                : null
        };

        return candidates.FirstOrDefault(c => c != null && ContainsGame(c, os));
    }

    public static ImportCheck Check(string? source, string target)
    {
        if (source == null)
        {
            return ImportCheck.GameNotFound;
        }

        var src = Normalize(source);
        var dst = Normalize(target);
        if (string.Equals(src, dst, PathComparison) || IsInside(dst, src) || IsInside(src, dst))
        {
            return ImportCheck.SameFolder;
        }

        if (Directory.Exists(dst) && Directory.EnumerateFileSystemEntries(dst).Any())
        {
            return ImportCheck.AlreadyInstalled;
        }

        return ImportCheck.Ok;
    }

    private static readonly string[] SkippedFolderNames =
        ["AppData", "Windows", "node_modules", "Library", "System32", "WindowsApps", "$Recycle.Bin"];

    public static IReadOnlyList<(string Path, int Depth)> SearchRoots(OS os)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new List<(string, int)>
        {
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LoE Launcher"), 0),
            (Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 3),
            (Path.Combine(home, "Downloads"), 3),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 3),
            (home, 2)
        };

        switch (os)
        {
            case OS.WindowsX64 or OS.WindowsX86:
                roots.Add((Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), 2));
                roots.Add((Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), 2));
                roots.AddRange(DriveInfo.GetDrives()
                    .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                    .Select(d => (d.RootDirectory.FullName, 2)));
                break;
            case OS.MacArm or OS.MacIntel:
                roots.Add(("/Applications", 1));
                roots.Add((Path.Combine(home, "Applications"), 1));
                break;
        }

        return roots.Where(r => !string.IsNullOrEmpty(r.Item1)).ToList();
    }

    public static IReadOnlyList<string> FindInstalls(IEnumerable<(string Path, int Depth)> roots, OS os,
        string target, string launcherFolder, int maxFolders = 5000)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
        var queue = new Queue<(string Path, int DepthLeft)>(roots);
        var launcher = Normalize(launcherFolder);

        while (queue.Count > 0 && seen.Count < maxFolders)
        {
            var (dir, depthLeft) = queue.Dequeue();
            string full;
            try
            {
                full = Normalize(dir);
            }
            catch (Exception)
            {
                continue;
            }

            if (!seen.Add(full) || !Directory.Exists(full)
                || string.Equals(full, launcher, PathComparison) || IsInside(full, launcher))
            {
                continue;
            }

            var game = FindGameFolder(full, os);
            if (game != null)
            {
                if (Check(game, target) == ImportCheck.Ok && !found.Contains(game, StringComparer.Ordinal))
                {
                    found.Add(game);
                }

                continue;
            }

            if (depthLeft <= 0 || full.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                foreach (var sub in new DirectoryInfo(full).EnumerateDirectories())
                {
                    if ((sub.Attributes & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint)) != 0
                        || sub.Name.StartsWith('.')
                        || SkippedFolderNames.Contains(sub.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    queue.Enqueue((sub.FullName, depthLeft - 1));
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
            }
        }

        return found.OrderByDescending(GameTimestamp).ToList();
    }

    private static DateTime GameTimestamp(string folder)
    {
        try
        {
            return new DirectoryInfo(folder).EnumerateFileSystemInfos()
                .Select(entry => entry.LastWriteTimeUtc)
                .Append(Directory.GetLastWriteTimeUtc(folder))
                .Max();
        }
        catch (Exception)
        {
            return DateTime.MinValue;
        }
    }

    public static void MoveInstall(string source, string target, Action<int, int>? progress = null)
    {
        var check = Check(source, target);
        if (check != ImportCheck.Ok)
        {
            throw new InvalidOperationException($"Can't import from {source}: {check}");
        }

        if (Directory.Exists(target))
        {
            Directory.Delete(target);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Normalize(target))!);

        if (SameVolume(source, target))
        {
            try
            {
                Directory.Move(source, target);
                Logger.Info($"Moved game install from {source} to {target}");
                return;
            }
            catch (IOException ex) when (!OperatingSystem.IsWindows() && !Directory.Exists(target))
            {
                Logger.Info($"Folder move failed ({ex.Message}), moving file by file");
            }
        }

        MoveFileByFile(source, target, progress);
    }

    private static void MoveFileByFile(string source, string target, Action<int, int>? progress)
    {
        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        for (var i = 0; i < files.Length; i++)
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, files[i]));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Move(files[i], destination, true);
            progress?.Invoke(i + 1, files.Length);
        }

        try
        {
            Directory.Delete(source, true);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Moved all files but couldn't remove {source}: {ex.Message}");
        }

        Logger.Info($"Moved {files.Length} files from {source} to {target}");
    }

    private static bool SameVolume(string a, string b) =>
        !OperatingSystem.IsWindows()
        || string.Equals(Path.GetPathRoot(Path.GetFullPath(a)), Path.GetPathRoot(Path.GetFullPath(b)),
            StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool IsInside(string path, string folder) =>
        path.StartsWith(folder + Path.DirectorySeparatorChar, PathComparison);
}
