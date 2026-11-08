using FluentAssertions;
using zsyncnet;

namespace LoE_Launcher.Tests;

public class ZsyncRoundTripTests
{
    [Fact]
    public void Sync_ShouldOnlyDownloadChangedBlocks()
    {
        var remote = RandomBytes(25 * 1024 * 1024 + 1234, seed: 1);
        var outdated = (byte[])remote.Clone();
        RandomBytes(100_000, seed: 2).CopyTo(outdated, 3 * 1024 * 1024);
        RandomBytes(50_000, seed: 3).CopyTo(outdated, outdated.Length - 60_000);

        var (output, downloaded) = Sync(remote, new MemoryStream(outdated));

        output.Should().Equal(remote);
        downloaded.Should().BeLessThan(1024 * 1024);
    }

    [Fact]
    public void Sync_ShouldOnlyDownloadChangedBlocks_WhenTheSeedReturnsShortReads()
    {
        var remote = RandomBytes(25 * 1024 * 1024 + 1234, seed: 4);
        var outdated = (byte[])remote.Clone();
        RandomBytes(100_000, seed: 5).CopyTo(outdated, 12 * 1024 * 1024);

        var (output, downloaded) = Sync(remote, new ShortReadStream(new MemoryStream(outdated), maxRead: 64 * 1024));

        output.Should().Equal(remote);
        downloaded.Should().BeLessThan(1024 * 1024);
    }

    private static (byte[] Output, long Downloaded) Sync(byte[] remote, Stream seed)
    {
        var written = new MemoryStream();
        ZsyncMake.MakeControlFile(new MemoryStream(remote), DateTime.UtcNow, "game.bin").WriteToStream(written);
        written.Position = 0;
        var controlFile = new ControlFile(written);

        var output = new MemoryStream();
        var downloader = new MemoryRangeDownloader(remote);
        Zsync.Sync(controlFile, [seed], downloader, output);
        return (output.ToArray(), downloader.Downloaded);
    }

    private static byte[] RandomBytes(int length, int seed)
    {
        var bytes = new byte[length];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }

    private sealed class MemoryRangeDownloader(byte[] data) : IRangeDownloader
    {
        public long Downloaded { get; private set; }

        public Stream DownloadRange(long from, long to)
        {
            var length = (int)(Math.Min(to, data.Length) - from);
            Downloaded += length;
            return new MemoryStream(data, (int)from, length, writable: false);
        }

        public Stream Download()
        {
            Downloaded += data.Length;
            return new MemoryStream(data, writable: false);
        }
    }

    private sealed class ShortReadStream(Stream inner, int maxRead) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            inner.Read(buffer, offset, Math.Min(count, maxRead));

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
