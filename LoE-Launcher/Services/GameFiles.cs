using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;

namespace LoE_Launcher.Services;

public static class GameFiles
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static readonly string[] TempSuffixes = [".zsync", ".gz", ".jar", ".zs-old", ".part", ".tmp"];

    public static IReadOnlyList<string>? Listed(string gameFolder, IReadOnlyList<string>? patchList)
    {
        var installed = InstalledFiles.Load(gameFolder);
        if (installed.Count > 0)
        {
            return installed;
        }

        return patchList;
    }

    public static IEnumerable<string> TempFilesFor(string gameFolder, IReadOnlyList<string> files)
    {
        var root = Root(gameFolder);
        var found = new HashSet<string>(InstalledFiles.PathComparison == StringComparison.Ordinal ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var full = InstalledFiles.InsideOrNull(root, file);
            var dir = full == null ? null : Path.GetDirectoryName(full);
            if (full == null || dir == null || !Directory.Exists(dir))
            {
                continue;
            }

            var name = Path.GetFileName(full);
            var gz = name + ".gz";
            IEnumerable<string> candidates;
            try
            {
                candidates = Directory.EnumerateFiles(dir).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Warn($"Couldn't list {dir}: {ex.Message}");
                continue;
            }

            foreach (var candidate in candidates)
            {
                var candidateName = Path.GetFileName(candidate);
                if (TempSuffixes.Any(s => IsExactly(candidateName, name + s) || IsExactly(candidateName, gz + s))
                    && found.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    public static long Size(string gameFolder, IReadOnlyList<string> files)
    {
        var root = Root(gameFolder);
        long total = 0;
        foreach (var file in files)
        {
            var full = InstalledFiles.InsideOrNull(root, file);
            if (full == null)
            {
                continue;
            }

            try
            {
                var info = new FileInfo(full);
                if (info.Exists)
                {
                    total += info.Length;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Warn($"Couldn't size {file}: {ex.Message}");
            }
        }

        return total;
    }

    public static int Delete(string gameFolder, IReadOnlyList<string> files)
    {
        var root = Root(gameFolder);
        var deleted = 0;
        foreach (var file in files)
        {
            var full = InstalledFiles.InsideOrNull(root, file);
            if (full == null)
            {
                continue;
            }

            deleted += TryDeleteFile(full, root);
        }

        deleted += DeleteTemp(gameFolder, files);
        deleted += TryDeleteFile(Path.Combine(root, InstalledFiles.ManifestName), root);
        return deleted;
    }

    public static int DeleteTemp(string gameFolder, IReadOnlyList<string> files)
    {
        var root = Root(gameFolder);
        return TempFilesFor(gameFolder, files).Sum(f => TryDeleteFile(f, root));
    }

    private static bool IsExactly(string a, string b) => string.Equals(a, b, InstalledFiles.PathComparison);

    private static string Root(string gameFolder) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameFolder));

    private static int TryDeleteFile(string full, string root)
    {
        try
        {
            var existed = File.Exists(full);
            if (existed)
            {
                File.Delete(full);
            }

            InstalledFiles.RemoveEmptyParents(full, root);
            return existed ? 1 : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Warn($"Couldn't remove {full}: {ex.Message}");
            return 0;
        }
    }
}
