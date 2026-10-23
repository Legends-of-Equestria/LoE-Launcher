using System.Text;
using FluentAssertions;
using LoE_Launcher.Services;
using Xunit;

namespace LoE_Launcher.Tests;

public sealed class DevTokenStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "loe-token-tests-" + Guid.NewGuid().ToString("N"));
    private readonly DevTokenStore _store;

    public DevTokenStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _store = new DevTokenStore(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void Load_WithNoFile_ReturnsEmpty()
    {
        _store.Load().Should().BeEmpty();
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        _store.Save("  my-dev-token-123 ");

        _store.Load().Should().Be("my-dev-token-123");
    }

    [Fact]
    public void Save_DoesNotWriteTokenInPlainText_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        _store.Save("my-dev-token-123");

        var raw = File.ReadAllBytes(_store.TokenFilePath);
        Encoding.UTF8.GetString(raw).Should().NotContain("my-dev-token-123");
        Encoding.Unicode.GetString(raw).Should().NotContain("my-dev-token-123");
    }

    [Fact]
    public void Save_RestrictsFileToOwner_OnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        _store.Save("my-dev-token-123");

        File.GetUnixFileMode(_store.TokenFilePath).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public void Load_RetightensLoosenedPermissions_OnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        _store.Save("my-dev-token-123");
        File.SetUnixFileMode(_store.TokenFilePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        _store.Load().Should().Be("my-dev-token-123");
        File.GetUnixFileMode(_store.TokenFilePath).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public void SaveEmpty_DeletesFile()
    {
        _store.Save("my-dev-token-123");
        _store.Save("   ");

        File.Exists(_store.TokenFilePath).Should().BeFalse();
        _store.Load().Should().BeEmpty();
    }

    [Fact]
    public void Load_WithUnreadableFile_ReturnsEmpty_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Like a token file copied from another user or PC
        File.WriteAllBytes(_store.TokenFilePath, [1, 2, 3, 4, 5]);

        _store.Load().Should().BeEmpty();
    }

    [Fact]
    public void Save_LeavesNoTempFile()
    {
        _store.Save("my-dev-token-123");

        Directory.GetFiles(_dir).Should().ContainSingle().Which.Should().Be(_store.TokenFilePath);
    }
}
