using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Models.Utils;
using NLog;

namespace LoE_Launcher.Services;

public static class GameMover
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static void CopyVerified(string source, string target, IReadOnlyList<string> files, Action<int, int>? progress = null)
    {
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
        {
            throw new IOException($"{target} isn't empty");
        }

        var copied = new List<string>();
        try
        {
            Directory.CreateDirectory(target);
            var sourceRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
            var targetRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target));
            var manifest = Path.Combine(sourceRoot, InstalledFiles.ManifestName);
            var all = files.ToList();
            var includeManifest = File.Exists(manifest);
            var total = all.Count + (includeManifest ? 1 : 0);

            for (var i = 0; i < all.Count; i++)
            {
                var from = InstalledFiles.InsideOrNull(sourceRoot, all[i])
                           ?? throw new IOException($"{all[i]} is outside the game folder");
                var to = InstalledFiles.InsideOrNull(targetRoot, all[i])
                         ?? throw new IOException($"{all[i]} can't be copied into the new folder");
                CopyOne(from, to, all[i], copied);
                progress?.Invoke(i + 1, total);
            }

            if (includeManifest)
            {
                CopyOne(manifest, Path.Combine(targetRoot, InstalledFiles.ManifestName), InstalledFiles.ManifestName, copied);
                progress?.Invoke(total, total);
            }
        }
        catch
        {
            Logger.Warn("Copy failed, removing what was copied");
            GameFiles.Delete(target, copied);
            throw;
        }
    }

    public static bool HasSpaceFor(string target, long bytes)
    {
        var path = GameFolderRules.Normalize(NearestExisting(target));
        DriveInfo? best = null;
        var bestLength = -1;
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                var rootName = drive.RootDirectory.FullName;
                if (path.StartsWith(rootName, InstalledFiles.PathComparison) && rootName.Length > bestLength)
                {
                    best = drive;
                    bestLength = rootName.Length;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Warn($"Couldn't read a drive: {ex.Message}");
            }
        }

        if (best == null)
        {
            return false;
        }

        try
        {
            return best.AvailableFreeSpace >= bytes * 1.05;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Warn($"Couldn't read free space: {ex.Message}");
            return false;
        }
    }

    private static string NearestExisting(string path)
    {
        var current = Path.GetFullPath(path);
        while (!Directory.Exists(current) && Path.GetDirectoryName(current) is { } parent)
        {
            current = parent;
        }

        return current;
    }

    private static void CopyOne(string from, string to, string name, List<string> copied)
    {
        if (!File.Exists(from))
        {
            throw new IOException($"{name} is missing from the game folder");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        copied.Add(name);
        File.Copy(from, to, true);
        if (new FileInfo(from).Length != new FileInfo(to).Length
            || from.GetFileHash(HashType.MD5) != to.GetFileHash(HashType.MD5))
        {
            throw new IOException($"Copy of {name} didn't match the original");
        }
    }
}
