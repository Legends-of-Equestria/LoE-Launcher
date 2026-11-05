using FluentAssertions;
using LoE_Launcher.Services;
using Models.Utils;

namespace LoE_Launcher.Tests;

public class GameImporterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "loe-import-" + Guid.NewGuid().ToString("N"));

    public GameImporterTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private string MakeWindowsGame(string relative)
    {
        var folder = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.Combine(folder, "LoE_Data"));
        File.WriteAllText(Path.Combine(folder, "LoE.exe"), "exe");
        File.WriteAllText(Path.Combine(folder, "LoE_Data", "data.bin"), "data");
        return folder;
    }

    private string Target => Path.Combine(_root, "Launcher", "Game");
    private string LauncherFolder => Path.Combine(_root, "Launcher");

    [Fact]
    public void FindGameFolder_ShouldAcceptGameFolderOrOldLauncherFolder()
    {
        var game = MakeWindowsGame(Path.Combine("OldLauncher", "Game"));

        GameImporter.FindGameFolder(game, OS.WindowsX64).Should().Be(game);
        GameImporter.FindGameFolder(Path.Combine(_root, "OldLauncher"), OS.WindowsX64).Should().Be(game);
        GameImporter.FindGameFolder(_root, OS.WindowsX64).Should().BeNull();
    }

    [Fact]
    public void ContainsGame_ShouldMatchLowercaseWindowsBuild()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var folder = Path.Combine(_root, "Lowercase");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "loe.exe"), "exe");

        GameImporter.ContainsGame(folder, OS.WindowsX64).Should().BeTrue();
    }

    [Fact]
    public void FindGameFolder_ShouldLookInsideOldMacAppBundle()
    {
        var game = Path.Combine(_root, "LoE Launcher.app", "Contents", "MacOS", "Game");
        Directory.CreateDirectory(Path.Combine(game, "LoE.app"));

        GameImporter.FindGameFolder(Path.Combine(_root, "LoE Launcher.app"), OS.MacArm).Should().Be(game);
        GameImporter.FindGameFolder(Path.Combine(game, "LoE.app"), OS.MacArm).Should().Be(game);
    }

    [Fact]
    public void Check_ShouldRejectLauncherFolderAndExistingInstall()
    {
        var game = MakeWindowsGame("Old");

        GameImporter.Check(null, Target).Should().Be(ImportCheck.GameNotFound);
        GameImporter.Check(Target, Target).Should().Be(ImportCheck.SameFolder);
        GameImporter.Check(LauncherFolder, Target).Should().Be(ImportCheck.SameFolder);
        GameImporter.Check(game, Target).Should().Be(ImportCheck.Ok);

        Directory.CreateDirectory(Target);
        GameImporter.Check(game, Target).Should().Be(ImportCheck.Ok);

        File.WriteAllText(Path.Combine(Target, "LoE.exe"), "exe");
        GameImporter.Check(game, Target).Should().Be(ImportCheck.AlreadyInstalled);
    }

    [Fact]
    public void MoveInstall_ShouldMoveEverythingIntoTarget()
    {
        var game = MakeWindowsGame(Path.Combine("Old", "Game"));
        Directory.CreateDirectory(Target);

        GameImporter.MoveInstall(game, Target);

        Directory.Exists(game).Should().BeFalse();
        File.ReadAllText(Path.Combine(Target, "LoE.exe")).Should().Be("exe");
        File.ReadAllText(Path.Combine(Target, "LoE_Data", "data.bin")).Should().Be("data");
    }

    [Fact]
    public void FindInstalls_ShouldFindOldInstallsButNotTheLauncherOwn()
    {
        var old = MakeWindowsGame(Path.Combine("Desktop", "LoE-Launcher", "Game"));
        MakeWindowsGame(Path.Combine("Launcher", "Game-Dev"));
        MakeWindowsGame(Path.Combine("Deep", "a", "b", "c", "d", "Game"));

        var found = GameImporter.FindInstalls([(_root, 3)], OS.WindowsX64, Target, LauncherFolder);

        found.Should().ContainSingle().Which.Should().Be(old);
    }

    [Fact]
    public void FindInstalls_ShouldCheckLegacyDataFolderAndPutNewestFirst()
    {
        var older = MakeWindowsGame(Path.Combine("Desktop", "LoE", "Game"));
        var newer = MakeWindowsGame(Path.Combine("AppData", "Roaming", "LoE Launcher", "Game"));
        File.SetLastWriteTimeUtc(Path.Combine(older, "LoE.exe"), new DateTime(2025, 10, 1));
        Directory.SetLastWriteTimeUtc(Path.Combine(older, "LoE_Data"), new DateTime(2025, 10, 1));
        Directory.SetLastWriteTimeUtc(older, new DateTime(2025, 10, 1));

        var roots = new[] { (Path.Combine(_root, "Desktop"), 3), (Path.Combine(_root, "AppData", "Roaming", "LoE Launcher"), 0) };
        var found = GameImporter.FindInstalls(roots, OS.WindowsX64, Target, LauncherFolder);

        found.Should().Equal(newer, older);
    }
}
