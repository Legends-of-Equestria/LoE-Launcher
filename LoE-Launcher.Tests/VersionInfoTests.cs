using System;
using FluentAssertions;
using Models;
using Xunit;

namespace LoE_Launcher.Tests;

public class VersionInfoTests
{
    [Theory]
    [InlineData("1.2.3.4", 1, 2, 3, 4)]
    [InlineData("2.0.1", 2, 0, 1, -1)]
    [InlineData("5.7", 5, 7, -1, -1)]
    [InlineData("0.0.0.0", 0, 0, 0, 0)]
    public void ImplicitStringConversion_WithValidVersionString_ShouldParseCorrectly(
        string versionString, int expectedMajor, int expectedMinor, int expectedBuild, int expectedRevision)
    {
        // Act
        VersionInfo result = versionString;

        // Assert
        result.Major.Should().Be(expectedMajor);
        result.Minor.Should().Be(expectedMinor);
        result.Build.Should().Be(expectedBuild);
        result.Revision.Should().Be(expectedRevision);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("1.2.3.4.5")]
    [InlineData("")]
    [InlineData("1.2.-3")]
    [InlineData("abc.def")]
    public void ImplicitStringConversion_WithInvalidVersionString_ShouldThrowFormatException(string invalidVersionString)
    {
        // Act & Assert
        var action = () => { VersionInfo result = invalidVersionString; };
        action.Should().Throw<FormatException>()
            .WithMessage($"The version string '{invalidVersionString}' is not valid.");
    }

    [Theory]
    [InlineData(1, 2, 3, 4)]
    [InlineData(2, 0, 1, -1)]
    [InlineData(5, 7, -1, -1)]
    [InlineData(0, 0, 0, 0)]
    public void ToSystemVersion_ShouldCreateCorrectVersionObject(int major, int minor, int build, int revision)
    {
        // Arrange
        var versionInfo = new VersionInfo
        {
            Major = major,
            Minor = minor,
            Build = build,
            Revision = revision
        };

        // Act
        var result = versionInfo.ToSystemVersion();

        // Assert
        result.Major.Should().Be(major);
        result.Minor.Should().Be(minor);
        
        if (build >= 0 && revision >= 0)
        {
            result.Build.Should().Be(build);
            result.Revision.Should().Be(revision);
        }
        else if (build >= 0)
        {
            result.Build.Should().Be(build);
            result.Revision.Should().Be(-1);
        }
        else
        {
            result.Build.Should().Be(-1);
            result.Revision.Should().Be(-1);
        }
    }

    [Fact]
    public void ToSystemVersion_WithBuildAndRevision_ShouldUse4PartVersion()
    {
        // Arrange
        var versionInfo = new VersionInfo { Major = 1, Minor = 2, Build = 3, Revision = 4 };

        // Act
        var result = versionInfo.ToSystemVersion();

        // Assert
        result.ToString().Should().Be("1.2.3.4");
    }

    [Fact]
    public void ToSystemVersion_WithBuildOnly_ShouldUse3PartVersion()
    {
        // Arrange
        var versionInfo = new VersionInfo { Major = 1, Minor = 2, Build = 3, Revision = -1 };

        // Act
        var result = versionInfo.ToSystemVersion();

        // Assert
        result.ToString().Should().Be("1.2.3");
    }

    [Fact]
    public void ToSystemVersion_WithoutBuildAndRevision_ShouldUse2PartVersion()
    {
        // Arrange
        var versionInfo = new VersionInfo { Major = 1, Minor = 2, Build = -1, Revision = -1 };

        // Act
        var result = versionInfo.ToSystemVersion();

        // Assert
        result.ToString().Should().Be("1.2");
    }

    [Theory]
    [InlineData("1.2.3.4")]
    [InlineData("2.0.1")]
    [InlineData("5.7")]
    public void FromSystemVersion_ShouldCreateCorrectVersionInfo(string versionString)
    {
        // Arrange
        var systemVersion = Version.Parse(versionString);

        // Act
        var result = VersionInfo.FromSystemVersion(systemVersion);

        // Assert
        result.Major.Should().Be(systemVersion.Major);
        result.Minor.Should().Be(systemVersion.Minor);
        result.Build.Should().Be(systemVersion.Build);
        result.Revision.Should().Be(systemVersion.Revision);
        result.MajorRevision.Should().Be(systemVersion.MajorRevision);
        result.MinorRevision.Should().Be(systemVersion.MinorRevision);
    }

    [Fact]
    public void ToString_ShouldReturnSystemVersionString()
    {
        // Arrange
        var versionInfo = new VersionInfo { Major = 1, Minor = 2, Build = 3, Revision = 4 };

        // Act
        var result = versionInfo.ToString();

        // Assert
        result.Should().Be("1.2.3.4");
    }

    [Fact]
    public void RoundTripConversion_ShouldPreserveVersionInfo()
    {
        // Arrange
        var original = "2.5.1.0";

        // Act
        VersionInfo versionInfo = original;
        var systemVersion = versionInfo.ToSystemVersion();
        var backToVersionInfo = VersionInfo.FromSystemVersion(systemVersion);
        string backToString = backToVersionInfo;

        // Assert
        backToString.Should().Be(original);
    }
}