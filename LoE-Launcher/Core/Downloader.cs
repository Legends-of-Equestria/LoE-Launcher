using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Models;
using Models.Paths;
using Models.Paths.Json;
using Models.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using zsyncnet;
using zsyncnet.Sync;
using SyncState = zsyncnet.SyncState;

namespace LoE_Launcher.Core;

public partial class Downloader
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly int MaxFileCheckRetries = 10;
    private static readonly int InitialFileCheckDelayMs = 100;
    private static readonly int MaxNetworkRetries = 5;
    private static readonly int OfflineRetryIntervalSeconds = 30;
    private static readonly VersionInfo CurrentLauncherVersion = new() { Major = 1, Minor = 0, Build = 0, Revision = 0 };

    private readonly IRelativeFilePath _settingsFile = "settings.json".ToRelativeFilePathAuto();
    private readonly IRelativeDirectoryPath _gameInstallationFolder = ".\\Game".ToRelativeDirectoryPathAuto();
    private readonly IAbsoluteDirectoryPath _launcherPath =
        GetLauncherDirectory().ToAbsoluteDirectoryPathAuto();
    private readonly Settings _settings;

    private string _versionDownload = "";
    private GameState _state = GameState.Unknown;
    private Timer? _offlineRetryTimer;

    public DownloadData _data = null;
    public ProgressData Progress { get; private set; }
    
    public delegate void DownloadProgressCallback(long bytesAdded);

    private long _bytesDownloaded;
    public long BytesDownloaded => _bytesDownloaded;
  
    private readonly DownloadStats _downloadStats = new DownloadStats();
    public DownloadStats DownloadStats => _downloadStats;

    public static OS OperatingSystem => PlatformUtils.OperatingSystem;
    public IAbsoluteDirectoryPath GameInstallFolder => _gameInstallationFolder.GetAbsolutePathFrom(_launcherPath);
    public IAbsoluteDirectoryPath LauncherFolder => _launcherPath;
    public IAbsoluteFilePath SettingsFile => _settingsFile.GetAbsolutePathFrom(_launcherPath);
    public string CacheDirectory => Path.Combine(_launcherPath.Path, "Cache");
    public GameState State => _state;

    private string ZsyncLocation
    {
        get
        {
            if (string.IsNullOrEmpty(_versionDownload) || _versionDownload == "default")
            {
                Logger.Warn("Attempting to use ZsyncLocation with invalid version");
                return string.Empty;
            }
            return _settings.FormatZsyncLocation(_versionDownload);
        }
    }

    public long TotalGameSize
    {
        get
        {
            if (!GameInstallFolder.Exists)
            {
                return 0;
            }

            try
            {
                return GameInstallFolder.DirectoryInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(file => file.Length);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error calculating total game size");
                return 0;
            }
        }
    }

    public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
        DateParseHandling = DateParseHandling.None,
        Converters =
        {
            new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
        },
        ContractResolver = new RelativeFilePathContractResolver()
    };

    public Downloader()
    {
        Progress = new ProgressData(this);

        var settingsFile = SettingsFile;
        _settings = settingsFile.Exists
            ? JsonConvert.DeserializeObject<Settings>(File.ReadAllText(settingsFile.Path), Settings)
            : new Settings();

        Task.Run(HandlePendingDeletions);
    }

    public async Task HandlePendingDeletions()
    {
        var pendingDeleteFile = Path.Combine(_launcherPath.Path, "pending_delete.txt");
        if (!File.Exists(pendingDeleteFile))
        {
            return;
        }

        try
        {
            var filesToDelete = File.ReadAllLines(pendingDeleteFile);
            var successfulDeletions = new List<string>();

            foreach (var file in filesToDelete)
            {
                if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
                {
                    successfulDeletions.Add(file);
                    continue;
                }

                try
                {
                    if (await IsFileDeleteableAsync(file, 3))
                    {
                        File.Delete(file);
                        successfulDeletions.Add(file);
                        Logger.Info($"Deleted pending file: {file}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Failed to delete pending file: {file}");
                }
            }

            if (successfulDeletions.Count > 0)
            {
                var remainingFiles = filesToDelete.Except(successfulDeletions).ToArray();
                if (remainingFiles.Length > 0)
                {
                    File.WriteAllLines(pendingDeleteFile, remainingFiles);
                }
                else
                {
                    File.Delete(pendingDeleteFile);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling pending deletions");
        }
    }

    public async Task RefreshState()
    {
        try
        {
            Logger.Info("Beginning game state refresh");

            _state = GameState.Unknown;
            Progress = new RefreshProgress(this) { Marquee = true };

            using (new Processing(Progress))
            {
                try
                {
                    // Check launcher version compatibility first
                    await CheckLauncherVersion();
                    
                    if (_state == GameState.LauncherOutOfDate)
                    {
                        Logger.Warn("Launcher is out of date, stopping refresh");
                        return;
                    }

                    await GetVersion();

                    if (_state == GameState.Offline)
                    {
                        Logger.Warn("Offline state detected after version check, exiting refresh");
                        return;
                    }

                    // Make sure we have a valid version download path
                    if (string.IsNullOrEmpty(_versionDownload) || _versionDownload == "default")
                    {
                        Logger.Warn("No valid version path available, marking as offline but allowing local gameplay");

                        if (GameInstallFolder.Exists && File.Exists(Path.Combine(GameInstallFolder.Path, "loe.exe")))
                        {
                            Logger.Info("Found existing game installation, allowing launch in offline mode");
                            _state = GameState.UpToDate; // Allow player to launch the game without updates
                        }
                        else
                        {
                            Logger.Warn("No existing installation found, setting to offline mode");
                            _state = GameState.Offline;
                        }
                        return;
                    }

                    var url = new Uri(ZsyncLocation + ".zsync-control.jar");
                    Logger.Info($"Downloading control file from: {url}");

                    var data = await DownloadMainControlFile(url);
                    if (data == null)
                    {
                        Logger.Warn("Control file data is null, likely offline or server issue");
                        // Don't override state if it's already set to ServerMaintenance
                        if (_state != GameState.ServerMaintenance)
                        {
                            _state = GameState.Offline;
                        }
                        return;
                    }

                    _data = data;

                    if (_data.ControlFile.RootUri == null)
                    {
                        _data.ControlFile.RootUri = new Uri(ZsyncLocation);
                    }

                    if (_data.ToProcess.Count == 0)
                    {
                        Logger.Info("No files to process, game is up to date");
                        _state = GameState.UpToDate;
                        return;
                    }

                    Logger.Info($"Need to process {_data.ToProcess.Count} files");

                    if (!GameInstallFolder.Exists)
                    {
                        Logger.Info($"Game folder doesn't exist: {GameInstallFolder.Path}");
                        _state = GameState.NotFound;
                        return;
                    }

                    Logger.Info("Update available");
                    _state = GameState.UpdateAvailable;
                }
                catch (UriFormatException uriEx)
                {
                    Logger.Error(uriEx, "Invalid URI format");
                    _state = GameState.Offline;
                }
                catch (HttpRequestException httpEx)
                {
                    Logger.Error(httpEx, "HTTP request failed");
                    _state = GameState.Offline;
                }
                catch (TaskCanceledException tcEx)
                {
                    Logger.Error(tcEx, "Network request timed out");
                    _state = GameState.Offline;
                }
                catch (Exception innerEx)
                {
                    Logger.Error(innerEx, "Error during state refresh");
                    _state = GameState.Unknown;
                    throw; // Re-throw for outer catch
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "RefreshState failed with an unhandled exception");
            _state = GameState.Unknown;
        }
        finally
        {
            Logger.Info($"State refresh completed. Final state: {_state}");
            Progress.Complete();
            
            if (_state == GameState.Offline)
            {
                StartOfflineRetryTimer();
            }
            else
            {
                StopOfflineRetryTimer();
            }
        }
    }

    private async Task CheckLauncherVersion()
    {
        Logger.Info($"Checking launcher version compatibility from {_settings.LauncherVersionUrl}");
        
        try
        {
            var launcherVersionInfo = await DownloadJson<LauncherVersionInfo>(new Uri(_settings.LauncherVersionUrl));
            
            if (launcherVersionInfo == null)
            {
                Logger.Warn("Could not retrieve launcher version info, assuming compatible");
                return;
            }

            Logger.Info($"Current launcher version: {CurrentLauncherVersion}");
            Logger.Info($"Server required version: {launcherVersionInfo.RequiredVersion}");
            
            if (!launcherVersionInfo.IsCompatible(CurrentLauncherVersion))
            {
                Logger.Warn($"Launcher version {CurrentLauncherVersion} is not compatible with server requirements");
                Logger.Warn($"Server requires: {launcherVersionInfo.RequiredVersion}");
                _state = GameState.LauncherOutOfDate;
                return;
            }

            Logger.Info($"Launcher version {CurrentLauncherVersion} is compatible");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to check launcher version, assuming compatible");
            // Don't block the launcher if version check fails - assume compatible
        }
    }

    private async Task GetVersion()
    {
        Logger.Info($"Getting version info from {_settings.Stream}");
        var data = await DownloadJson<VersionsControlFile>(new Uri(_settings.Stream));

        // If data is null (likely offline), create a fallback version to avoid NullReferenceException
        if (data == null)
        {
            Logger.Warn("Version data is null, using fallback default version");
            _versionDownload = "default";
            return;
        }

        Logger.Info($"Successfully retrieved version info. Using OS: {OperatingSystem}");
        switch (OperatingSystem)
        {
            case OS.WindowsX86:
                _versionDownload = data.Win32;
                break;
            case OS.WindowsX64:
                _versionDownload = data.Win64;
                break;
            case OS.MacArm:
                _versionDownload = data.Mac_Arm;
                break;
            case OS.MacIntel:
                _versionDownload = data.Mac_Intel;
                break;
            case OS.X11:
                _versionDownload = data.Linux;
                break;
            case OS.Other:
                _versionDownload = data.Win32;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        Logger.Info($"Using version download path: {_versionDownload}");
    }

    public async Task DoInstallation()
    {
        var totalFiles = _data.ToProcess.Count;
        Logger.Info($"Starting installation/update process, {totalFiles} files to process");

        try
        {
            if (_data.ToProcess.Count == 0)
            {
                Logger.Error("No files to process in DoInstallation");
                _state = GameState.Unknown;
                return;
            }
            
            _downloadStats.Reset();

            // First prepare all files - compression phase
            Logger.Info("Starting compression phase");
            Progress = new PreparingProgress(this) { Marquee = true };
            using (new Processing(Progress))
            {
                var maxConcurrency = Math.Max(1, Environment.ProcessorCount - 1);
                var semaphore = new SemaphoreSlim(maxConcurrency);
                var tasks = new List<Task>(_data.ToProcess.Count);
                Progress.ResetCounter(_data.ToProcess.Count, true);
                var processedCount = 0;

                foreach (var controlFileItem in _data.ToProcess)
                {
                    var fileName = controlFileItem.InstallPath?.ToString() ?? "unknown file";

                    tasks.Add(Task.Run(async () => {
                        await semaphore.WaitAsync();
                        try
                        {
                            Logger.Debug($"Compressing file: {fileName}");
                            var realFile = controlFileItem.GetUnzippedFileName().GetAbsolutePathFrom(GameInstallFolder);
                            await CompressOriginalFile(realFile);

                            Interlocked.Increment(ref processedCount);
                            Progress.Count();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, $"Error compressing file {fileName}");
                            // Don't rethrow - we want to continue with other files
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

            // Then install all files
            Logger.Info("Starting installation phase");
            Progress = new InstallingProgress(this) { Marquee = true };
            using (new Processing(Progress))
            {
                await UpdateFiles(3);
            }

            Logger.Info("Installation completed, running cleanup");
            await Cleanup();

            Logger.Info("Refreshing state after installation");
            await RefreshState();

            Logger.Info($"Installation process completed. Current state: {_state}");
        }
        catch (AggregateException aex)
        {
            foreach (var ex in aex.InnerExceptions)
            {
                Logger.Error(ex, $"Installation failed (inner exception): {ex.Message}");
            }

            await HandleInstallationFailure("Multiple errors occurred during installation");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Installation failed: {ex.Message}");
            await HandleInstallationFailure(ex.Message);
        }
    }

    private async Task HandleInstallationFailure(string errorMessage)
    {
        Logger.Warn($"Installation failed: {errorMessage}. Running cleanup and refresh.");
        try
        {
            await Cleanup();
        }
        catch (Exception cleanupEx)
        {
            Logger.Error(cleanupEx, "Error during cleanup after failed installation");
        }

        try
        {
            await RefreshState();
        }
        catch (Exception refreshEx)
        {
            Logger.Error(refreshEx, "Error refreshing state after failed installation");
            _state = GameState.Unknown;
        }
    }

    public async Task PrepareUpdate()
    {
        Progress = new PreparingProgress(this) { Marquee = true };

        using (new Processing(Progress))
        {
            var maxConcurrency = Math.Max(1, Environment.ProcessorCount - 1);
            var semaphore = new SemaphoreSlim(maxConcurrency);
            var tasks = new List<Task>(_data.ToProcess.Count);
            Progress.ResetCounter(_data.ToProcess.Count, true);

            foreach (var controlFileItem in _data.ToProcess)
            {
                tasks.Add(Task.Run(async () => {
                    await semaphore.WaitAsync();
                    try
                    {
                        await CompressOriginalFile(controlFileItem.GetUnzippedFileName().GetAbsolutePathFrom(GameInstallFolder));
                        Progress.Count();
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            semaphore.Dispose();
        }
    }

    public async Task InstallUpdate()
    {
        Progress = new InstallingProgress(this) { Marquee = true };

        using (new Processing(Progress))
        {
            await UpdateFiles(3);
        }
    }

    private async Task UpdateFiles(int retries = 0)
    {
        var installProgress = Progress as InstallingProgress;
        var tries = 0;
        var queue = new Queue<ControlFileItem>(_data.ToProcess);

        Progress.ResetCounter(queue.Count, true);
        _bytesDownloaded = 0;

        var hashCache = LoadHashCache();
        var hashCacheUpdates = new ConcurrentDictionary<string, FileHashCache>();

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("LoE-Launcher/1.0");
        httpClient.Timeout = TimeSpan.FromMinutes(5);

        if (!await TestConnectionIfNeeded(queue, httpClient))
        {
            return;
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
                    var bytesRead = await ProcessQueueItem(item, installProgress, lastUnzip, hashCacheUpdates);

                    if (bytesRead == 0)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(item.InstallPath.Path);
                        Logger.Warn($"Adding {fileName} to reProcess queue (download failed)");
                        reProcess.Enqueue(item);
                    }
                    else
                    {
                        Progress.Count();
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

            SaveHashCache(hashCache);
        }

        if (queue.Count != 0)
        {
            Logger.Warn($"Failed to download {queue.Count} files after {retries} retries");
        }

        installProgress.FlavorText = "";
    }

    private async Task<bool> TestConnectionIfNeeded(Queue<ControlFileItem> queue, HttpClient httpClient)
    {
        if (queue.Count == 0)
        {
            return true;
        }

        var testItem = queue.Peek();
        var testUri = testItem.GetContentUri(_data.ControlFile);
        
        for (int attempt = 0; attempt < MaxNetworkRetries; attempt++)
        {
            try
            {
                Logger.Info($"Testing connection to: {testUri} (attempt {attempt + 1}/{MaxNetworkRetries})");
                var response = await httpClient.GetAsync(testUri, HttpCompletionOption.ResponseHeadersRead);
                Logger.Info($"Test connection successful: {response.StatusCode}");
                return true;
            }
            catch (Exception ex) when (attempt < MaxNetworkRetries - 1)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                Logger.Warn($"Connection test failed, retrying in {delay.TotalSeconds}s: {ex.Message}");
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "All connection test attempts failed");
                _state = GameState.Offline;
                return false;
            }
        }
        
        return false;
    }

    private async Task<long> ProcessQueueItem(
        ControlFileItem item,
        InstallingProgress installProgress,
        Task lastUnzip,
        ConcurrentDictionary<string, FileHashCache> hashCacheUpdates)
    {
        // Wait for any previous unzip operation to complete first
        await lastUnzip;

        var zsyncUri = item.GetContentUri(_data.ControlFile);
        var objUri = new Uri(zsyncUri.ToString()[..(zsyncUri.ToString().Length - ControlFileItem.ZsyncExtension.Length)]);
        var zsyncFilePath = item.InstallPath.GetAbsolutePathFrom(GameInstallFolder).Path;
        var objFilePath = zsyncFilePath[..^ControlFileItem.ZsyncExtension.Length];

        var fileName = new CustomFilePath(objFilePath).FileNameWithoutExtension;

        EnsureDirectoryExists(objFilePath);

        var bytesRead = await DownloadFile(zsyncUri, objFilePath, objUri, fileName, installProgress, OnDownloadProgress);
        if (bytesRead <= 0)
        {
            Logger.Warn($"Download failed for {fileName}");
            return 0;
        }

        var fileToUnzip = item.InstallPath.GetAbsolutePathFrom(GameInstallFolder)
            .GetBrotherFileWithName(
                item.InstallPath.FileNameWithoutExtension.ToRelativeFilePathAuto()
                    .FileNameWithoutExtension);

        var realFile = item.GetUnzippedFileName().GetAbsolutePathFrom(GameInstallFolder);

        var unzipSuccessful = await UnzipFile(fileToUnzip);
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


    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static async Task<long> DownloadFile(Uri zsyncUri, string objFilePath, Uri objUri, string fileName,
        InstallingProgress installProgress, DownloadProgressCallback progressCallback = null)
    {
        long bytesRead;
        try
        {
            bytesRead = await SyncFileWithZsync(zsyncUri, objFilePath, objUri, fileName, installProgress, progressCallback);
            if (bytesRead == 0)
            {
                Logger.Info($"ZSync returned 0 bytes, trying direct download for: {fileName}");
                bytesRead = await DirectDownloadFile(objUri, objFilePath, progressCallback);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, $"File download failed for {fileName}");
            DeleteFileIfExists(objFilePath);
            try
            {
                Logger.Info($"Trying direct download fallback for: {fileName}");
                bytesRead = await DirectDownloadFile(objUri, objFilePath, progressCallback);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Direct download fallback failed for {fileName}");
                bytesRead = 0;
            }
        }
        return bytesRead;
    }


    private static void DeleteFileIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch
            {
                // Ignore deletion failures
            }
        }
    }

    private static async Task<long> SyncFileWithZsync(Uri zsyncUri, string objFilePath, Uri objUri,
        string fileName, InstallingProgress installProgress, DownloadProgressCallback progressCallback = null)
    {
        Logger.Info($"Attempting zsync for: {fileName}");

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LoE-Launcher/1.0");
        client.Timeout = TimeSpan.FromMinutes(2);

        var downloader = new ProgressReportingRangeDownloader(objUri, client, progressCallback);
        try
        {
            Logger.Info($"Downloading control file from: {zsyncUri}");

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                var controlFile = await Task.Run(() => DownloadControlFile(zsyncUri, cts.Token), cts.Token);
                if (controlFile == null)
                {
                    Logger.Error($"Failed to download control file for {fileName}");
                    return 0;
                }

                Logger.Info($"Control file downloaded successfully for {fileName}");
                var outputDir = new DirectoryInfo(Path.GetDirectoryName(objFilePath));

                var progressAdapter = progressCallback != null
                    ? new Progress<ulong>(bytes => progressCallback((long)bytes))
                    : null;

                var syncTask = Task.Run(() => {
                    Zsync.Sync(controlFile, downloader, outputDir, (ss) => {
                        var flavor = ss switch
                        {
                            SyncState.CalcDiff => $"{fileName} diff",
                            SyncState.CopyExisting => $"{fileName} copying parts",
                            SyncState.DownloadPatch => $"{fileName} downloading patch",
                            SyncState.DownloadNew => $"{fileName} downloading",
                            SyncState.PatchFile => $"{fileName} patching",
                            _ => ""
                        };
                        installProgress.FlavorText = flavor;
                        Logger.Debug($"ZSync state: {ss} - {flavor}");
                    }, progressAdapter, cts.Token);
                }, cts.Token);

                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(3), cts.Token);
                if (await Task.WhenAny(syncTask, timeoutTask) == timeoutTask)
                {
                    Logger.Error($"Zsync operation timed out for {fileName}");
                    throw new TimeoutException($"Zsync operation timed out for {fileName}");
                }

                await syncTask;
            }

            var fileInfo = new FileInfo(objFilePath);
            if (fileInfo.Exists)
            {
                Logger.Info($"Zsync complete. File exists: {objFilePath}, size: {fileInfo.Length} bytes");
                return fileInfo.Length;
            }

            Logger.Warn($"File does not exist after zsync: {objFilePath}");
            return 0;
        }
        catch (TimeoutException tex)
        {
            Logger.Error(tex, $"Zsync timed out: {tex.Message}");
            return 0;
        }
        catch (TaskCanceledException tcex)
        {
            Logger.Error(tcex, $"Zsync operation was canceled: {tcex.Message}");
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error during zsync: {ex.Message}");
            return 0; // Return 0 instead of throwing to allow falling back to direct download
        }
    }

    private static async Task<bool> IsFileDeleteableAsync(string filePath, int maxRetries = 5, int initialDelayMs = 100)
    {
        if (!File.Exists(filePath))
        {
            return true;
        }

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);

                var tempDeleteTest = filePath + ".deletetest";
                try
                {
                    File.Create(tempDeleteTest).Dispose();
                    File.Delete(tempDeleteTest);
                    return true;
                }
                catch
                {
                    if (i == maxRetries - 1)
                    {
                        Logger.Warn($"Unable to create test file near {filePath}");
                        return false;
                    }
                }
            }
            catch (IOException ex) when (ex.Message.Contains("being used by another process"))
            {
                var delay = initialDelayMs * (1 << Math.Min(i, 10));
                Logger.Debug($"File locked for deletion, retry {i + 1}/{maxRetries} in {delay}ms: {filePath}");
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error checking if file is deleteable: {filePath}");
                return false;
            }
        }

        return false;
    }

    private static async Task<string> GetSafeTargetPathAsync(string filePath)
    {
        if (!File.Exists(filePath) || await IsFileDeleteableAsync(filePath))
        {
            return filePath;
        }

        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        for (var i = 1; i <= 10; i++)
        {
            var alternativePath = Path.Combine(directory, $"{fileName}.new{i}{extension}");
            if (!File.Exists(alternativePath))
            {
                Logger.Info($"Using alternative path: {alternativePath}");
                return alternativePath;
            }
        }

        var guidPath = Path.Combine(directory, $"{fileName}.{Guid.NewGuid()}{extension}");
        Logger.Info($"Using GUID alternative path: {guidPath}");
        return guidPath;
    }

    private static async Task<long> DirectDownloadFile(Uri fileUri, string filePath, DownloadProgressCallback progressCallback = null)
    {
        Logger.Info($"Attempting direct download: {fileUri} -> {filePath}");

        var tempFilePath = filePath + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Logger.Info($"Created directory: {directory}");
            }

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LoE-Launcher/1.0");

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var response = await client.GetAsync(
                fileUri,
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Error($"Server returned error status: {response.StatusCode}");
                return 0;
            }

            var contentLength = response.Content.Headers.ContentLength ?? -1;
            Logger.Debug($"Content length: {contentLength} bytes");

            await using var contentStream = await response.Content.ReadAsStreamAsync(cts.Token);

            long totalBytesRead = 0;
            int bytesRead;
            var startTime = DateTime.UtcNow;
            var lastProgressReport = DateTime.UtcNow;

            Logger.Debug($"Creating temporary file: {tempFilePath}");
            await using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                var buffer = new byte[81920];

                while ((bytesRead = await contentStream.ReadAsync(buffer, cts.Token)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token);
                    totalBytesRead += bytesRead;

                    progressCallback?.Invoke(bytesRead);

                    var now = DateTime.UtcNow;
                    if ((now - lastProgressReport).TotalSeconds >= 2)
                    {
                        var progressElapsed = (now - startTime).TotalSeconds;
                        var bytesPerSecond = totalBytesRead / progressElapsed;
                        var percentComplete = contentLength > 0 ? (totalBytesRead * 100.0 / contentLength) : 0;

                        Logger.Info($"Download progress: {totalBytesRead:N0}/{contentLength:N0} bytes ({percentComplete:F1}%) - {bytesPerSecond:N0} bytes/sec");
                        lastProgressReport = now;
                    }
                }

                Logger.Info("Download completed, closing stream");
                await fileStream.FlushAsync(cts.Token);
            }

            var actualTargetPath = await GetSafeTargetPathAsync(filePath);

            Logger.Debug($"Moving temp file to destination: {tempFilePath} -> {actualTargetPath}");
            File.Move(tempFilePath, actualTargetPath, true);

            if (actualTargetPath != filePath)
            {
                Logger.Info($"Used alternative path due to file lock. Original: {filePath}, Actual: {actualTargetPath}");
            }

            var totalElapsed = (DateTime.UtcNow - startTime).TotalSeconds;
            var overallSpeed = totalBytesRead / totalElapsed;
            Logger.Info($"Direct download complete: {totalBytesRead:N0} bytes in {totalElapsed:F1} seconds ({overallSpeed:N0} bytes/sec)");

            return totalBytesRead;
        }
        catch (TaskCanceledException tcex)
        {
            Logger.Error(tcex, "Direct download timed out or was canceled");
            await CleanupDownloadFiles(tempFilePath, filePath);
            return 0;
        }
        catch (HttpRequestException hrex)
        {
            Logger.Error(hrex, $"HTTP error during direct download: {hrex.Message}");
            await CleanupDownloadFiles(tempFilePath, filePath);
            return 0;
        }
        catch (IOException ioex)
        {
            Logger.Error(ioex, $"IO error during direct download: {ioex.Message}");

            if (ioex.Message.Contains("being used by another process"))
            {
                if (File.Exists(tempFilePath))
                {
                    var fileInfo = new FileInfo(tempFilePath);
                    if (fileInfo.Length > 0)
                    {
                        try
                        {
                            var alternativePath = await GetSafeTargetPathAsync(filePath + ".alt");
                            Logger.Info($"Attempting to save to alternative location due to lock: {alternativePath}");
                            File.Move(tempFilePath, alternativePath, true);

                            return fileInfo.Length;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "Failed to use alternative path after lock");
                        }
                    }
                }
            }

            await CleanupDownloadFiles(tempFilePath, filePath);
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Direct download failed: {ex.Message}");
            await CleanupDownloadFiles(tempFilePath, filePath);
            return 0;
        }
    }

    private static async Task CleanupDownloadFiles(string tempFilePath, string filePath)
    {
        if (File.Exists(tempFilePath))
        {
            try
            {
                Logger.Info($"Deleting temporary file: {tempFilePath}");

                if (await IsFileDeleteableAsync(tempFilePath, 5))
                {
                    File.Delete(tempFilePath);
                }
                else
                {
                    Logger.Warn($"Unable to delete locked temporary file: {tempFilePath}");
                    try
                    {
                        var pendingDeleteFile = Path.Combine(
                            Path.GetDirectoryName(tempFilePath),
                            "pending_delete.txt");

                        File.AppendAllLines(pendingDeleteFile, new[] { tempFilePath });
                    }
                    catch
                    {
                        Logger.Warn($"Failed to schedule {tempFilePath} for later deletion");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to delete temporary file: {tempFilePath}");
            }
        }

        if (File.Exists(filePath) && File.Exists(tempFilePath) && new FileInfo(tempFilePath).Length > 0)
        {
            try
            {
                Logger.Info($"Deleting target file: {filePath}");

                if (await IsFileDeleteableAsync(filePath, 5))
                {
                    File.Delete(filePath);
                }
                else
                {
                    Logger.Warn($"Unable to delete locked target file: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to delete target file: {filePath}");
            }
        }
    }

    private static ControlFile? DownloadControlFile(Uri uri, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LoE-Launcher/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = client.Send(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Error($"Failed to download control file. Status code: {response.StatusCode}");
                return null;
            }

            using var stream = response.Content.ReadAsStream(cancellationToken);
            return new ControlFile(stream);
        }
        catch (OperationCanceledException ocex)
        {
            Logger.Error(ocex, "Control file download was canceled");
            return null;
        }
        catch (HttpRequestException hrex)
        {
            Logger.Error(hrex, $"HTTP error downloading control file: {hrex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error downloading control file: {ex.Message}");
            return null;
        }
    }

    public async Task Cleanup()
    {
        Progress = new CleanupProgress(this) { Marquee = true };
        using (new Processing(Progress))
        {
            const int maxRetries = 3;
            for (var i = 0; i < maxRetries; i++)
            {
                try
                {
                    CleanGameFolder();
                    break;
                }
                catch (IOException ex) when (ex.Message.Contains("being used by another process"))
                {
                    if (i == maxRetries - 1)
                    {
                        throw;
                    }
                    Logger.Warn($"Files still in use during cleanup, retry {i + 1}/{maxRetries}");
                    await Task.Delay(1000 * (i + 1));
                }
            }
        }
    }

    public async Task RepairGame()
    {
        Logger.Info("Starting game repair process");

        Progress = new RepairingProgress(this) { Marquee = true };

        try
        {
            using (new Processing(Progress))
            {
                // Reset state from previous sessions
                _bytesDownloaded = 0;
    
                var cacheFile = Path.Combine(CacheDirectory, "hash_cache.json");
                if (File.Exists(cacheFile))
                {
                    File.Delete(cacheFile);
                    Logger.Info("Deleted hash cache for repair process");
                }

                _state = GameState.Unknown;

                await GetVersion();

                if (_state == GameState.Offline)
                {
                    Logger.Warn("Cannot repair in offline mode");
                    throw new InvalidOperationException("Cannot repair game files while offline. Please check your internet connection.");
                }

                var url = new Uri($"{ZsyncLocation}.zsync-control.jar");
                Logger.Info($"Downloading control file from: {url}");

                var data = await DownloadMainControlFile(url);
                if (data == null)
                {
                    throw new InvalidOperationException("Failed to download game verification data.");
                }

                _data = data;

                if (_data.ControlFile.RootUri == null)
                {
                    _data.ControlFile.RootUri = new Uri(ZsyncLocation);
                }

                if (!GameInstallFolder.Exists)
                {
                    Directory.CreateDirectory(GameInstallFolder.Path);
                    Logger.Info("Created game installation directory");
                }

                if (_data.ToProcess.Count == 0)
                {
                    Logger.Info("No files need repair");
                    return;
                }

                Logger.Info($"Found {_data.ToProcess.Count} files that need repair");

                await DoInstallation();

                Logger.Info("Repair process completed");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Game repair failed");
            throw;
        }
        finally
        {
            Progress.Complete();
            await RefreshState();
        }
    }

    private void CleanGameFolder()
    {
        try
        {
            if (!GameInstallFolder.Exists)
            {
                Directory.CreateDirectory(GameInstallFolder.ToString());
            }

            var extensions = new[] { "*.zsync", "*.jar", "*.gz", "*.zs-old" };
            foreach (var extension in extensions)
            {
                foreach (var file in GameInstallFolder.DirectoryInfo.EnumerateFiles(extension, SearchOption.AllDirectories))
                {
                    try
                    {
                        file.Delete();
                    }
                    catch (IOException ex) when (ex.Message.Contains("being used by another process"))
                    {
                        Logger.Warn($"Could not delete {file.FullName}: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Error deleting {file.FullName}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "CleanGameFolder failed");
            Progress = new ErrorProgress($"Could not clean game folder", this);
            _state = GameState.Unknown;
            throw;
        }
    }

    private static async Task<bool> UnzipFile(IAbsoluteFilePath file)
    {
        var outputFile = file.GetBrotherFileWithName(file.FileNameWithoutExtension).Path;
        if (!await FileIsReadyAsync(file.Path, MaxFileCheckRetries, InitialFileCheckDelayMs))
        {
            Logger.Error($"File not ready for unzipping after {MaxFileCheckRetries} attempts: {file.Path}");
            throw new IOException($"File locked: {file.Path}");
        }

        try
        {
            if (IsGzipFile(file.Path))
            {
                await using (var inputStream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                await using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
                await using (var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await gzipStream.CopyToAsync(outputStream);
                    await outputStream.FlushAsync();
                }

                var extractedSize = new FileInfo(outputFile).Length;
                var originalSize = new FileInfo(file.Path).Length;
                Logger.Info($"Unzipped file: {file.Path}, compressed size: {originalSize}, extracted size: {extractedSize}");
            }
            else
            {
                Logger.Info($"File is not GZip, copying directly: {file.Path}");
                File.Copy(file.Path, outputFile, true);
                Logger.Info($"Successfully copied: {file.Path} to {outputFile}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error unzipping file {file.Path}");
            return false;
        }
    }

    private static bool IsGzipFile(string filePath)
    {
        try
        {
            // GZip files start with the magic numbers 0x1F 0x8B
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length < 2)
            {
                return false;
            }

            var header = new byte[2];
            fs.ReadExactly(header, 0, 2);
            return header[0] == 0x1F && header[1] == 0x8B;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error checking if file is GZip: {filePath}");
            return false;
        }
    }

    private static async Task<bool> FileIsReadyAsync(string filePath, int maxRetries = 10, int initialDelayMs = 100)
    {
        if (!File.Exists(filePath))
        {
            Logger.Warn($"File does not exist: {filePath}");
            return false;
        }

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length > 0)
                {
                    var buffer = new byte[Math.Min(1024, fs.Length)];
                    await fs.ReadExactlyAsync(buffer, 0, buffer.Length);
                }

                return true;
            }
            catch (IOException ex) when (ex.Message.Contains("being used by another process"))
            {
                var delay = initialDelayMs * (1 << Math.Min(i, 10));
                Logger.Debug($"File locked, retry {i + 1}/{maxRetries} in {delay}ms: {filePath}");
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error checking if file is ready: {filePath}");
                return false;
            }
        }
        return false;
    }

    private async Task<DownloadData?> DownloadMainControlFile(Uri url)
    {
        var mainControlFile = await DownloadJson<MainControlFile>(url);
        if (mainControlFile == null)
        {
            return null;
        }

        var data = new DownloadData(mainControlFile);

        Progress.ResetCounter(data.ControlFile.Content.Count, true);

        var filesToProcess = new List<ControlFileItem>();
        var processedCount = 0;

        var hashCache = LoadHashCache();
        var cacheUpdates = new Dictionary<string, FileHashCache>();

        // Group by directory for better locality, which minimizes disk seeks
        var filesByDirectory = data.ControlFile.Content
            .GroupBy(item => Path.GetDirectoryName(
                item.GetUnzippedFileName().GetAbsolutePathFrom(GameInstallFolder).Path))
            .ToList();

        // Process each directory sequentially
        foreach (var item in filesByDirectory.SelectMany(directoryGroup => directoryGroup))
        {
            try
            {
                var realFile = item.GetUnzippedFileName().GetAbsolutePathFrom(GameInstallFolder);
                var fileMatchesHash = false;

                if (realFile.Exists)
                {
                    var fileInfo = new FileInfo(realFile.Path);

                    if (hashCache.TryGetValue(realFile.Path, out var cachedInfo) &&
                        fileInfo.LastWriteTimeUtc == cachedInfo.LastModifiedUtc &&
                        fileInfo.Length == cachedInfo.FileSize)
                    {
                        fileMatchesHash = cachedInfo.Hash == item.FileHash;
                        Logger.Info($"{item._installPath}: Hash : {cachedInfo.Hash} : {item.FileHash} : Match={fileMatchesHash}");
                    }
                    else
                    {
                        // No valid cache entry, try to get the hash directly
                        var fileHash = realFile.ToString().GetFileHash(HashType.MD5);
                        fileMatchesHash = fileHash == item.FileHash;
                        Logger.Info($"{item._installPath}: Direct hash : {fileHash} : {item.FileHash} : Match={fileMatchesHash}");

                        // Add to cache updates regardless of match
                        if (!hashCache.TryGetValue(realFile.Path, out var value) || value.Hash != fileHash)
                        {
                            cacheUpdates[realFile.Path] = new FileHashCache
                            {
                                FilePath = realFile.Path,
                                Hash = fileHash,
                                LastModifiedUtc = fileInfo.LastWriteTimeUtc,
                                FileSize = fileInfo.Length
                            };
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
                Dispatcher.UIThread.Post(() => Progress.SetCount(processedCount));
            }
        }

        foreach (var update in cacheUpdates)
        {
            hashCache[update.Key] = update.Value;
        }

        SaveHashCache(hashCache);

        data.ToProcess = filesToProcess;

        return data;
    }

    private Dictionary<string, FileHashCache> LoadHashCache()
    {
        try
        {
            if (!Directory.Exists(CacheDirectory))
            {
                Directory.CreateDirectory(CacheDirectory);
            }
            
            var cacheFile = Path.Combine(CacheDirectory, "hash_cache.json");
            if (File.Exists(cacheFile))
            {
                var json = File.ReadAllText(cacheFile);
                return JsonConvert.DeserializeObject<Dictionary<string, FileHashCache>>(json)
                    ?? new Dictionary<string, FileHashCache>();
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to load hash cache, creating new one");
        }

        return new Dictionary<string, FileHashCache>();
    }

    private void SaveHashCache(Dictionary<string, FileHashCache> cache)
    {
        try
        {
            if (!Directory.Exists(CacheDirectory))
            {
                Directory.CreateDirectory(CacheDirectory);
            }
            
            var cacheFile = Path.Combine(CacheDirectory, "hash_cache.json");
            var json = JsonConvert.SerializeObject(cache);
            File.WriteAllText(cacheFile, json);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to save hash cache");
        }
    }

    private async Task<TType?> DownloadJson<TType>(Uri url)
        where TType : class
    {
        for (int attempt = 0; attempt < MaxNetworkRetries; attempt++)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(Math.Min(10 + attempt * 5, 30));
                client.DefaultRequestHeaders.UserAgent.ParseAdd("LoE-Launcher/1.0");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Min(10 + attempt * 5, 30)));
                using var response = await client.GetAsync(url, cts.Token);
                
                if (response.StatusCode 
                    is System.Net.HttpStatusCode.NotFound 
                    or System.Net.HttpStatusCode.InternalServerError 
                    or System.Net.HttpStatusCode.BadGateway 
                    or System.Net.HttpStatusCode.ServiceUnavailable 
                    or System.Net.HttpStatusCode.GatewayTimeout)
                {
                    Logger.Error($"Server error {(int)response.StatusCode} {response.StatusCode} @ {url}");
                    _state = GameState.ServerMaintenance;
                    return null;
                }
                
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync(cts.Token);

                Logger.Info($"JSON download successful (attempt {attempt + 1})");
                return JsonConvert.DeserializeObject<TType>(result, Settings);
            }
            catch (OperationCanceledException) when (attempt < MaxNetworkRetries - 1)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                Logger.Warn($"Request timed out @ {url}, retrying in {delay.TotalSeconds}s");
                await Task.Delay(delay);
            }
            catch (HttpRequestException) when (attempt < MaxNetworkRetries - 1)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                Logger.Warn($"HTTP request failed @ {url}, retrying in {delay.TotalSeconds}s");
                await Task.Delay(delay);
            }
            catch (Exception) when (attempt < MaxNetworkRetries - 1)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                Logger.Warn($"Could not download json @ {url}, retrying in {delay.TotalSeconds}s");
                await Task.Delay(delay);
            }
        }

        Logger.Error($"All download attempts failed for {url}");
        _state = GameState.Offline;
        return null;
    }

    private static async Task CompressOriginalFile(IAbsoluteFilePath realFile)
    {
        if (realFile.Exists)
        {
            await FileIsReadyAsync(realFile.Path, maxRetries: 6);
            var compressedFile = realFile.GetBrotherFileWithName(realFile.FileName + ".jar");

            await using var inputStream = new FileStream(realFile.Path, FileMode.Open);
            await using var outputStream = new FileStream(compressedFile.Path, FileMode.Create);
            await using var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal);

            await inputStream.CopyToAsync(gzipStream);
        }
    }

    private void OnDownloadProgress(long bytesAdded)
    {
        Interlocked.Add(ref _bytesDownloaded, bytesAdded);
    
        double progressPercentage = 0;
        if (Progress.Max > 0)
        {
            progressPercentage = (double)Progress.Current / Progress.Max * 100;
        }
    
        _downloadStats.Update(_bytesDownloaded, progressPercentage);
    }

    public void SaveSettings()
    {
        try
        {
            var settingsJson = JsonConvert.SerializeObject(_settings, Formatting.Indented);
            File.WriteAllText(SettingsFile.Path, settingsJson);
            Logger.Info("Settings saved successfully");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to save settings");
        }
    }

    private static string GetLauncherDirectory()
    {
        return Directory.GetCurrentDirectory();
    }

    private void StartOfflineRetryTimer()
    {
        StopOfflineRetryTimer();
        
        Logger.Info($"Starting offline retry timer (checks every {OfflineRetryIntervalSeconds} seconds)");
        _offlineRetryTimer = new Timer(async _ =>
        {
            try
            {
                Logger.Info("Offline retry timer triggered, checking connectivity");
                await CheckConnectivityAndRefresh();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during offline retry check");
            }
        }, null, TimeSpan.FromSeconds(OfflineRetryIntervalSeconds), TimeSpan.FromSeconds(OfflineRetryIntervalSeconds));
    }

    private void StopOfflineRetryTimer()
    {
        _offlineRetryTimer?.Dispose();
        _offlineRetryTimer = null;
    }

    private async Task CheckConnectivityAndRefresh()
    {
        if (_state != GameState.Offline)
        {
            return;
        }

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LoE-Launcher/1.0");
            
            var testUrl = new Uri(_settings.Stream);
            using var response = await client.GetAsync(testUrl, HttpCompletionOption.ResponseHeadersRead);
            
            if (response.IsSuccessStatusCode)
            {
                Logger.Info("Connectivity restored, refreshing state");
                await RefreshState();
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"Connectivity check failed: {ex.Message}");
        }
    }

    public class DownloadData(MainControlFile controlFile)
    {
        public MainControlFile ControlFile { get; private set; } = controlFile;
        public List<ControlFileItem> ToProcess { get; set; } = [];
    }
}

public enum GameState
{
    Unknown,
    NotFound,
    UpdateAvailable,
    UpToDate,
    Offline,
    ServerMaintenance,
    LauncherOutOfDate
}

public class FileHashCache
{
    public string FilePath { get; set; }
    public string Hash { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public long FileSize { get; set; }
}

public class Processing : IDisposable
{
    private readonly ProgressData _state;
    public Processing(ProgressData state)
    {
        _state = state;
        _state.Processing = true;
    }

    public void Dispose()
    {
        _state.Processing = false;
        _state.IsFinished = true;
        GC.SuppressFinalize(this);
    }
}
