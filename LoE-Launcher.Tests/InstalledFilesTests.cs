using FluentAssertions;
using LoE_Launcher.Services;

namespace LoE_Launcher.Tests;

public class InstalledFilesTests : IDisposable
{
    private readonly string _game = Path.Combine(Path.GetTempPath(), "loe-files-" + Guid.NewGuid().ToString("N"));

    public InstalledFilesTests() => Directory.CreateDirectory(_game);

    public void Dispose()
    {
        if (Directory.Exists(_game))
        {
            Directory.Delete(_game, true);
        }
    }

    private static string P(params string[] parts) => Path.Combine(parts);

    private void Touch(string relative)
    {
        var full = Path.Combine(_game, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "x");
    }

    private bool Exists(string relative) => File.Exists(Path.Combine(_game, relative));

    [Fact]
    public void FirstRun_ShouldOnlyRecordFiles()
    {
        Touch("LoE.exe");
        Touch(P("loe_Data", "old.resS"));

        InstalledFiles.RemoveDropped(_game, ["LoE.exe"]).Should().BeEmpty();

        Exists(P("loe_Data", "old.resS")).Should().BeTrue();
        InstalledFiles.Load(_game).Should().Equal("LoE.exe");
    }

    [Fact]
    public void LaterRun_ShouldRemoveDroppedFilesButNotUntrackedOnes()
    {
        var files = new[] { "LoE.exe", P("loe_Data", "a.assets"), P("loe_Data", "StreamingAssets", "aa", "catalog.json") };
        foreach (var file in files)
        {
            Touch(file);
        }

        Touch(P("BepInEx", "plugins", "mod.dll"));
        InstalledFiles.Save(_game, files);

        var removed = InstalledFiles.RemoveDropped(_game, ["LoE.exe", P("loe_Data", "a.assets")]);

        removed.Should().Equal(P("loe_Data", "StreamingAssets", "aa", "catalog.json"));
        Directory.Exists(Path.Combine(_game, "loe_Data", "StreamingAssets")).Should().BeFalse();
        Exists(P("BepInEx", "plugins", "mod.dll")).Should().BeTrue();
        InstalledFiles.Load(_game).Should().BeEquivalentTo("LoE.exe", P("loe_Data", "a.assets"));
    }

    [Fact]
    public void ShouldSkipCleanupWhenPatchLooksWrong()
    {
        var files = new[] { "LoE.exe", "a", "b", "c" };
        foreach (var file in files)
        {
            Touch(file);
        }

        InstalledFiles.Save(_game, files);

        InstalledFiles.RemoveDropped(_game, ["LoE.exe"]).Should().BeEmpty();
        InstalledFiles.RemoveDropped(_game, []).Should().BeEmpty();

        files.Should().OnlyContain(f => Exists(f));
    }

    [Fact]
    public void SweepJunk_ShouldRemoveUnityDoNotShipFoldersAndPartialDownloads()
    {
        Touch(P("Legends of Equestria_BurstDebugInformation_DoNotShip", "Data", "lib.txt"));
        Touch(P("loe_Data", "resources.resource.jar.part"));
        Touch(P("loe_Data", "resources.resource"));

        InstalledFiles.SweepJunk(_game, removePartialDownloads: false);

        Directory.Exists(Path.Combine(_game, "Legends of Equestria_BurstDebugInformation_DoNotShip")).Should().BeFalse();
        Exists(P("loe_Data", "resources.resource.jar.part")).Should().BeTrue();

        InstalledFiles.SweepJunk(_game, removePartialDownloads: true);

        Exists(P("loe_Data", "resources.resource.jar.part")).Should().BeFalse();
        Exists(P("loe_Data", "resources.resource")).Should().BeTrue();
    }
}
