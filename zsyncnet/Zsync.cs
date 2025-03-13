using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using zsyncnet.Sync;
using zsyncnet.Util;

namespace zsyncnet
{
    public class Zsync
    {
        private static bool IsAbsoluteUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out _);
        }

        private static ControlFile DownloadControlFile(Uri uri)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = client.Send(request);
            response.EnsureSuccessStatusCode();
            using var stream = response.Content.ReadAsStream();
            return new ControlFile(stream);
        }

        /// <summary>
        /// Syncs a file in the output folder from a remote file. Can handle .part files.
        /// </summary>
        /// <param name="zsyncFile">Uri to the remote file. A .zsync file is assumed to be next to it and will be used to find or create the local file</param>
        /// <param name="output">Folder in which the work file and a potential .part file exist or will be created.</param>
        /// <param name="stateCallback">Callback function for state changes during the sync operation</param>
        /// <param name="progress">Receives incremental progress in bytes. The total sum will be equal to the target file size when the operation is complete.</param>
        /// <param name="cancellationToken">Cancels the syncing operation. Downloaded data is continuously written to the workingStream and will not be lost.</param>
        public static void Sync(Uri zsyncFile, DirectoryInfo output, Action<SyncState> stateCallback = null, IProgress<ulong> progress = null, CancellationToken cancellationToken = default)
        {
            // Load zsync control file
            var cf = DownloadControlFile(zsyncFile);

            Uri fileUri;

            if (cf.GetHeader().Url == null || !IsAbsoluteUrl(cf.GetHeader().Url))
            {
                // Relative
                fileUri = new Uri(zsyncFile.ToString().Replace(".zsync", string.Empty));
            }
            else
            {
                fileUri = new Uri(cf.GetHeader().Url);
            }

            var downloader = new RangeDownloader(fileUri, new HttpClient());

            Sync(cf, downloader, output, stateCallback, progress, cancellationToken);
        }

        /// <summary>
        /// Syncs a file in the output folder from a control file and file downloader. Can handle .part files.
        /// </summary>
        /// <param name="controlFile">The control file. The filename is used to find or create the working file in the output folder</param>
        /// <param name="downloader">Downloader for the remote file.</param>
        /// <param name="output">Folder in which the work file and a potential .part file exist or will be created.</param>
        /// <param name="stateCallback">Callback function for state changes during the sync operation</param>
        /// <param name="progress">Receives incremental progress in bytes. The total sum will be equal to the target file size when the operation is complete.</param>
        /// <param name="cancellationToken">Cancels the syncing operation. Downloaded data is continuously written to the workingStream and will not be lost.</param>
        public static void Sync(ControlFile controlFile, IRangeDownloader downloader, DirectoryInfo output, Action<SyncState> stateCallback = null, IProgress<ulong> progress = null, CancellationToken cancellationToken = default)
        {
            IZsyncProgress stateProgress = stateCallback != null 
                ? new StateProgress(stateCallback, progress) 
                : progress as IZsyncProgress;

            var path = Path.Combine(output.FullName, controlFile.GetHeader().Filename.Trim());
            if (!File.Exists(path))
            {
                // File does not exist on disk, we just need to download it
                stateProgress?.ReportState(SyncState.DownloadNew);
                
                using var downloadStream = downloader.Download();
                using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                
                downloadStream.CopyToWithProgress(stream, 8192, stateProgress, cancellationToken);
                File.SetLastWriteTime(path, controlFile.GetHeader().MTime);

                return;
            }
            
            var partFile = new FileInfo(path + ".part");

            var tmpStream = new FileStream(partFile.FullName, FileMode.OpenOrCreate, FileAccess.ReadWrite);

            var seeds = new List<Stream> { File.OpenRead(path) };

            try
            {
                Sync(controlFile, seeds, downloader, tmpStream, stateCallback, progress, cancellationToken);
            }
            finally
            {
                tmpStream.Close();
                foreach (var seed in seeds)
                {
                    seed.Close();
                }
            }

            stateProgress?.ReportState(SyncState.PatchFile);
            File.Move(partFile.FullName, path, true);
            File.SetLastWriteTime(path, controlFile.GetHeader().MTime);
        }

        /// <summary>
        /// Patches a file (workingStream) according to the passed controlFile. Additional seeds can be specified.
        /// </summary>
        /// <param name="controlFile">The control file. The filename in it is ignored.</param>
        /// <param name="seeds">Additional seeding streams. Must not include the workingStream. Streams are not closed.</param>
        /// <param name="downloader">Downloader for the remote file.</param>
        /// <param name="workingStream">Working Stream. If it contains any data, that data is used as a seed. Will not be closed.</param>
        /// <param name="stateCallback">Callback function for state changes during the sync operation</param>
        /// <param name="progress">Receives incremental progress in bytes. The total sum will be equal to the target file size when the operation is complete.</param>
        /// <param name="cancellationToken">Cancels the syncing operation. Downloaded data is continuously written to the workingStream and will not be lost.</param>
        public static void Sync(ControlFile controlFile, List<Stream> seeds, IRangeDownloader downloader, Stream workingStream, Action<SyncState> stateCallback = null, IProgress<ulong> progress = null, CancellationToken cancellationToken = default)
        {
            IZsyncProgress stateProgress = stateCallback != null 
                ? new StateProgress(stateCallback, progress) 
                : progress as IZsyncProgress;

            ZsyncPatch.Patch(seeds, controlFile, downloader, workingStream, stateProgress, cancellationToken);
        }

        /// <summary>
        /// Syncs a file from a remote location using the zsync algorithm.
        /// </summary>
        /// <param name="zsyncUri">The URI of the zsync control file</param>
        /// <param name="outputFile">The target file to create or update</param>
        /// <param name="targetUri">The URI of the target file to download</param>
        /// <param name="stateCallback">Callback function that provides status updates during the sync process</param>
        /// <returns>Number of bytes transferred</returns>
        public static long Sync(Uri zsyncUri, FileInfo outputFile, Uri targetUri, Action<SyncState> stateCallback = null)
        {
            // This method is added for backward compatibility
            var byteCounter = new ByteCounter();
            IZsyncProgress stateProgress = stateCallback != null 
                ? new StateProgress(stateCallback, byteCounter) 
                : byteCounter as IZsyncProgress;

            var client = new HttpClient();
            var downloader = new RangeDownloader(targetUri, client);
    
            var controlFile = DownloadControlFile(zsyncUri);
    
            Sync(controlFile, downloader, outputFile.Directory, stateCallback, byteCounter);
    
            return (long)byteCounter.BytesProcessed;
        }

        private class ByteCounter : IZsyncProgress
        {
            public ulong BytesProcessed { get; private set; } = 0;

            public void Report(ulong value)
            {
                BytesProcessed += value;
            }

            public void ReportState(SyncState state)
            {
                // Do nothing - this is handled by the StateProgress wrapper
            }
        }
    }
}