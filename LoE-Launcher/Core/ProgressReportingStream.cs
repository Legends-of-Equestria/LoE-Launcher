using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LoE_Launcher.Core;

namespace LoE_Launcher;

public class ProgressReportingStream(Stream baseStream, Downloader.DownloadProgressCallback progressCallback) : Stream
{    
    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = baseStream.Read(buffer, offset, count);
        if (bytesRead > 0)
        {
            progressCallback?.Invoke(bytesRead);
        }
        
        return bytesRead;
    }
    
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var bytesRead = await baseStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        if (bytesRead > 0)
        {
            progressCallback?.Invoke(bytesRead);
        }
        
        return bytesRead;
    }
    
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var bytesRead = await baseStream.ReadAsync(buffer, cancellationToken);
        if (bytesRead > 0)
        {
            progressCallback?.Invoke(bytesRead);
        }
        
        return bytesRead;
    }
    
    public override bool CanRead => baseStream.CanRead;
    public override bool CanSeek => baseStream.CanSeek;
    public override bool CanWrite => baseStream.CanWrite;
    public override long Length => baseStream.Length;
    
    public override long Position
    {
        get => baseStream.Position;
        set => baseStream.Position = value;
    }
    
    public override void Flush() => baseStream.Flush();
    public override long Seek(long offset, SeekOrigin origin) => baseStream.Seek(offset, origin);
    public override void SetLength(long value) => baseStream.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => baseStream.Write(buffer, offset, count);
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            baseStream.Dispose();
        }
        
        base.Dispose(disposing);
    }
}