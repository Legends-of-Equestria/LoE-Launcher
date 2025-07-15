using System;
using FluentAssertions;
using LoE_Launcher.Core;
using Xunit;

namespace LoE_Launcher.Tests;

public class DownloadStatsTests
{
    [Theory]
    [InlineData(1024, "1.0 KB/s")]
    [InlineData(1048576, "1.0 MB/s")]
    [InlineData(1073741824, "1.0 GB/s")]
    [InlineData(512, "512.0 B/s")]
    [InlineData(2560, "2.5 KB/s")]
    [InlineData(1536, "1.5 KB/s")]
    public void GetFormattedSpeed_ShouldFormatCorrectly(double speedBps, string expected)
    {
        // Arrange
        var stats = new DownloadStats();
        
        var field = typeof(DownloadStats).GetField("_currentSpeedBps", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(stats, speedBps);

        // Act
        var result = stats.GetFormattedSpeed();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(3661, "1h 1m")] // 1 hour, 1 minute, 1 second
    [InlineData(3600, "1h")] // exactly 1 hour
    [InlineData(7200, "2h")] // exactly 2 hours
    [InlineData(61, "1m 1s")] // 1 minute, 1 second
    [InlineData(60, "1m")] // exactly 1 minute
    [InlineData(30, "30s")] // 30 seconds
    [InlineData(601, "10m")] // 10 minutes, 1 second
    public void GetFormattedTimeRemaining_ShouldFormatCorrectly(double seconds, string expected)
    {
        // Arrange
        var stats = new DownloadStats();
        
        var timeRemainingField = typeof(DownloadStats).GetField("_timeRemaining", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var hasValidEstimateField = typeof(DownloadStats).GetField("_hasValidTimeEstimate", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        timeRemainingField?.SetValue(stats, TimeSpan.FromSeconds(seconds));
        hasValidEstimateField?.SetValue(stats, true);

        // Act
        var result = stats.GetFormattedTimeRemaining();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetFormattedTimeRemaining_WithExcessiveTime_ShouldReturnCalculating()
    {
        // Arrange
        var stats = new DownloadStats();
        
        var timeRemainingField = typeof(DownloadStats).GetField("_timeRemaining", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var hasValidEstimateField = typeof(DownloadStats).GetField("_hasValidTimeEstimate", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        timeRemainingField?.SetValue(stats, TimeSpan.FromHours(25));
        hasValidEstimateField?.SetValue(stats, true);

        // Act
        var result = stats.GetFormattedTimeRemaining();

        // Assert
        result.Should().Be("Calculating...");
    }
}