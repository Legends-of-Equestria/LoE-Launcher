using System;
using System.IO;
using System.Linq;
using Models.Utils;

namespace LoE_Launcher.Services;

public abstract record LocationChoice
{
    public sealed record Refused(string Message) : LocationChoice;

    public sealed record UseInPlace(string Folder) : LocationChoice;

    public sealed record NewFolder(string Folder) : LocationChoice;

    public sealed record Unchanged : LocationChoice;
}

public sealed record LocationContext(string CurrentFolder, string? OtherChannelFolder, int LongestGamePath, KnownFolders Known, OS Os);

public static class GameLocation
{
    public const string FolderName = "Legends of Equestria";

    private const string CannotUse = "That folder can't be used.";
    private const string Nested = "The new folder can't be inside the current game folder, or the other way around.";
    private const string OtherChannel = "That folder is used by the other channel's game.";
    private const string TooLong = "That folder's path is too long for the game.";
    public const string NotWritable = "The launcher can't write to that folder.";
    private const string NotEmpty = "That folder already has other files in it.";

    public static LocationChoice Resolve(string picked, LocationContext context)
    {
        try
        {
            return ResolveCore(picked, context);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new LocationChoice.Refused(CannotUse);
        }
    }

    public static LocationChoice ResolveDefault(string defaultFolder, string currentFolder, string? otherChannelFolder, OS os)
    {
        var comparison = GameFolderRules.Comparison(os);

        if (string.Equals(GameFolderRules.Normalize(defaultFolder), GameFolderRules.Normalize(currentFolder), comparison))
        {
            return new LocationChoice.Unchanged();
        }

        if (GameFolderRules.IsSameOrInside(defaultFolder, currentFolder, os)
            || GameFolderRules.IsSameOrInside(currentFolder, defaultFolder, os))
        {
            return new LocationChoice.Refused(Nested);
        }

        if (!string.IsNullOrEmpty(otherChannelFolder)
            && (GameFolderRules.IsSameOrInside(defaultFolder, otherChannelFolder, os)
                || GameFolderRules.IsSameOrInside(otherChannelFolder, defaultFolder, os)))
        {
            return new LocationChoice.Refused(OtherChannel);
        }

        if (GameImporter.ContainsGame(defaultFolder, os))
        {
            return new LocationChoice.UseInPlace(defaultFolder);
        }

        if (Directory.Exists(defaultFolder) && Directory.EnumerateFileSystemEntries(defaultFolder).Any())
        {
            return new LocationChoice.Refused($"The default game folder already has other files in it:\n{defaultFolder}");
        }

        return new LocationChoice.NewFolder(defaultFolder);
    }

    public static bool IsSeparateCopy(string oldCopy, string currentFolder, string? otherChannelFolder, OS os)
    {
        bool Overlaps(string folder) =>
            GameFolderRules.IsSameOrInside(oldCopy, folder, os) || GameFolderRules.IsSameOrInside(folder, oldCopy, os);

        return !Overlaps(currentFolder) && (string.IsNullOrEmpty(otherChannelFolder) || !Overlaps(otherChannelFolder));
    }

    private static LocationChoice ResolveCore(string picked, LocationContext context)
    {
        var os = context.Os;
        var known = context.Known;
        var comparison = GameFolderRules.Comparison(os);

        var verdict = GameFolderRules.Classify(picked, known, os);
        if (verdict.Kind == FolderKind.Forbidden)
        {
            return new LocationChoice.Refused(verdict.Reason ?? CannotUse);
        }

        string candidate;
        var inPlace = false;
        var found = GameImporter.FindGameFolder(picked, os);

        if (found != null && GameFolderRules.Classify(found, known, os).Kind == FolderKind.Allowed)
        {
            candidate = found;
            inPlace = true;
        }
        else
        {
            var pickedFull = Path.GetFullPath(picked).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var isApp = os is OS.MacArm or OS.MacIntel && pickedFull.EndsWith(".app", comparison);
            var baseFolder = isApp ? Path.GetDirectoryName(pickedFull) ?? pickedFull : pickedFull;
            candidate = Path.Combine(baseFolder, FolderName);

            if (Directory.Exists(candidate) && Directory.EnumerateFileSystemEntries(candidate).Any())
            {
                var existing = GameImporter.FindGameFolder(candidate, os);
                if (existing == null || GameFolderRules.Classify(existing, known, os).Kind != FolderKind.Allowed)
                {
                    return new LocationChoice.Refused(NotEmpty);
                }

                candidate = existing;
                inPlace = true;
            }
        }

        var normalized = GameFolderRules.Normalize(candidate);

        if (string.Equals(normalized, GameFolderRules.Normalize(context.CurrentFolder), comparison))
        {
            return new LocationChoice.Unchanged();
        }

        if (GameFolderRules.IsSameOrInside(candidate, context.CurrentFolder, os)
            || GameFolderRules.IsSameOrInside(context.CurrentFolder, candidate, os))
        {
            return new LocationChoice.Refused(Nested);
        }

        if (!string.IsNullOrEmpty(context.OtherChannelFolder)
            && (GameFolderRules.IsSameOrInside(candidate, context.OtherChannelFolder, os)
                || GameFolderRules.IsSameOrInside(context.OtherChannelFolder, candidate, os)))
        {
            return new LocationChoice.Refused(OtherChannel);
        }

        if (os is OS.WindowsX64 or OS.WindowsX86 && normalized.Length + 1 + context.LongestGamePath > 259)
        {
            return new LocationChoice.Refused(TooLong);
        }

        var probeFolder = Directory.Exists(candidate) ? candidate : Path.GetDirectoryName(candidate);
        if (probeFolder == null || !CanWrite(probeFolder))
        {
            return new LocationChoice.Refused(NotWritable);
        }

        return inPlace ? new LocationChoice.UseInPlace(candidate) : new LocationChoice.NewFolder(candidate);
    }

    private static bool CanWrite(string folder)
    {
        var probe = Path.Combine(folder, ".loe-write-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (File.Create(probe))
            {
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return false;
        }
        finally
        {
            try
            {
                File.Delete(probe);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
