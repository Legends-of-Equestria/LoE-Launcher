using System;
using System.Runtime.InteropServices;
using FluentAssertions;
using Xunit;

namespace LoE_Launcher.Tests;

public class UnityPlayerLogHelperTests
{
    [Fact]
    public void GetPlayerLogPath_OnWindows_ShouldReturnCorrectPath()
    {
        // This test only runs on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return; // Skip on non-Windows platforms
        }

        // Arrange
        var companyName = "TestCompany";
        var productName = "TestProduct";
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expectedPath = System.IO.Path.Combine(userProfile, "AppData", "LocalLow", companyName, productName, "Player.log");

        // Act
        var result = UnityPlayerLogHelper.GetPlayerLogPath(companyName, productName);

        // Assert
        result.Should().Be(expectedPath);
        result.Should().EndWith("Player.log");
        result.Should().Contain("AppData\\LocalLow");
    }

    [Fact]
    public void GetPlayerLogPath_OnMacOS_ShouldReturnCorrectPath()
    {
        // This test only runs on macOS
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return; // Skip on non-macOS platforms
        }

        // Arrange
        var companyName = "TestCompany";
        var productName = "TestProduct";
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expectedPath = System.IO.Path.Combine(userProfile, "Library", "Logs", companyName, productName, "Player.log");

        // Act
        var result = UnityPlayerLogHelper.GetPlayerLogPath(companyName, productName);

        // Assert
        result.Should().Be(expectedPath);
        result.Should().EndWith("Player.log");
        result.Should().Contain("Library/Logs");
    }

    [Fact]
    public void GetPlayerLogPath_OnLinux_ShouldReturnCorrectPath()
    {
        // This test only runs on Linux
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return; // Skip on non-Linux platforms
        }

        // Arrange
        var companyName = "TestCompany";
        var productName = "TestProduct";
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expectedPath = System.IO.Path.Combine(userProfile, ".config", "unity3d", companyName, productName, "Player.log");

        // Act
        var result = UnityPlayerLogHelper.GetPlayerLogPath(companyName, productName);

        // Assert
        result.Should().Be(expectedPath);
        result.Should().EndWith("Player.log");
        result.Should().Contain(".config/unity3d");
    }

    [Theory]
    [InlineData("Company With Spaces", "Product-With-Dashes")]
    [InlineData("Company/With/Slashes", "Product\\With\\Backslashes")]
    public void GetPlayerLogPath_WithVariousInputs_ShouldHandleCorrectly(string companyName, string productName)
    {
        // Act
        var result = UnityPlayerLogHelper.GetPlayerLogPath(companyName, productName);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().EndWith("Player.log");
        result.Should().Contain(companyName);
        result.Should().Contain(productName);
    }

    [Theory]
    [InlineData("", "TestProduct")]
    [InlineData("TestCompany", "")]
    public void GetPlayerLogPath_WithEmptyInputs_ShouldStillReturnValidPath(string companyName, string productName)
    {
        // Act
        var result = UnityPlayerLogHelper.GetPlayerLogPath(companyName, productName);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().EndWith("Player.log");
        // Don't check containment for empty strings, just verify path structure
        if (!string.IsNullOrEmpty(companyName))
        {
            result.Should().Contain(companyName);
        }
        if (!string.IsNullOrEmpty(productName))
        {
            result.Should().Contain(productName);
        }
    }
}