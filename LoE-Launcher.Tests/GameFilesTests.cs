using FluentAssertions;
using LoE_Launcher.Services;

namespace LoE_Launcher.Tests;

public class GameFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "loe-gf-" + Guid.NewGuid().ToString("N"));
    private string _game = "";

    public GameFilesTests()
    {
        _game = Path.Combine(_root, "Game");
        Directory.CreateDirectory(_game);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private static string P(params string[] parts) => Path.Combine(parts);

    private void Touch(string relative, string content = "x")
    {
        var full = Path.Combine(_game, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private bool Exists(string relative) => File.Exists(Path.Combine(_game, relative));

    [Fact]
    public void Delete_ShouldRemoveOnlyListedFilesTheirTempFilesAndTheManifest()
    {
        var bin = P("LoE_Data", "a.bin");
        Touch("LoE.exe");
        Touch(bin);
        Touch(P("LoE_Data", "a.bin.gz.part"));
        Touch(InstalledFiles.ManifestName);
        Touch(P("Homework", "essay.docx"));
        Touch("other.jar");
        Touch("notes.part");

        var deleted = GameFiles.Delete(_game, ["LoE.exe", bin]);

        deleted.Should().BeGreaterThanOrEqualTo(2);
        Exists("LoE.exe").Should().BeFalse();
        Exists(bin).Should().BeFalse();
        Exists(InstalledFiles.ManifestName).Should().BeFalse();
        Directory.Exists(Path.Combine(_game, "LoE_Data")).Should().BeFalse();
        Exists(P("Homework", "essay.docx")).Should().BeTrue();
        Exists("other.jar").Should().BeTrue();
        Exists("notes.part").Should().BeTrue();
    }

    [Fact]
    public void Delete_ShouldNeverTouchAnythingOutsideTheGameFolder()
    {
        var outside = Path.Combine(_root, "outside.txt");
        File.WriteAllText(outside, "x");

        GameFiles.Delete(_game, [P("..", "outside.txt"), outside]).Should().Be(0);

        File.Exists(outside).Should().BeTrue();
    }

    [Fact]
    public void Delete_ShouldKeepFoldersThatStillHaveOtherFiles()
    {
        var bin = P("LoE_Data", "a.bin");
        Touch(bin);
        Touch(P("LoE_Data", "mine.txt"));

        GameFiles.Delete(_game, [bin]);

        Exists(P("LoE_Data", "mine.txt")).Should().BeTrue();
    }

    [Fact]
    public void Delete_ShouldHandleUnicodeAndSpaces()
    {
        _game = Path.Combine(_root, "Spiele", "Légendes d'Équestria");
        var file = P("LoE_Data", "größe datei.bin");
        Touch(file);
        Touch(file + ".gz.part");

        GameFiles.Delete(_game, [file]).Should().BeGreaterThanOrEqualTo(1);

        Exists(file).Should().BeFalse();
        Directory.Exists(Path.Combine(_game, "LoE_Data")).Should().BeFalse();
        Directory.Exists(Path.Combine(_root, "Spiele")).Should().BeTrue();
    }

    [Fact]
    public void Listed_ShouldPreferManifestThenPatchListThenNull()
    {
        GameFiles.Listed(_game, null).Should().BeNull();
        GameFiles.Listed(_game, ["patch.bin"]).Should().Equal("patch.bin");

        InstalledFiles.Save(_game, ["manifest.bin"]);

        GameFiles.Listed(_game, ["patch.bin"]).Should().Equal("manifest.bin");
    }

    [Fact]
    public void Size_ShouldCountOnlyListedFiles()
    {
        Touch("a.bin", "12345");
        Touch("b.bin", "123");
        Touch("unlisted.bin", "1234567890");

        GameFiles.Size(_game, ["a.bin", "b.bin", "missing.bin"]).Should().Be(8);
    }

    [Fact]
    public void DeleteTemp_ShouldLeaveUnrelatedJarGzAndPartFiles()
    {
        var bin = P("LoE_Data", "data.bin");
        Touch(bin);
        Touch(bin + ".gz");
        Touch(bin + ".gz.part");
        Touch(bin + ".zs-old");
        Touch(P("LoE_Data", "other.jar"));
        Touch(P("LoE_Data", "notes.part"));
        Touch(P("LoE_Data", "data.binary.gz"));

        GameFiles.DeleteTemp(_game, [bin]).Should().Be(3);

        Exists(bin).Should().BeTrue();
        Exists(bin + ".gz").Should().BeFalse();
        Exists(bin + ".gz.part").Should().BeFalse();
        Exists(bin + ".zs-old").Should().BeFalse();
        Exists(P("LoE_Data", "other.jar")).Should().BeTrue();
        Exists(P("LoE_Data", "notes.part")).Should().BeTrue();
        Exists(P("LoE_Data", "data.binary.gz")).Should().BeTrue();
    }

    [Fact]
    public void TempFilesFor_ShouldMatchOnlyTheExactTempNamesOfAListedFile()
    {
        var bin = P("LoE_Data", "data.bin");
        Touch(bin);
        string[] temps = ["data.bin.gz.part", "data.bin.gz.zsync", "data.bin.zs-old", "data.bin.jar", "data.bin.tmp"];
        string[] others = ["data.bin.backup.part", "data.bin.old.jar", "data.bin.notes.gz"];
        foreach (var name in temps.Concat(others))
        {
            Touch(P("LoE_Data", name));
        }

        var found = GameFiles.TempFilesFor(_game, [bin]).Select(Path.GetFileName).ToList();

        found.Should().BeEquivalentTo(temps);
        GameFiles.DeleteTemp(_game, [bin]).Should().Be(temps.Length);
        others.Should().OnlyContain(n => Exists(P("LoE_Data", n)));
    }

    [Fact]
    public void TempFilesFor_ShouldFollowTheHostsCaseRules()
    {
        Touch("data.bin");
        Touch("data.bin.PART");

        var found = GameFiles.TempFilesFor(_game, ["DATA.BIN"]).ToList();

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            found.Should().HaveCount(1);
        }
        else
        {
            found.Should().BeEmpty();
            GameFiles.TempFilesFor(_game, ["data.bin"]).Should().BeEmpty();
        }
    }

    [Fact]
    public void Delete_ShouldNotFollowALinkedFolderOutOfTheGameFolder()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var outside = Path.Combine(_root, "Elsewhere");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "a.bin"), "x");
        File.WriteAllText(Path.Combine(outside, "a.bin.part"), "x");
        Directory.CreateSymbolicLink(Path.Combine(_game, "LoE_Data"), outside);

        GameFiles.Delete(_game, [P("LoE_Data", "a.bin")]);
        GameFiles.DeleteTemp(_game, [P("LoE_Data", "a.bin")]).Should().Be(0);
        GameFiles.Size(_game, [P("LoE_Data", "a.bin")]).Should().Be(0);

        File.Exists(Path.Combine(outside, "a.bin")).Should().BeTrue();
        File.Exists(Path.Combine(outside, "a.bin.part")).Should().BeTrue();
    }
}
