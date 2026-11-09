using System.Text.Json;
using FluentAssertions;
using LoE_Launcher.Core;
using Models;

namespace LoE_Launcher.Tests;

public class LauncherJsonTests
{
    [Fact]
    public void LauncherVersion_ShouldReadLiveObjectFormat()
    {
        const string json = """
            {
              "RequiredVersion": { "Major": 1, "Minor": 0, "Build": 0, "Revision": 0 },
              "Message": "Your launcher is out of date. Please download the latest version."
            }
            """;

        var info = LauncherJson.Parse(json, LauncherJson.Default.LauncherVersionInfo)!;

        info.RequiredVersion.ToString().Should().Be("1.0.0.0");
        info.IsCompatible("1.3.0.0").Should().BeTrue();
    }

    [Fact]
    public void LauncherVersion_ShouldReadStringFormatAndTreatMissingPartsAsZero()
    {
        LauncherJson.Parse("""{ "RequiredVersion": "1.3.0" }""", LauncherJson.Default.LauncherVersionInfo)!
            .RequiredVersion.ToString().Should().Be("1.3.0");

        var partial = LauncherJson.Parse("""{ "RequiredVersion": { "Major": 1, "Minor": 3 } }""",
            LauncherJson.Default.LauncherVersionInfo)!;
        partial.RequiredVersion.ToString().Should().Be("1.3.0.0");
        partial.IsCompatible("1.2.2.0").Should().BeFalse();
        partial.IsCompatible("1.3.0.0").Should().BeTrue();
    }

    [Fact]
    public void ControlFile_ShouldReadPatchServerFormat()
    {
        const string json = "\uFEFF" + """
            {
              "Version": { "Major": 0, "Minor": 2, "Build": -1, "Revision": -1, "MajorRevision": -1, "MinorRevision": -1 },
              "Content": [
                {
                  "RelativeContentUrl": "loe_Data/Resources/unity%20default%20resources.jar.zsync.jar",
                  "FileHash": "759a0182a9627061467f109db89ec534",
                  "_installPath": ".\\loe_Data\\Resources\\unity default resources.jar.zsync.jar"
                }
              ],
            }
            """;

        var control = LauncherJson.Parse(json, LauncherJson.Default.MainControlFile)!;

        control.Version!.ToString().Should().Be("0.2");
        control.RootUri.Should().BeNull();
        var item = control.Content.Should().ContainSingle().Subject;
        item.RelativeContentUrl!.IsAbsoluteUri.Should().BeFalse();
        item.RelativeContentUrl.ToString().Should().Be("loe_Data/Resources/unity%20default%20resources.jar.zsync.jar");
        item.FileHash.Should().Be("759a0182a9627061467f109db89ec534");
        item.InstallPath.Should().NotBeNull();
    }

    [Fact]
    public void Settings_ShouldReadFileWrittenByOlderLaunchers()
    {
        const string json = """
            {
              "IgnoreSSLCertificates": false,
              "ZsyncLocation": "https://patches.legendsofequestria.com/zsync/{version}/",
              "Stream": "https://patches.legendsofequestria.com/zsync/versions3.json",
              "LauncherVersionUrl": "https://patches.legendsofequestria.com/launcher-version.json",
              "CloseAfterLaunch": false
            }
            """;

        var settings = LauncherJson.Parse(json, LauncherJson.Default.Settings)!;

        settings.CloseAfterLaunch.Should().BeFalse();
        settings.Channel.Should().Be(GameChannel.Live);
        settings.DevStream.Should().Be("https://loedata.legendsofequestria.com/dev/versions-dev.json");
    }

    [Fact]
    public void Settings_ShouldReadChannelCaseInsensitively()
    {
        LauncherJson.Parse("""{ "channel": "dev" }""", LauncherJson.Default.Settings)!
            .Channel.Should().Be(GameChannel.Dev);
    }

    [Fact]
    public void HashCache_ShouldReadNewtonsoftDatesExactly()
    {
        const string json = """{"C:\\Game\\baselib.dll":{"FilePath":"C:\\Game\\baselib.dll","Hash":"759a","LastModifiedUtc":"2025-12-21T17:04:39.4263533Z","FileSize":226216}}""";

        var cache = LauncherJson.Parse(json, LauncherJson.Default.DictionaryStringFileHashCache)!;
        var entry = cache[@"C:\Game\baselib.dll"];

        entry.LastModifiedUtc.Should().Be(new DateTime(2025, 12, 21, 17, 4, 39, DateTimeKind.Utc).AddTicks(4263533));
        entry.LastModifiedUtc.Kind.Should().Be(DateTimeKind.Utc);
        entry.FileSize.Should().Be(226216);

        var again = LauncherJson.Parse(JsonSerializer.Serialize(cache, LauncherJson.Default.DictionaryStringFileHashCache),
            LauncherJson.Default.DictionaryStringFileHashCache)!;
        again[@"C:\Game\baselib.dll"].LastModifiedUtc.Should().Be(entry.LastModifiedUtc);
    }

    [Fact]
    public void Settings_From140_ShouldUseDefaultFolders()
    {
        var settings = LauncherJson.Parse("""{ "CloseAfterLaunch": false, "ImportOffered": true }""",
            LauncherJson.Default.Settings)!;

        settings.GameFolder.Should().BeNull();
        settings.DevGameFolder.Should().BeNull();
        settings.PendingMoveTarget.Should().BeNull();
        settings.PendingOldCopy.Should().BeNull();
    }

    [Fact]
    public void Settings_NewFields_ShouldRoundTrip()
    {
        var settings = new Settings
        {
            GameFolder = @"D:\Games\LoE",
            DevGameFolder = @"D:\Games\LoE-Dev",
            PendingMoveTarget = @"E:\New",
            PendingOldCopy = @"D:\Games\LoE"
        };

        var again = LauncherJson.Parse(JsonSerializer.Serialize(settings, LauncherJson.Default.Settings),
            LauncherJson.Default.Settings)!;

        again.GameFolder.Should().Be(settings.GameFolder);
        again.DevGameFolder.Should().Be(settings.DevGameFolder);
        again.PendingMoveTarget.Should().Be(settings.PendingMoveTarget);
        again.PendingOldCopy.Should().Be(settings.PendingOldCopy);
    }
}
