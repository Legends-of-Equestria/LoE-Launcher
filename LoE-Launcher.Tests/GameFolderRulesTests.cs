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
}
