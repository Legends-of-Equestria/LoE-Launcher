using System;
using System.IO;
using System.Linq;

namespace LoE_Launcher.Core;

public static class DataFolder
{
    private const string FolderName = "LoE Launcher";

    public static bool IsCustom { get; private set; }

    public static string Resolve(string[] args, out string? note)
    {
        note = null;
        var index = Array.IndexOf(args, "--data-dir");
        IsCustom = index >= 0 && index + 1 < args.Length;
        if (IsCustom)
        {
            return Path.GetFullPath(args[index + 1]);
        }

        var roaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName);
        if (!OperatingSystem.IsWindows())
        {
            return roaming;
        }

        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderName);
        return MoveLegacy(roaming, local, out note);
    }

    public static string MoveLegacy(string oldDir, string newDir, out string? note)
    {
        note = null;
        if (!Directory.Exists(oldDir))
        {
            return newDir;
        }

        if (Directory.Exists(newDir) && Directory.EnumerateFileSystemEntries(newDir).Any())
        {
            note = $"Both {oldDir} and {newDir} exist, using {newDir}";
            return newDir;
        }

        if (!string.Equals(Path.GetPathRoot(Path.GetFullPath(oldDir)), Path.GetPathRoot(Path.GetFullPath(newDir)),
                StringComparison.OrdinalIgnoreCase))
        {
            note = $"{oldDir} is on another drive than {newDir}, keeping it where it is";
            return oldDir;
        }

        try
        {
            if (Directory.Exists(newDir))
            {
                Directory.Delete(newDir);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(newDir))!);
            Directory.Move(oldDir, newDir);
            note = $"Moved launcher data from {oldDir} to {newDir}";
            return newDir;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            note = $"Couldn't move {oldDir} to {newDir} ({ex.Message}), using the old folder for now";
            return oldDir;
        }
    }
}
