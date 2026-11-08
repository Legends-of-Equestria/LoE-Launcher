using FluentAssertions;
using LoE_Launcher.Services;
using Models.Utils;

namespace LoE_Launcher.Tests;

public class GameFolderRulesTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "loe-rules-" + Guid.NewGuid().ToString("N"));

    private KnownFolders Known => new(
        Path.Combine(_root, "Home"),
        [Path.Combine(_root, "Desktop")],
        [Path.Combine(_root, "Apps")]);

    [Theory]
    [InlineData("Home", true)]
    [InlineData("Desktop", true)]
    [InlineData("Apps", true)]
    [InlineData("Desktop/", true)]
    [InlineData("Desktop/LoE_Win", false)]
    [InlineData("Other", false)]
    public void IsSharedPlace_ShouldMatchKnownFolders(string relative, bool expected)
    {
        GameFolderRules.IsSharedPlace(Path.Combine(_root, relative), Known, OS.X11).Should().Be(expected);
    }

    [Fact]
    public void IsSharedPlace_ShouldTreatARootAsShared()
    {
        GameFolderRules.IsSharedPlace(Path.GetPathRoot(_root)!, Known, OS.X11).Should().BeTrue();
    }

    [Theory]
    [InlineData(OS.WindowsX64, true)]
    [InlineData(OS.MacArm, true)]
    [InlineData(OS.MacIntel, true)]
    [InlineData(OS.X11, false)]
    public void IsSharedPlace_ShouldIgnoreCaseExceptOnLinux(OS os, bool expected)
    {
        GameFolderRules.IsSharedPlace(Path.Combine(_root, "DESKTOP"), Known, os).Should().Be(expected);
    }

    [Fact]
    public void IsSharedPlace_ShouldSkipKnownEntriesThatCannotBeNormalized()
    {
        var known = new KnownFolders(Path.Combine(_root, "Home"), ["bad\0path"], [Path.Combine(_root, "Desktop")]);

        GameFolderRules.IsSharedPlace(Path.Combine(_root, "Desktop"), known, OS.X11).Should().BeTrue();
    }

    [Fact]
    public void Classify_ShouldForbidPersonalFoldersAndEverythingInside()
    {
        foreach (var relative in new[] { "Desktop", "Desktop/Sub/Deeper" })
        {
            var verdict = GameFolderRules.Classify(Path.Combine(_root, relative), Known, OS.X11);

            verdict.Kind.Should().Be(FolderKind.Forbidden);
            verdict.Reason.Should().Be(GameFolderRules.PersonalReason);
        }
    }

    [Fact]
    public void Classify_ShouldForbidSystemFoldersAndEverythingInside()
    {
        foreach (var relative in new[] { "Apps", "Apps/LoE" })
        {
            var verdict = GameFolderRules.Classify(Path.Combine(_root, relative), Known, OS.X11);

            verdict.Kind.Should().Be(FolderKind.Forbidden);
            verdict.Reason.Should().Be(GameFolderRules.SystemReason);
        }
    }

    [Fact]
    public void Classify_ShouldForbidUncPaths()
    {
        var verdict = GameFolderRules.Classify(@"\\server\share\Games", Known, OS.WindowsX64);

        verdict.Kind.Should().Be(FolderKind.Forbidden);
        verdict.Reason.Should().Be(GameFolderRules.NetworkReason);
    }

    [Fact]
    public void Classify_ShouldAllowHomeAndRootsOnlyAsParent()
    {
        GameFolderRules.Classify(Path.Combine(_root, "Home"), Known, OS.X11).Kind.Should().Be(FolderKind.ParentOnly);
        GameFolderRules.Classify(Path.GetPathRoot(_root)!, Known, OS.X11).Kind.Should().Be(FolderKind.ParentOnly);
        GameFolderRules.Classify(Path.Combine(_root, "Home", "Games"), Known, OS.X11).Kind.Should().Be(FolderKind.Allowed);
    }

    [Theory]
    [InlineData(OS.WindowsX64, FolderKind.Forbidden)]
    [InlineData(OS.MacArm, FolderKind.Forbidden)]
    [InlineData(OS.X11, FolderKind.Allowed)]
    public void Classify_ShouldUseOsCaseRules(OS os, FolderKind expected)
    {
        GameFolderRules.Classify(Path.Combine(_root, "DESKTOP", "x"), Known, os).Kind.Should().Be(expected);
    }

    [Theory]
    [InlineData("Personal/../Desktop/")]
    [InlineData("Desktop//")]
    [InlineData("Desktop/Sub/..")]
    public void Classify_ShouldNormalizeTrailingSeparatorsAndDotDot(string relative)
    {
        GameFolderRules.Classify(Path.Combine(_root, relative), Known, OS.X11).Kind.Should().Be(FolderKind.Forbidden);
    }

    [Fact]
    public void Classify_ShouldSkipKnownEntriesThatCannotBeNormalized()
    {
        var known = new KnownFolders(Path.Combine(_root, "Home"), ["bad\0path"], [Path.Combine(_root, "Apps")]);

        GameFolderRules.Classify(Path.Combine(_root, "Apps", "x"), known, OS.X11).Reason.Should().Be(GameFolderRules.SystemReason);
    }

    [Fact]
    public void IsSameOrInside_ShouldHandleRootsAndSiblingPrefixes()
    {
        var root = Path.GetPathRoot(_root)!;

        GameFolderRules.IsSameOrInside(_root, root, OS.X11).Should().BeTrue();
        GameFolderRules.IsSameOrInside(Path.Combine(_root, "Desktop2"), Path.Combine(_root, "Desktop"), OS.X11).Should().BeFalse();
        GameFolderRules.IsSameOrInside(Path.Combine(_root, "Desktop", "a"), Path.Combine(_root, "Desktop"), OS.X11).Should().BeTrue();
    }
}
