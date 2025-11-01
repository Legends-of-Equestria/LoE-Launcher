using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using LoE_Launcher.Core;
using Models;
using Models.Paths;
using Models.Paths.Json;
using Models.Utils;
using NLog;

namespace LoE_Launcher.Services;

public class FileUpdateService(FileOperationsService fileOps, NetworkDownloadService network, HashCacheService cache)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public async Task<Downloader.DownloadData?> DownloadMainControlFile(Uri url, ProgressData progress, IAbsoluteDirectoryPath gameInstallFolder, Action<GameState> setState)
    {
        var mainControlFile = await network.DownloadJson<MainControlFile>(url, setState);
        if (mainControlFile == null)
        {
            return null;
        }

        var data = new Downloader.DownloadData(mainControlFile);

        progress.ResetCounter(data.ControlFile.Content.Count, true);

        var filesToProcess = new List<ControlFileItem>();
        var processedCount = 0;

        var hashCache = cache.LoadHashCache();
        var cacheUpdates = new Dictionary<string, FileHashCache>();

        var filesByDirectory = data.ControlFile.Content
            .GroupBy(item => Path.GetDirectoryName(
                item.GetUnzippedFileName().GetAbsolutePathFrom(gameInstallFolder).Path))
            .ToList();

        foreach (var item in filesByDirectory.SelectMany(directoryGroup => directoryGroup))
        {
            try
            {
                var realFile = item.GetUnzippedFileName().GetAbsolutePathFrom(gameInstallFolder);
                var fileMatchesHash = false;

                if (realFile.Exists)
                {
                    var fileInfo = new FileInfo(realFile.Path);

                    if (cache.TryGetCachedHash(realFile.Path, fileInfo, hashCache, out var cachedHash))
                    {
                        fileMatchesHash = cachedHash == item.FileHash;
                        Logger.Info($"{item._installPath}: Hash : {cachedHash} : {item.FileHash} : Match={fileMatchesHash}");
                    }
                    else
                    {
                        var fileHash = realFile.ToString().GetFileHash(HashType.MD5);
                        fileMatchesHash = fileHash == item.FileHash;
                        Logger.Info($"{item._installPath}: Direct hash : {fileHash} : {item.FileHash} : Match={fileMatchesHash}");

                        if (!hashCache.TryGetValue(realFile.Path, out var value) || value.Hash != fileHash)
                        {
                            cache.UpdateCacheEntry(realFile.Path, fileHash, fileInfo, cacheUpdates);
                            Logger.Info($"Adding cache entry for up-to-date file: {realFile.Path}");
                        }
                    }
                }

                if (!fileMatchesHash)
                {
                    filesToProcess.Add(item);
                    Logger.Info($"Adding to process list: {item.InstallPath}");
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Issue with control file item {item.InstallPath}");
                throw;
            }
            finally
            {
                processedCount++;
                Dispatcher.UIThread.Post(() => progress.SetCount(processedCount));
            }
        }

        foreach (var update in cacheUpdates)
        {
            hashCache[update.Key] = update.Value;
        }

        cache.SaveHashCache(hashCache);

        data.ToProcess = filesToProcess;

        return data;
    }

    public async Task UpdateFiles(Downloader.DownloadData data, ProgressData progress, IAbsoluteDirectoryPath gameInstallFolder,
        Downloader.DownloadProgressCallback onDownloadProgress, Action<GameState> setState, int retries = 0)
    {
        var installProgress = progress as Downloader.InstallingProgress;
        var tries = 0;
        var queue = new Queue<ControlFileItem>(data.ToProcess);

        progress.ResetCounter(queue.Count, true);

        var hashCache = cache.LoadHashCache();
        var hashCacheUpdates = new ConcurrentDictionary<string, FileHashCache>();

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("LoE-Launcher/1.0");
        httpClient.Timeout = TimeSpan.FromMinutes(5);

        if (queue.Count > 0)
        {
            var testItem = queue.Peek();
            var testUri = testItem.GetContentUri(data.ControlFile);

            if (!await network.TestConnectionIfNeeded(testUri, httpClient))
            {
                setState(GameState.Offline);
                return;
            }
        }

        while (tries <= retries)
        {
            tries++;
            var reProcess = new Queue<ControlFileItem>();
            var lastUnzip = Task.CompletedTask;

            while (queue.Count != 0)
            {
                try
                {
                    var item = queue.Dequeue();
                    var bytesRead = await ProcessQueueItem(item, data.ControlFile, gameInstallFolder, installProgress, lastUnzip, hashCacheUpdates, onDownloadProgress);

                    if (bytesRead == 0)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(item.InstallPath.Path);
                        Logger.Warn($"Adding {fileName} to reProcess queue (download failed)");
                        reProcess.Enqueue(item);
                    }
                    else
                    {
                        progress.Count();
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"Unexpected exception in file processing");
                }
            }

            await lastUnzip;

            queue = reProcess;
            if (reProcess.Count == 0)
            {
                break;
            }

            Logger.Info($"Retrying {reProcess.Count} files after delay. Try #{tries}");
            await Task.Delay(2000 * tries);
        }

        if (!hashCacheUpdates.IsEmpty)
        {
            Logger.Info($"Applying {hashCacheUpdates.Count} hash cache updates");
            foreach (var update in hashCacheUpdates)
            {
                hashCache[update.Key] = update.Value;
            }

            cache.SaveHashCache(hashCache);
        }

        if (queue.Count != 0)
        {
            Logger.Warn($"Failed to download {queue.Count} files after {retries} retries");
        }

        if (installProgress != null)
        {
            installProgress.FlavorText = "";
        }
    }

    private async Task<long> ProcessQueueItem(
        ControlFileItem item,
        MainControlFile controlFile,
        IAbsoluteDirectoryPath gameInstallFolder,
        Downloader.InstallingProgress installProgress,
        Task lastUnzip,
        ConcurrentDictionary<string, FileHashCache> hashCacheUpdates,
        Downloader.DownloadProgressCallback onDownloadProgress)
    {
        await lastUnzip;

        var zsyncUri = item.GetContentUri(controlFile);
        var objUri = new Uri(zsyncUri.ToString()[..(zsyncUri.ToString().Length - ControlFileItem.ZsyncExtension.Length)]);
        var zsyncFilePath = item.InstallPath.GetAbsolutePathFrom(gameInstallFolder).Path;
        var objFilePath = zsyncFilePath[..^ControlFileItem.ZsyncExtension.Length];

        var fileName = new CustomFilePath(objFilePath).FileNameWithoutExtension;

        fileOps.EnsureDirectoryExists(objFilePath);

        var bytesRead = await network.DownloadFile(zsyncUri, objFilePath, objUri, fileName, installProgress, onDownloadProgress);
        if (bytesRead <= 0)
        {
            Logger.Warn($"Download failed for {fileName}");
            return 0;
        }

        var fileToUnzip = item.InstallPath.GetAbsolutePathFrom(gameInstallFolder)
            .GetBrotherFileWithName(
                item.InstallPath.FileNameWithoutExtension.ToRelativeFilePathAuto()
                    .FileNameWithoutExtension);

        var realFile = item.GetUnzippedFileName().GetAbsolutePathFrom(gameInstallFolder);

        var unzipSuccessful = await fileOps.UnzipFile(fileToUnzip);
        if (!unzipSuccessful)
        {
            Logger.Error($"Unzip operation failed for {fileToUnzip.Path}");
            return bytesRead;
        }

        if (!realFile.Exists)
        {
            Logger.Error("File does not exist");
            return bytesRead;
        }

        var fileInfo = new FileInfo(realFile.Path);
        if (fileInfo.Length <= 0)
        {
            Logger.Warn($"File exists, but has no content : {realFile.Path}");
            return bytesRead;
        }

        var actualFileHash = realFile.ToString().GetFileHash(HashType.MD5);
        Logger.Info($"File: {realFile.Path}, Actual Hash: {actualFileHash}, Expected Hash: {item.FileHash}");

        hashCacheUpdates[realFile.Path] = new FileHashCache
        {
            FilePath = realFile.Path,
            Hash = actualFileHash,
            LastModifiedUtc = fileInfo.LastWriteTimeUtc,
            FileSize = fileInfo.Length
        };

        if (actualFileHash != item.FileHash)
        {
            Logger.Warn($"Hash mismatch for {realFile.Path}: Expected {item.FileHash}, Got {actualFileHash}");
        }

        return bytesRead;
    }

    public async Task PrepareFilesForUpdate(List<ControlFileItem> filesToProcess, ProgressData progress, IAbsoluteDirectoryPath gameInstallFolder)
    {
        var maxConcurrency = Math.Max(1, Environment.ProcessorCount - 1);
        var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = new List<Task>(filesToProcess.Count);
        progress.ResetCounter(filesToProcess.Count, true);
        var processedCount = 0;

        foreach (var controlFileItem in filesToProcess)
        {
            var fileName = controlFileItem.InstallPath?.ToString() ?? "unknown file";

            tasks.Add(Task.Run(async () => {
                await semaphore.WaitAsync();
                try
                {
                    Logger.Debug($"Compressing file: {fileName}");
                    var realFile = controlFileItem.GetUnzippedFileName().GetAbsolutePathFrom(gameInstallFolder);
                    await fileOps.CompressOriginalFile(realFile);

                    Interlocked.Increment(ref processedCount);
                    progress.Count();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Error compressing file {fileName}");
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);
        semaphore.Dispose();
        Logger.Info("Compression phase completed");
    }
}
