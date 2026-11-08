using FluentAssertions;
using LoE_Launcher.Core;

namespace LoE_Launcher.Tests;

public class GameMoveSequenceTests
{
    private const string Old = "/games/old";
    private const string Target = "/games/new/Legends of Equestria";

    private readonly Settings _settings = new() { GameFolder = Old };
    private readonly List<string> _steps = [];

    private void Save() => _steps.Add($"save active={_settings.ActiveCustomGameFolder ?? "default"} pending={_settings.PendingMoveTarget ?? "none"}");

    private void Run(Action? save = null, Action? copy = null, string? newActive = Target) =>
        GameMoveSequence.Run(_settings, Target, newActive, save ?? Save, copy ?? (() => _steps.Add("copy")),
            () => _steps.Add("delete old"));

    [Fact]
    public void Run_ShouldSavePendingThenCopyThenSwitchAndSaveBeforeDeletingTheOldCopy()
    {
        Run();

        _steps.Should().Equal(
            $"save active={Old} pending={Target}",
            "copy",
            $"save active={Target} pending=none",
            "delete old");
    }

    [Fact]
    public void Run_ShouldClearPendingKeepTheOldFolderAndNotDeleteWhenTheCopyFails()
    {
        var act = () => Run(copy: () =>
        {
            _steps.Add("copy");
            throw new IOException("disk full");
        });

        act.Should().Throw<IOException>().WithMessage("disk full");
        _steps.Should().Equal(
            $"save active={Old} pending={Target}",
            "copy",
            $"save active={Old} pending=none");
        _settings.ActiveCustomGameFolder.Should().Be(Old);
        _settings.PendingMoveTarget.Should().BeNull();
    }

    [Fact]
    public void Run_ShouldNotCopyWhenThePendingMoveCantBeSaved()
    {
        var act = () => Run(save: () => throw new IOException("read-only"));

        act.Should().Throw<IOException>();
        _steps.Should().BeEmpty();
        _settings.ActiveCustomGameFolder.Should().Be(Old);
    }

    [Fact]
    public void Run_ShouldStayOnTheOldFolderAndNotDeleteWhenTheSwitchCantBeSaved()
    {
        var saves = 0;
        var act = () => Run(save: () =>
        {
            if (++saves == 2)
            {
                throw new IOException("read-only");
            }

            Save();
        });

        act.Should().Throw<IOException>();
        _steps.Should().Equal($"save active={Old} pending={Target}", "copy");
        _settings.ActiveCustomGameFolder.Should().Be(Old);
        _settings.PendingMoveTarget.Should().Be(Target);
    }

    [Fact]
    public void Run_ShouldStoreTheGivenSettingValue()
    {
        Run(newActive: null);

        _settings.ActiveCustomGameFolder.Should().BeNull();
        _steps.Should().Contain("save active=default pending=none");
    }

    [Fact]
    public void Run_ShouldKeepTheCopyErrorWhenClearingThePendingMoveFails()
    {
        var saves = 0;
        var act = () => Run(
            save: () =>
            {
                if (++saves == 2)
                {
                    throw new UnauthorizedAccessException("settings");
                }
            },
            copy: () => throw new IOException("disk full"));

        act.Should().Throw<IOException>().WithMessage("disk full");
    }
}
