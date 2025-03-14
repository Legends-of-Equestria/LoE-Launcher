using System;
using System.IO;
using System.Net.Http;
using LoE_Launcher.Core;
using zsyncnet.Sync;

namespace LoE_Launcher;

public class ProgressReportingRangeDownloader(Uri uri, HttpClient client, Downloader.DownloadProgressCallback? progressCallback = null) : RangeDownloader(uri, client)
{
    public new Stream DownloadRange(long from, long to)
    {
        var originalStream = base.DownloadRange(from, to);
        return progressCallback != null 
            ? new ProgressReportingStream(originalStream, progressCallback) 
            : originalStream;
    }
    
    public new Stream Download()
    {
        var originalStream = base.Download();
        return progressCallback != null 
            ? new ProgressReportingStream(originalStream, progressCallback) 
            : originalStream;
    }
}