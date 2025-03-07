using System;
using System.IO;

namespace zsyncnet
{
    public enum SyncState
    {
        Unknown,
        CalcDiff,         // Calculating differences
        CopyExisting,     // Copying existing parts
        DownloadPatch,    // Downloading patch data
        DownloadNew,      // Downloading new data
        PatchFile         // Applying patches
    }

    public interface IRangeDownloader
    {
        /// <summary>
        /// Starts the download of a file section. Should return after receiving headers and opening the stream, so that the reading can be done asynchronously.
        /// </summary>
        /// <param name="from">Start (inclusive)</param>
        /// <param name="to">End (exclusive)</param>
        /// <returns></returns>
        Stream DownloadRange(long from, long to);
        
        /// <summary>
        /// Starts the download of the entire file. Should return after receiving headers and opening the stream, so that the reading can be done asynchronously.
        /// </summary>
        /// <returns></returns>
        Stream Download();
    }

    public class ZsyncProgress
    {
        public SyncState State { get; set; }
        public ulong BytesProcessed { get; set; }
    }

    public interface IZsyncProgress : IProgress<ulong>
    {
        void ReportState(SyncState state);
    }

    public class StateProgress : IZsyncProgress
    {
        private readonly Action<SyncState> _stateCallback;
        private readonly IProgress<ulong> _progress;

        public StateProgress(Action<SyncState> stateCallback, IProgress<ulong> progress = null)
        {
            _stateCallback = stateCallback;
            _progress = progress;
        }

        public void ReportState(SyncState state)
        {
            _stateCallback?.Invoke(state);
        }

        public void Report(ulong value)
        {
            _progress?.Report(value);
        }
    }
}
