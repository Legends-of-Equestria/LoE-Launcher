using FluentAssertions;
using LoE_Launcher.Core;
using LoE_Launcher.Services;

namespace LoE_Launcher.Tests;

public class LocationPlanTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "loe-plan-" + Guid.NewGuid().ToString("N"));

    public LocationPlanTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    public static TheoryData<LocationChoice, bool, LocationStep> Choices => new()
    {
        { new LocationChoice.Refused("no"), true, LocationStep.Refuse },
        { new LocationChoice.Refused("no"), false, LocationStep.Refuse },
        { new LocationChoice.Unchanged(), true, LocationStep.Nothing },
        { new LocationChoice.Unchanged(), false, LocationStep.Nothing },
        { new LocationChoice.NewFolder("/x"), false, LocationStep.Switch },
        { new LocationChoice.UseInPlace("/x"), false, LocationStep.Switch },
        { new LocationChoice.NewFolder("/x"), true, LocationStep.Move },
        { new LocationChoice.UseInPlace("/x"), true, LocationStep.UseInPlace }
    };

    [Theory]
    [MemberData(nameof(Choices))]
    public void Decide_ShouldMapEachChoiceToItsStep(LocationChoice choice, bool hasGame, LocationStep expected)
    {
        LocationPlan.Decide(choice, hasGame).Should().Be(expected);
    }

    [Fact]
    public void HasGame_ShouldBeTrueForAFolderWithFilesInAKnownState()
    {
        File.WriteAllText(Path.Combine(_root, "LoE.exe"), "x");

        LocationPlan.HasGame(GameState.UpToDate, _root).Should().BeTrue();
        LocationPlan.HasGame(GameState.UpdateAvailable, _root).Should().BeTrue();
    }

    [Fact]
    public void HasGame_ShouldBeFalseWhenNotInstalledOrTheFolderIsMissing()
    {
        File.WriteAllText(Path.Combine(_root, "LoE.exe"), "x");

        LocationPlan.HasGame(GameState.NotFound, _root).Should().BeFalse();
        LocationPlan.HasGame(GameState.FolderMissing, _root).Should().BeFalse();
        LocationPlan.HasGame(GameState.UpToDate, Path.Combine(_root, "missing")).Should().BeFalse();
    }

    [Fact]
    public void HasGame_ShouldBeFalseForAnEmptyFolder()
    {
        LocationPlan.HasGame(GameState.Offline, _root).Should().BeFalse();
    }
}
