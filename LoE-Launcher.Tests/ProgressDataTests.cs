using System;
using FluentAssertions;
using LoE_Launcher.Core;
using Moq;
using Xunit;

namespace LoE_Launcher.Tests;

public class ProgressDataTests
{
    private readonly Mock<Downloader> _mockDownloader;
    private readonly ProgressData _progressData;

    public ProgressDataTests()
    {
        _mockDownloader = new Mock<Downloader>();
        _progressData = new ProgressData(_mockDownloader.Object);
    }

    [Fact]
    public void Count_WithValidIncrement_ShouldUpdateCurrent()
    {
        // Arrange
        _progressData.ResetCounter(10);

        // Act
        _progressData.Count(3);

        // Assert
        _progressData.Current.Should().Be(3);
    }

    [Fact]
    public void Count_WithDefaultIncrement_ShouldIncrementByOne()
    {
        // Arrange
        _progressData.ResetCounter(10);

        // Act
        _progressData.Count();

        // Assert
        _progressData.Current.Should().Be(1);
    }

    [Fact]
    public void Count_WhenExceedsMax_ShouldThrowArithmeticException()
    {
        // Arrange
        _progressData.ResetCounter(5);
        _progressData.SetCount(3);

        // Act & Assert
        var action = () => _progressData.Count(3); // 3 + 3 = 6, which exceeds max of 5
        action.Should().Throw<ArithmeticException>()
            .WithMessage("Current can not be higher than Maximum");
    }

    [Fact]
    public void Count_WhenEqualsMax_ShouldNotThrow()
    {
        // Arrange
        _progressData.ResetCounter(5);
        _progressData.SetCount(3);

        // Act & Assert
        var action = () => _progressData.Count(2); // 3 + 2 = 5, which equals max
        action.Should().NotThrow();
        _progressData.Current.Should().Be(5);
    }

    [Fact]
    public void Count_WithZeroIncrement_ShouldNotChange()
    {
        // Arrange
        _progressData.ResetCounter(10);
        _progressData.SetCount(5);

        // Act
        _progressData.Count(0);

        // Assert
        _progressData.Current.Should().Be(5);
    }

    [Fact]
    public void Count_WithNegativeIncrement_ShouldDecrease()
    {
        // Arrange
        _progressData.ResetCounter(10);
        _progressData.SetCount(5);

        // Act
        _progressData.Count(-2);

        // Assert
        _progressData.Current.Should().Be(3);
    }

    [Fact]
    public void ResetCounter_ShouldResetCurrentToZero()
    {
        // Arrange
        _progressData.SetCount(5);

        // Act
        _progressData.ResetCounter(20);

        // Assert
        _progressData.Current.Should().Be(0);
        _progressData.Max.Should().Be(20);
    }

    [Fact]
    public void ResetCounter_WithChangeFromMarquee_ShouldSetMarqueeToFalse()
    {
        // Arrange
        _progressData.Marquee = true;

        // Act
        _progressData.ResetCounter(10, changeFromMarquee: true);

        // Assert
        _progressData.Marquee.Should().BeFalse();
    }

    [Fact]
    public void ResetCounter_WithoutChangeFromMarquee_ShouldNotChangeMarquee()
    {
        // Arrange
        _progressData.Marquee = true;

        // Act
        _progressData.ResetCounter(10, changeFromMarquee: false);

        // Assert
        _progressData.Marquee.Should().BeTrue();
    }
}