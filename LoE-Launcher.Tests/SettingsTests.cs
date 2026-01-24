using FluentAssertions;
using LoE_Launcher;
using Xunit;

namespace LoE_Launcher.Tests;

public class SettingsTests
{
    [Theory]
    [InlineData("1.0.0", "https://patches.legendsofequestria.com/zsync/1.0.0/")]
    [InlineData("2.5.1", "https://patches.legendsofequestria.com/zsync/2.5.1/")]
    [InlineData("test-version", "https://patches.legendsofequestria.com/zsync/test-version/")]
    public void FormatZsyncLocation_ShouldReturnCorrectUrl(string version, string expected)
    {
        // Arrange
        var settings = new Settings();

        // Act
        var result = settings.FormatZsyncLocation(version);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void FormatZsyncLocation_ShouldAddTrailingSlashIfMissing()
    {
        // Arrange
        var settings = new Settings
        {
            ZsyncLocation = "http://example.com/zsync/{version}"
        };

        // Act
        var result = settings.FormatZsyncLocation("1.0.0");

        // Assert
        result.Should().Be("http://example.com/zsync/1.0.0/");
        result.Should().EndWith("/");
    }

    [Fact]
    public void FormatZsyncLocation_ShouldNotAddExtraSlashIfAlreadyPresent()
    {
        // Arrange
        var settings = new Settings
        {
            ZsyncLocation = "http://example.com/zsync/{version}/"
        };

        // Act
        var result = settings.FormatZsyncLocation("1.0.0");

        // Assert
        result.Should().Be("http://example.com/zsync/1.0.0/");
        result.Should().NotEndWith("//");
    }
}