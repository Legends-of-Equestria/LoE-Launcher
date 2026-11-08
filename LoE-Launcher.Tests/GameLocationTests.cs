using FluentAssertions;
using LoE_Launcher.Services;
using Models.Utils;

namespace LoE_Launcher.Tests;

public class GameLocationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "loe-loc-" + Guid.NewGuid().ToString("N"));

    public GameLocationTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private string Dir(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(path);
        return path;
    }

    private string WindowsGame(params string[] parts)
    {
        var folder = Dir(parts);
        File.WriteAllText(Path.Combine(folder, "LoE.exe"), "exe");
        return folder;
    }

    private string Home => Path.Combine(_root, "Home");
    private string Personal => Path.Combine(_root, "Home", "Documents");
    private string Current => Path.Combine(_root, "Current");

    private LocationContext Context(OS os = OS.WindowsX64, string? other = null, int longest = 100) =>
        new(Current, other, longest, new KnownFolders(Home, [Personal], [Path.Combine(_root, "System")]), os);

    private static string Sub(string folder) => Path.Combine(folder, GameLocation.FolderName);

    [Fact]
    public void Resolve_ShouldUseAnInstallInPlace()
    {
        var game = WindowsGame("LoE_Win");

        GameLocation.Resolve(game, Context()).Should().Be(new LocationChoice.UseInPlace(game));
    }

    [Fact]
    public void Resolve_ShouldUseAGameSubfolderInPlace()
    {
        var game = WindowsGame("Old", "Game");

        GameLocation.Resolve(Path.Combine(_root, "Old"), Context()).Should().Be(new LocationChoice.UseInPlace(game));
    }

    [Fact]
    public void Resolve_ShouldNotAdoptAMacAppsParentWhenItIsParentOnly()
    {
        var app = Dir("Home", "LoE.app");

        var result = GameLocation.Resolve(app, Context(OS.MacArm));

        result.Should().Be(new LocationChoice.NewFolder(Sub(Home)));
    }

    [Fact]
    public void Resolve_ShouldIgnoreAnotherOsInstall()
    {
        var game = WindowsGame("LoE_Win");

        GameLocation.Resolve(game, Context(OS.MacArm)).Should().Be(new LocationChoice.NewFolder(Sub(game)));
    }

    [Fact]
    public void Resolve_ShouldCreateASubfolderInAnOrdinaryFolder()
    {
        var games = Dir("D", "Games");

        GameLocation.Resolve(games, Context()).Should().Be(new LocationChoice.NewFolder(Sub(games)));
        Directory.Exists(Sub(games)).Should().BeFalse();
        Directory.GetFileSystemEntries(games).Should().BeEmpty();
    }

    [Fact]
    public void Resolve_ShouldUseAnEmptyExistingSubfolderAsNew()
    {
        var games = Dir("D", "Games");
        Dir("D", "Games", GameLocation.FolderName);

        GameLocation.Resolve(games, Context()).Should().Be(new LocationChoice.NewFolder(Sub(games)));
    }

    [Fact]
    public void Resolve_ShouldRefuseForbiddenFolders()
    {
        var personal = Dir("Home", "Documents");

        GameLocation.Resolve(personal, Context()).Should().Be(new LocationChoice.Refused(GameFolderRules.PersonalReason));
    }

    [Fact]
    public void Resolve_ShouldRefuseAPathThatCannotBeUsed()
    {
        GameLocation.Resolve(Path.Combine(_root, "bad\0path"), Context())
            .Should().Be(new LocationChoice.Refused("That folder can't be used."));
    }

    [Fact]
    public void Resolve_ShouldRefuseANonEmptyExistingSubfolder()
    {
        var games = Dir("D", "Games");
        var existing = Dir("D", "Games", GameLocation.FolderName);
        File.WriteAllText(Path.Combine(existing, "notes.txt"), "x");

        GameLocation.Resolve(games, Context())
            .Should().Be(new LocationChoice.Refused("That folder already has other files in it."));
    }

    [Fact]
    public void Resolve_ShouldUseAnInstallInANonEmptyExistingSubfolder()
    {
        var games = Dir("D", "Games");
        var existing = WindowsGame("D", "Games", GameLocation.FolderName);

        GameLocation.Resolve(games, Context()).Should().Be(new LocationChoice.UseInPlace(existing));
    }

    [Fact]
    public void Resolve_ShouldTreatCaseAndTrailingSeparatorVariantsAsCurrentFolder()
    {
        Dir("Current");
        var variant = Current.ToUpperInvariant() + Path.DirectorySeparatorChar;
        var context = Context() with { CurrentFolder = Path.Combine(_root, "Current", GameLocation.FolderName) };
        Dir("Current", GameLocation.FolderName);

        GameLocation.Resolve(variant, context).Should().Be(new LocationChoice.Unchanged());
    }

    [Fact]
    public void Resolve_ShouldReturnUnchangedForTheCurrentGameFolder()
    {
        var game = WindowsGame("Current");

        GameLocation.Resolve(game + Path.DirectorySeparatorChar, Context()).Should().Be(new LocationChoice.Unchanged());
    }

    [Fact]
    public void Resolve_ShouldIgnoreCaseWhenOsIsWindows()
    {
        var game = WindowsGame("Current");
        var context = Context() with { CurrentFolder = game.ToUpperInvariant() + Path.DirectorySeparatorChar };

        if (OperatingSystem.IsWindows())
        {
            GameLocation.Resolve(game, context).Should().Be(new LocationChoice.Unchanged());
        }
        else
        {
            GameLocation.Resolve(game, context).Should().NotBeOfType<LocationChoice.Refused>();
        }
    }

    [Fact]
    public void Resolve_ShouldRefuseNesting()
    {
        const string message = "The new folder can't be inside the current game folder, or the other way around.";
        var inside = Dir("Current", "Sub");
        var outside = WindowsGame("Outer");
        var context = Context() with { CurrentFolder = Dir("Outer", "Inner") };

        GameLocation.Resolve(inside, Context()).Should().Be(new LocationChoice.Refused(message));
        GameLocation.Resolve(outside, context).Should().Be(new LocationChoice.Refused(message));
    }

    [Fact]
    public void Resolve_ShouldRefuseLinkIntoCurrentFolder()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var sub = Dir("Current", "Sub");
        var elsewhere = Dir("Elsewhere");
        Directory.Delete(elsewhere);
        Directory.CreateSymbolicLink(elsewhere, sub);

        GameLocation.Resolve(elsewhere, Context()).Should().Be(new LocationChoice.Refused(
            "The new folder can't be inside the current game folder, or the other way around."));
    }

    [Fact]
    public void Resolve_ShouldRefuseTheOtherChannelsFolder()
    {
        const string message = "That folder is used by the other channel's game.";
        var other = Dir("Other");
        var inside = Dir("Other", "Deeper");
        var above = WindowsGame("Wrapper");
        var otherUnder = Dir("Wrapper", "Other2");

        GameLocation.Resolve(other, Context(other: other)).Should().Be(new LocationChoice.Refused(message));
        GameLocation.Resolve(inside, Context(other: other)).Should().Be(new LocationChoice.Refused(message));
        GameLocation.Resolve(above, Context(other: otherUnder)).Should().Be(new LocationChoice.Refused(message));
    }

    [Fact]
    public void Resolve_ShouldRefuseTooLongPathsOnWindowsOnly()
    {
        var games = Dir("D", "Games");

        GameLocation.Resolve(games, Context(OS.WindowsX64, longest: 250))
            .Should().Be(new LocationChoice.Refused("That folder's path is too long for the game."));
        GameLocation.Resolve(games, Context(OS.X11, longest: 250)).Should().Be(new LocationChoice.NewFolder(Sub(games)));
    }

    [Fact]
    public void Resolve_ShouldRefuseAFolderThatDoesNotExist()
    {
        GameLocation.Resolve(Path.Combine(_root, "Missing"), Context())
            .Should().Be(new LocationChoice.Refused("The launcher can't write to that folder."));
    }

    [Fact]
    public void Resolve_ShouldRefuseAFolderTheLauncherCannotWriteTo()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            return;
        }

        var games = Dir("D", "Games");
        File.SetUnixFileMode(games, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            GameLocation.Resolve(games, Context())
                .Should().Be(new LocationChoice.Refused("The launcher can't write to that folder."));
        }
        finally
        {
            File.SetUnixFileMode(games, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void Resolve_ShouldHandleUnicodeAndSpaces()
    {
        var games = Dir("Игры и ポニー", "my games");

        GameLocation.Resolve(games, Context()).Should().Be(new LocationChoice.NewFolder(Sub(games)));
    }

    [Fact]
    public void ResolveDefault_ShouldBeUnchangedWhenTheDefaultIsCurrent()
    {
        GameLocation.ResolveDefault(Current, Current, null, OS.WindowsX64)
            .Should().Be(new LocationChoice.Unchanged());
    }

    [Fact]
    public void ResolveDefault_ShouldUseAnInstallInTheDefaultInPlace()
    {
        var game = WindowsGame("Launcher", "Game");

        GameLocation.ResolveDefault(game, Current, null, OS.WindowsX64)
            .Should().Be(new LocationChoice.UseInPlace(game));
    }

    [Fact]
    public void ResolveDefault_ShouldMoveIntoAMissingOrEmptyDefault()
    {
        var missing = Path.Combine(_root, "Launcher", "Game");
        var empty = Dir("Launcher", "Game-Dev");

        GameLocation.ResolveDefault(missing, Current, null, OS.WindowsX64)
            .Should().Be(new LocationChoice.NewFolder(missing));
        GameLocation.ResolveDefault(empty, Current, null, OS.WindowsX64)
            .Should().Be(new LocationChoice.NewFolder(empty));
    }

    [Fact]
    public void ResolveDefault_ShouldRefuseADefaultWithOtherFiles()
    {
        var folder = Dir("Launcher", "Game");
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "x");

        GameLocation.ResolveDefault(folder, Current, null, OS.WindowsX64)
            .Should().Be(new LocationChoice.Refused($"The default game folder already has other files in it:\n{folder}"));
    }

    [Fact]
    public void ResolveDefault_ShouldRefuseNestingAndTheOtherChannelsFolder()
    {
        var folder = Path.Combine(_root, "Launcher", "Game");

        GameLocation.ResolveDefault(folder, Path.Combine(folder, "Legends of Equestria"), null, OS.WindowsX64)
            .Should().BeOfType<LocationChoice.Refused>();
        GameLocation.ResolveDefault(folder, Current, folder, OS.WindowsX64)
            .Should().Be(new LocationChoice.Refused("That folder is used by the other channel's game."));
    }

    [Fact]
    public void IsSeparateCopy_ShouldBeFalseForTheCurrentFolderOrAnythingNestedWithIt()
    {
        var old = Path.Combine(_root, "Old");

        GameLocation.IsSeparateCopy(old, old, null, OS.WindowsX64).Should().BeFalse();
        GameLocation.IsSeparateCopy(Path.Combine(old, "Inner"), old, null, OS.WindowsX64).Should().BeFalse();
        GameLocation.IsSeparateCopy(old, Path.Combine(old, "Inner"), null, OS.WindowsX64).Should().BeFalse();
    }

    [Fact]
    public void IsSeparateCopy_ShouldBeFalseForTheOtherChannelsFolder()
    {
        var old = Path.Combine(_root, "Old");

        GameLocation.IsSeparateCopy(old, Current, old, OS.WindowsX64).Should().BeFalse();
        GameLocation.IsSeparateCopy(old, Current, Path.Combine(old, "Dev"), OS.WindowsX64).Should().BeFalse();
    }

    [Fact]
    public void IsSeparateCopy_ShouldBeTrueForAnUnrelatedFolder()
    {
        GameLocation.IsSeparateCopy(Path.Combine(_root, "Old"), Current, Path.Combine(_root, "Dev"), OS.WindowsX64)
            .Should().BeTrue();
    }
}
