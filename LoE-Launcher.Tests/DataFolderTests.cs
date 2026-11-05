using FluentAssertions;
using LoE_Launcher.Core;

namespace LoE_Launcher.Tests;

public class DataFolderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "loe-data-" + Guid.NewGuid().ToString("N"));

    private string Old => Path.Combine(_root, "Roaming", "LoE Launcher");
    private string New => Path.Combine(_root, "Local", "LoE Launcher");

    public DataFolderTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Fact]
    public void MoveLegacy_ShouldMoveOldDataToNewFolder()
    {
        Directory.CreateDirectory(Path.Combine(Old, "Game"));
        File.WriteAllText(Path.Combine(Old, "settings.json"), "{}");

        DataFolder.MoveLegacy(Old, New, out var note).Should().Be(New);

        note.Should().NotBeNull();
        Directory.Exists(Old).Should().BeFalse();
        File.Exists(Path.Combine(New, "settings.json")).Should().BeTrue();
        Directory.Exists(Path.Combine(New, "Game")).Should().BeTrue();
    }

    [Fact]
    public void MoveLegacy_ShouldUseNewFolderWhenNothingToMove()
    {
        DataFolder.MoveLegacy(Old, New, out var note).Should().Be(New);
        note.Should().BeNull();
    }

    [Fact]
    public void MoveLegacy_ShouldNotOverwriteExistingNewData()
    {
        Directory.CreateDirectory(Old);
        File.WriteAllText(Path.Combine(Old, "settings.json"), "old");
        Directory.CreateDirectory(New);
        File.WriteAllText(Path.Combine(New, "settings.json"), "new");

        DataFolder.MoveLegacy(Old, New, out _).Should().Be(New);

        File.ReadAllText(Path.Combine(New, "settings.json")).Should().Be("new");
        File.Exists(Path.Combine(Old, "settings.json")).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldPreferDataDirArgument()
    {
        var custom = Path.Combine(_root, "custom");

        DataFolder.Resolve(["--data-dir", custom], out _).Should().Be(Path.GetFullPath(custom));
        DataFolder.IsCustom.Should().BeTrue();
    }
}
