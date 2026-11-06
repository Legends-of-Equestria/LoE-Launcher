using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LoE_Launcher.Core;
using NLog;

namespace LoE_Launcher.Services;

public static class InstalledFiles
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public const string ManifestName = "launcher-files.json";

    private static readonly string[] UnityJunkSuffixes = ["_DoNotShip", "_ButDontShipItWithYourGame"];

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static IReadOnlyList<string> Load(string gameFolder)
    {
        var path = Path.Combine(gameFolder, ManifestName);
        try
        {
            return File.Exists(path)
                ? LauncherJson.Parse(File.ReadAllText(path), LauncherJson.Default.ListString) ?? []
                : [];
        }
        catch (Exception ex)
        {
            Logger.Warn($"Couldn't read {path}: {ex.Message}");
            return [];
        }
    }

    public static void Save(string gameFolder, IEnumerable<string> files)
    {
        var sorted = files.Distinct(PathComparer).OrderBy(f => f, StringComparer.Ordinal).ToList();
        File.WriteAllText(Path.Combine(gameFolder, ManifestName), JsonSerializer.Serialize(sorted, LauncherJson.Default.ListString));
    }

    public static IReadOnlyList<string> RemoveDropped(string gameFolder, IReadOnlyCollection<string> current)
    {
        var previous = Load(gameFolder);
        if (current.Count == 0)
        {
            Logger.Warn("Patch lists no files, leaving the install alone");
            return [];
        }

        var currentSet = current.ToHashSet(PathComparer);
        var dropped = previous.Where(f => !currentSet.Contains(f)).ToList();
        if (dropped.Count > previous.Count / 2)
        {
            Logger.Warn($"Patch would remove {dropped.Count} of {previous.Count} files, skipping cleanup");
            return [];
        }

        var root = Path.GetFullPath(gameFolder);
        var removed = new List<string>();
        var kept = new List<string>();
        foreach (var file in dropped)
        {
            var full = Path.GetFullPath(Path.Combine(root, file));
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, PathComparison))
            {
                continue;
            }

            try
            {
                if (File.Exists(full))
                {
                    File.Delete(full);
                    removed.Add(file);
                    RemoveEmptyParents(full, root);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Warn($"Couldn't remove {file}, trying again next time: {ex.Message}");
                kept.Add(file);
            }
        }

        Save(gameFolder, current.Concat(kept));
        if (removed.Count > 0)
        {
            Logger.Info($"Removed {removed.Count} files the patch no longer includes: {string.Join(", ", removed)}");
        }

        return removed;
    }

    public static void SweepJunk(string gameFolder, bool removePartialDownloads)
    {
        var root = new DirectoryInfo(gameFolder);
        if (!root.Exists)
        {
            return;
        }

        foreach (var dir in root.EnumerateDirectories()
                     .Where(d => UnityJunkSuffixes.Any(s => d.Name.EndsWith(s, StringComparison.OrdinalIgnoreCase))))
        {
            TryDelete(() => dir.Delete(true), dir.Name);
        }

        if (!removePartialDownloads)
        {
            return;
        }

        foreach (var file in root.EnumerateFiles("*.part", SearchOption.AllDirectories))
        {
            TryDelete(file.Delete, file.Name);
        }
    }

    private static void TryDelete(Action delete, string name)
    {
        try
        {
            delete();
            Logger.Info($"Removed {name}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Warn($"Couldn't remove {name}: {ex.Message}");
        }
    }

    private static void RemoveEmptyParents(string file, string root)
    {
        var dir = Path.GetDirectoryName(file);
        while (dir != null && dir.Length > root.Length && !Directory.EnumerateFileSystemEntries(dir).Any())
        {
            Directory.Delete(dir);
            dir = Path.GetDirectoryName(dir);
        }
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
