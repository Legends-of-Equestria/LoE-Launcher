using FluentAssertions;
using Models;
using Models.Utils;
using Newtonsoft.Json;

namespace LoE_Launcher.Tests;

public class VersionsControlFileTests
{
    private const string LiveJson = """
        {
          "win32": "LoE_Win32_2025.06.002",
          "win64": "LoE_Win64_2026.03.001",
          "Linux": "LoE_Linux_2026.03.001",
          "Mac_Intel": "LoE_Mac_2026.03.001",
          "Mac_Arm": "LoE_Mac_2026.03.001"
        }
        """;

    [Fact]
    public void ForPlatform_ShouldReadCurrentLiveFile()
    {
        var data = JsonConvert.DeserializeObject<VersionsControlFile>(LiveJson)!;

        data.ForPlatform(OS.WindowsX64).Should().Be("LoE_Win64_2026.03.001");
        data.ForPlatform(OS.X11).Should().Be("LoE_Linux_2026.03.001");
        data.ForPlatform(OS.MacArm).Should().Be("LoE_Mac_2026.03.001");
        data.ForPlatform(OS.MacIntel).Should().Be("LoE_Mac_2026.03.001");
    }

    [Fact]
    public void ForPlatform_ShouldPreferSingleMacKey()
    {
        var data = JsonConvert.DeserializeObject<VersionsControlFile>(
            """{ "Mac": "LoE_Mac_2026.04.001", "Mac_Arm": "old", "Mac_Intel": "old" }""")!;

        data.ForPlatform(OS.MacArm).Should().Be("LoE_Mac_2026.04.001");
        data.ForPlatform(OS.MacIntel).Should().Be("LoE_Mac_2026.04.001");
    }

    [Theory]
    [InlineData(OS.WindowsX86)]
    [InlineData(OS.Other)]
    public void Unsupported_ShouldHaveNoBuild(OS os)
    {
        var data = JsonConvert.DeserializeObject<VersionsControlFile>(LiveJson)!;

        VersionsControlFile.IsSupported(os).Should().BeFalse();
        data.ForPlatform(os).Should().BeNull();
    }
}
