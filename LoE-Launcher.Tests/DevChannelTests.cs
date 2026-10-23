using System.Net;
using FluentAssertions;
using LoE_Launcher;
using LoE_Launcher.Services;
using Models;
using Newtonsoft.Json;
using Xunit;

namespace LoE_Launcher.Tests;

public class DevChannelTests
{
    private static Settings DevSettings(string token = "secret") => new()
    {
        Channel = GameChannel.Dev,
        DevToken = token
    };

    [Fact]
    public void LiveChannel_ShouldUseLiveStreamAndFolder()
    {
        var settings = new Settings();

        settings.IsDevChannelActive.Should().BeFalse();
        settings.ActiveStream.Should().Be(settings.Stream);
        settings.ActiveInstallFolderName.Should().Be("Game");
        settings.FormatZsyncLocation("1.0.0").Should().Be("https://patches.legendsofequestria.com/zsync/1.0.0/");
    }

    [Fact]
    public void DevChannel_ShouldUseDevStreamAndFolder()
    {
        var settings = DevSettings();

        settings.IsDevChannelActive.Should().BeTrue();
        settings.ActiveStream.Should().Be("https://loedata.legendsofequestria.com/dev/versions-dev.json");
        settings.ActiveInstallFolderName.Should().Be("Game-Dev");
        settings.FormatZsyncLocation("build-1234").Should().Be("https://loedata.legendsofequestria.com/dev/build-1234/");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DevChannel_WithoutToken_ShouldFallBackToLive(string token)
    {
        var settings = DevSettings(token);

        settings.IsDevChannelActive.Should().BeFalse();
        settings.ActiveStream.Should().Be(settings.Stream);
        settings.ActiveInstallFolderName.Should().Be("Game");
    }

    [Theory]
    [InlineData("https://loedata.legendsofequestria.com/dev/versions-dev.json", true)]
    [InlineData("https://loedata.legendsofequestria.com/dev/build-1/.zsync-control.jar", true)]
    [InlineData("https://loedata.legendsofequestria.com/data/Changelog.txt", false)]
    [InlineData("https://patches.legendsofequestria.com/zsync/versions3.json", false)]
    [InlineData("http://loedata.legendsofequestria.com/dev/versions-dev.json", false)]
    [InlineData("https://evil.example.com/dev/versions-dev.json", false)]
    public void ShouldSendDevToken_OnlyForDevHostAndPath(string url, bool expected)
    {
        DevSettings().ShouldSendDevToken(new Uri(url)).Should().Be(expected);
    }

    [Fact]
    public void ShouldSendDevToken_AllowsPlainHttpOnLoopback()
    {
        var settings = DevSettings();
        settings.DevZsyncLocation = "http://localhost:8765/dev/{version}/";

        settings.ShouldSendDevToken(new Uri("http://localhost:8765/dev/versions-dev.json")).Should().BeTrue();
        settings.ShouldSendDevToken(new Uri("http://localhost:9999/dev/versions-dev.json")).Should().BeFalse();
    }

    [Fact]
    public void ShouldSendDevToken_NeverWhenCertificateChecksAreOff()
    {
        var settings = DevSettings();
        settings.IgnoreSSLCertificates = true;

        settings.ShouldSendDevToken(new Uri("https://loedata.legendsofequestria.com/dev/versions-dev.json"))
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldSendDevToken_NeverWithoutToken()
    {
        DevSettings("").ShouldSendDevToken(new Uri("https://loedata.legendsofequestria.com/dev/x")).Should().BeFalse();
    }

    [Fact]
    public void Settings_ShouldRoundTripChannelAsString_WithoutToken()
    {
        var json = JsonConvert.SerializeObject(DevSettings());

        json.Should().Contain("\"Channel\":\"Dev\"");
        json.Should().NotContain("IsDevChannelActive");
        json.Should().NotContain("DevToken");
        json.Should().NotContain("secret");

        var restored = JsonConvert.DeserializeObject<Settings>(json)!;
        restored.Channel.Should().Be(GameChannel.Dev);
        restored.DevToken.Should().BeEmpty();
    }

    [Fact]
    public void Settings_ShouldIgnoreTokenFoundInSettingsJson()
    {
        var restored = JsonConvert.DeserializeObject<Settings>("{\"Channel\":\"Dev\",\"DevToken\":\"secret\"}")!;

        restored.DevToken.Should().BeEmpty();
        restored.IsDevChannelActive.Should().BeFalse();
    }

    [Fact]
    public void OldSettingsFile_ShouldDefaultToLive()
    {
        var restored = JsonConvert.DeserializeObject<Settings>("{\"CloseAfterLaunch\":false}")!;

        restored.Channel.Should().Be(GameChannel.Live);
        restored.IsDevChannelActive.Should().BeFalse();
    }

    [Fact]
    public void VersionsControlFile_ShouldParseBuildInfo()
    {
        const string json = """
            {
              "Win64": "build-1234",
              "Build": { "Id": "build-1234", "Branch": "main", "Commit": "abc1234", "Time": "2026-10-23T18:00:00Z" }
            }
            """;

        var data = JsonConvert.DeserializeObject<VersionsControlFile>(json)!;

        data.Win64.Should().Be("build-1234");
        data.Linux.Should().BeNull();
        data.Build!.ToString().Should().Be("build-1234 · main@abc1234 · 2026-10-23T18:00:00Z");
    }

    [Fact]
    public void BuildInfo_ShouldSkipMissingParts()
    {
        new BuildInfo { Id = "build-1" }.ToString().Should().Be("build-1");
        new BuildInfo { Commit = "abc" }.ToString().Should().Be("abc");
    }

    [Fact]
    public async Task DevTokenHandler_AddsBearerOnlyToDevRequests()
    {
        var recorder = new RecordingHandler();
        using var client = new HttpClient(new DevTokenHandler(DevSettings(" secret "), recorder));

        await client.GetAsync("https://loedata.legendsofequestria.com/dev/versions-dev.json");
        recorder.LastAuthorization.Should().Be("Bearer secret");

        await client.GetAsync("https://patches.legendsofequestria.com/zsync/versions3.json");
        recorder.LastAuthorization.Should().BeNull();
    }

    [Theory]
    [InlineData("https://loedata.legendsofequestria.com/dev-auth/login.php", true)]
    [InlineData("http://loedata.legendsofequestria.com/dev-auth/login.php", false)]
    [InlineData("https://evil.example.com/dev-auth/login.php", false)]
    [InlineData("https://patches.legendsofequestria.com/dev-auth/login.php", false)]
    public void IsSafeLoginUri_OnlyDevHostOverHttps(string url, bool expected)
    {
        new Settings().IsSafeLoginUri(new Uri(url)).Should().Be(expected);
    }

    [Fact]
    public void IsSafeLoginUri_NeverWhenCertificateChecksAreOff()
    {
        var settings = new Settings { IgnoreSSLCertificates = true };

        settings.IsSafeLoginUri(new Uri(settings.DevLoginUrl)).Should().BeFalse();
    }

    [Fact]
    public void ParseDevLoginResponse_ReturnsToken()
    {
        var token = new string('a', 64);

        var result = NetworkDownloadService.ParseDevLoginResponse(HttpStatusCode.OK,
            $"{{\"token\":\"{token}\",\"expires\":\"2026-11-23T00:00:00Z\"}}");

        result.Should().Be(new DevLoginResult(DevLoginStatus.Ok, token));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "{\"token\":\"short\"}", DevLoginStatus.Unreachable)]
    [InlineData(HttpStatusCode.OK, "<html>not json</html>", DevLoginStatus.Unreachable)]
    [InlineData(HttpStatusCode.Unauthorized, "{\"error\":\"wrong_login\"}", DevLoginStatus.WrongLogin)]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\":\"missing\"}", DevLoginStatus.WrongLogin)]
    [InlineData(HttpStatusCode.Forbidden, "{\"error\":\"no_access\"}", DevLoginStatus.NoAccess)]
    [InlineData(HttpStatusCode.TooManyRequests, "<html>429</html>", DevLoginStatus.TooManyAttempts)]
    [InlineData(HttpStatusCode.BadGateway, "", DevLoginStatus.Unreachable)]
    public void ParseDevLoginResponse_MapsErrors(HttpStatusCode status, string body, DevLoginStatus expected)
    {
        NetworkDownloadService.ParseDevLoginResponse(status, body).Status.Should().Be(expected);
    }

    [Fact]
    public void IsSafeDevUri_WorksWithoutSavedToken()
    {
        var settings = new Settings();

        settings.IsSafeDevUri(new Uri(settings.DevStream)).Should().BeTrue();
        settings.IsSafeDevUri(new Uri("https://evil.example.com/dev/versions-dev.json")).Should().BeFalse();
        settings.ShouldSendDevToken(new Uri(settings.DevStream)).Should().BeFalse();
    }

    private class RecordingHandler : HttpMessageHandler
    {
        public string? LastAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastAuthorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
