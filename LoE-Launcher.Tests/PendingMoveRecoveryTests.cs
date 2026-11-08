using FluentAssertions;
using LoE_Launcher.Core;

namespace LoE_Launcher.Tests;

public class PendingMoveRecoveryTests : IDisposable
{
    private readonly string _target = Path.Combine(Path.GetTempPath(), "loe-pending-" + Guid.NewGuid().ToString("N"));

    public PendingMoveRecoveryTests() => Directory.CreateDirectory(_target);

    public void Dispose()
    {
        if (Directory.Exists(_target))
        {
            Directory.Delete(_target, true);
        }
    }

    [Fact]
    public void Run_ShouldDeleteOnlyListedFilesAndReportDone()
    {
        File.WriteAllText(Path.Combine(_target, "game.dll"), "x");
        File.WriteAllText(Path.Combine(_target, "mine.txt"), "x");

        var done = PendingMoveRecovery.Run(_target, ["game.dll"]);

        done.Should().BeTrue();
        File.Exists(Path.Combine(_target, "game.dll")).Should().BeFalse();
        File.Exists(Path.Combine(_target, "mine.txt")).Should().BeTrue();
    }

    [Fact]
    public void Run_ShouldDeleteNothingAndReportNotDoneWithoutAFileList()
    {
        File.WriteAllText(Path.Combine(_target, "game.dll"), "x");

        var done = PendingMoveRecovery.Run(_target, null);

        done.Should().BeFalse();
        File.Exists(Path.Combine(_target, "game.dll")).Should().BeTrue();
    }

    [Fact]
    public void Run_ShouldReportDoneForAnEmptyListWithoutTouchingFiles()
    {
        File.WriteAllText(Path.Combine(_target, "mine.txt"), "x");

        var done = PendingMoveRecovery.Run(_target, []);

        done.Should().BeTrue();
        File.Exists(Path.Combine(_target, "mine.txt")).Should().BeTrue();
    }

    [Fact]
    public void Run_ShouldReportDoneWhenTheTargetDoesNotExist()
    {
        Directory.Delete(_target, true);

        PendingMoveRecovery.Run(_target, ["game.dll"]).Should().BeTrue();
    }
}
