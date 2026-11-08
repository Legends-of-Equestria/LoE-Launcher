using FluentAssertions;
using LoE_Launcher.Services;

namespace LoE_Launcher.Tests;

public class GameMoverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "loe-gm-" + Guid.NewGuid().ToString("N"));
    private readonly string _source;
    private readonly string _target;

    public GameMoverTests()
    {
        _source = Path.Combine(_root, "Old");
        _target = Path.Combine(_root, "New");
        Directory.CreateDirectory(_source);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            foreach (var f in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    if (!OperatingSystem.IsWindows())
                    {
                        File.SetUnixFileMode(f, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    }
                }
                catch (IOException)
                {
                }
            }

            Directory.Delete(_root, true);
        }
    }

    private void Touch(string relative, string content = "x")
    {
        var full = Path.Combine(_source, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    [Fact]
    public void CopyVerified_ShouldCopyListedFilesAndManifestOnly()
    {
        var bin = Path.Combine("LoE_Data", "a.bin");
        Touch("LoE.exe", "exe");
        Touch(bin, "bin");
        Touch(InstalledFiles.ManifestName, "[]");
        Touch(Path.Combine("Homework", "essay.docx"), "essay");

        GameMover.CopyVerified(_source, _target, ["LoE.exe", bin]);

        File.ReadAllText(Path.Combine(_target, "LoE.exe")).Should().Be("exe");
        File.ReadAllText(Path.Combine(_target, bin)).Should().Be("bin");
        File.Exists(Path.Combine(_target, InstalledFiles.ManifestName)).Should().BeTrue();
        File.Exists(Path.Combine(_target, "Homework", "essay.docx")).Should().BeFalse();
        File.Exists(Path.Combine(_source, "Homework", "essay.docx")).Should().BeTrue();
        File.Exists(Path.Combine(_source, "LoE.exe")).Should().BeTrue();
    }

    [Fact]
    public void CopyVerified_ShouldRollBackWhenASourceFileCantBeRead()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Touch("a.bin");
        Touch("b.bin");
        Touch("c.bin");
        File.SetUnixFileMode(Path.Combine(_source, "b.bin"), UnixFileMode.None);

        var act = () => GameMover.CopyVerified(_source, _target, ["a.bin", "b.bin", "c.bin"]);

        act.Should().Throw<Exception>();
        File.Exists(Path.Combine(_target, "a.bin")).Should().BeFalse();
        File.Exists(Path.Combine(_target, "b.bin")).Should().BeFalse();
        File.Exists(Path.Combine(_source, "a.bin")).Should().BeTrue();
        File.Exists(Path.Combine(_source, "c.bin")).Should().BeTrue();
    }

    [Fact]
    public void CopyVerified_ShouldRollBackWhenAListedFileIsMissingOrOutside()
    {
        Touch("a.bin");

        var missing = () => GameMover.CopyVerified(_source, _target, ["a.bin", "nope.bin"]);
        var outside = () => GameMover.CopyVerified(_source, _target, ["a.bin", Path.Combine("..", "x")]);

        missing.Should().Throw<IOException>();
        File.Exists(Path.Combine(_target, "a.bin")).Should().BeFalse();
        outside.Should().Throw<IOException>();
        File.Exists(Path.Combine(_target, "a.bin")).Should().BeFalse();
    }

    [Fact]
    public void CopyVerified_ShouldRefuseANonEmptyTargetAndLeaveEverythingAlone()
    {
        Touch("a.bin");
        Directory.CreateDirectory(_target);
        File.WriteAllText(Path.Combine(_target, "mine.txt"), "mine");

        var act = () => GameMover.CopyVerified(_source, _target, ["a.bin"]);

        act.Should().Throw<IOException>().WithMessage("*isn't empty*");
        Directory.GetFileSystemEntries(_target).Should().ContainSingle().Which.Should().EndWith("mine.txt");
        File.ReadAllText(Path.Combine(_target, "mine.txt")).Should().Be("mine");
        File.Exists(Path.Combine(_source, "a.bin")).Should().BeTrue();
    }

    [Fact]
    public void CopyVerified_ShouldCopyIntoAnExistingEmptyTarget()
    {
        Touch("a.bin", "data");
        Directory.CreateDirectory(_target);

        GameMover.CopyVerified(_source, _target, ["a.bin"]);

        File.ReadAllText(Path.Combine(_target, "a.bin")).Should().Be("data");
    }

    [Fact]
    public void CopyVerified_ShouldReportProgress()
    {
        Touch("a.bin");
        Touch("b.bin");
        var calls = new List<(int, int)>();

        GameMover.CopyVerified(_source, _target, ["a.bin", "b.bin"], (done, total) => calls.Add((done, total)));

        calls.Should().Equal((1, 2), (2, 2));
    }

    [Fact]
    public void CopyVerified_ShouldHandleUnicodeAndSpaces()
    {
        var name = Path.Combine("Näme with spaces", "ファイル é.bin");
        Touch(name, "data");

        GameMover.CopyVerified(_source, _target, [name]);

        File.ReadAllText(Path.Combine(_target, name)).Should().Be("data");
    }

    [Fact]
    public void HasSpaceFor_ShouldBeFalseForAnAbsurdSize()
    {
        GameMover.HasSpaceFor(_root, long.MaxValue / 2).Should().BeFalse();
        GameMover.HasSpaceFor(_root, 1).Should().BeTrue();
        GameMover.HasSpaceFor(_target, 1).Should().BeTrue();
    }
}
