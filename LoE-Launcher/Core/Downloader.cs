using LoE_Launcher.Core.Models;
using LoE_Launcher.Core.Models.Paths;
using LoE_Launcher.Core.Models.Paths.Json;
using System.Globalization;
using ICSharpCode.SharpZipLib.GZip;
using LoE_Launcher.Core.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using zsyncnet.Sync;

namespace LoE_Launcher.Core;

public partial class Downloader
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly int MaxFileCheckRetries = 10;
    private static readonly int InitialFileCheckDelayMs = 100;
    private static readonly Version MaxVersionSupported = new Version(0, 2);

    private readonly IRelativeFilePath _settingsFile = "settings.json".ToRelativeFilePathAuto();
    private readonly IRelativeDirectoryPath _gameInstallationFolder = ".\\game".ToRelativeDirectoryPathAuto();
    private readonly IAbsoluteDirectoryPath _launcherPath =
        Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location).ToAbsoluteDirectoryPathAuto();
    private readonly Settings _settings;

    private string _versionDownload = "";
    private GameState _state = GameState.Unknown;

    public DownloadData _data = null;
    public ProgressData Progress { get; private set; }
    public long BytesDownloaded { get; private set; }
    public static OS OperatingSystem => PlatformUtils.OperatingSystem;
    public IAbsoluteDirectoryPath GameInstallFolder => _gameInstallationFolder.GetAbsolutePathFrom(_launcherPath);
    public IAbsoluteDirectoryPath LauncherFolder => _launcherPath;
    public IAbsoluteFilePath SettingsFile => _settingsFile.GetAbsolutePathFrom(_launcherPath);
    public GameState State => _state;
    private string ZsyncLocation => _settings.FormatZsyncLocation(_versionDownload);

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
    }

    public async Task RefreshState()
    {
        try
        {
            Progress = new RefreshProgress(this) { Marquee = true };
            using (new Processing(Progress))
            {
                await GetVersion();

                var url = new Uri(ZsyncLocation + ".zsync-control.jar");
                var data = await DownloadMainControlFile(url);

                if (data == null)
                {
                    return;
                }

                _data = data;

                if (_data.ControlFile.RootUri == null)
                {
                    _data.ControlFile.RootUri = new Uri(ZsyncLocation);
                }

                if (_data.ToProcess.Count == 0)
                {
                    _state = GameState.UpToDate;
                    return;
                }

                if (!GameInstallFolder.Exists)
                {
                    _state = GameState.NotFound;
                    return;
                }

                _state = GameState.UpdateAvailable;
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "RefreshState failed");
            _state = GameState.Unknown;
        }
    }

    private async Task GetVersion()
    {
        var data = await DownloadJson<VersionsControlFile>(new Uri(_settings.Stream));

        switch (OperatingSystem)
        {
            case OS.WindowsX86:
                _versionDownload = data.Win32;
                break;
            case OS.WindowsX64:
                _versionDownload = data.Win64;
                break;
            case OS.MacArm:
            case OS.MacIntel:
                _versionDownload = data.Mac;
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
    }

    public async Task DoInstallation()
    {
        try
        {
            await PrepareUpdate();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "DoInstallation->PrepareUpdate failed");

            await Cleanup();
            await RefreshState();
            // MessageBox.Show("The Launcher ran into a critical error while trying to patch your game. Please try again later.\n\nException: " + ex.ToString());
            return;
        }

        await InstallUpdate();
        await Cleanup();
        await RefreshState();
    }

    public async Task PrepareUpdate()
    {
        Progress = new PreparingProgress(this) { Marquee = true };

        using (new Processing(Progress))
        {
            var tasks = new List<Task>(_data.ToProcess.Count);
            Progress.ResetCounter(_data.ToProcess.Count, true);

            foreach (var controlFileItem in _data.ToProcess)
            {
                tasks.Add(Task.Run(() => {
                    CompressOriginalFile(controlFileItem.GetUnzippedFileName().GetAbsolutePathFrom(GameInstallFolder));
                    Progress.Count();
                }));
            }

            await Task.WhenAll(tasks);
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
        BytesDownloaded = 0;

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
            var lastUnzip = Task.CompletedTask; // Use CompletedTask instead of FromResult(0)

            while (queue.Count != 0)
            {
                try
                {
                    var item = queue.Dequeue();
                    var bytesRead = await ProcessQueueItem(item, installProgress, lastUnzip);

                    if (bytesRead == 0)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(item.InstallPath.Path);
                        Logger.Warn($"Adding {fileName} to reProcess queue (download failed)");
                        reProcess.Enqueue(item);
                    }
                    else
                    {
                        BytesDownloaded += bytesRead;
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

        try
        {
            var testItem = queue.Peek();
            var testUri = testItem.GetContentUri(_data.ControlFile);
            Logger.Info($"Testing connection to: {testUri}");

            var response = await httpClient.GetAsync(testUri, HttpCompletionOption.ResponseHeadersRead);
            Logger.Info($"Test connection successful: {response.StatusCode}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Initial connection test failed");
            _state = GameState.Offline;
            return false;
        }
    }

    private async Task<long> ProcessQueueItem(ControlFileItem item, InstallingProgress installProgress, Task lastUnzip)
    {
        // Wait for any previous unzip operation to complete first
        await lastUnzip;

        var zsyncUri = item.GetContentUri(_data.ControlFile);
        var objUri = new Uri(zsyncUri.ToString().Substring(0, zsyncUri.ToString().Length - ControlFileItem.ZsyncExtension.Length));

        var zsyncFilePath = item.InstallPath.GetAbsolutePathFrom(GameInstallFolder).Path;
        var objFilePath = zsyncFilePath.Substring(0, zsyncFilePath.Length - ControlFileItem.ZsyncExtension.Length);

        var fileName = new CustomFilePath(objFilePath).FileNameWithoutExtension;

        EnsureDirectoryExists(objFilePath);

        long bytesRead = await DownloadFile(zsyncUri, objFilePath, objUri, fileName, installProgress);

        if (bytesRead > 0)
        {
            var fileToUnzip = item.InstallPath.GetAbsolutePathFrom(GameInstallFolder)
                .GetBrotherFileWithName(
                    item.InstallPath.FileNameWithoutExtension.ToRelativeFilePathAuto()
                        .FileNameWithoutExtension);

            lastUnzip = UnzipFile(fileToUnzip)
                .ContinueWith(t => {
                    if (t.IsFaulted)
                    {
                        Logger.Error(t.Exception, $"Unzip operation failed for {fileToUnzip.Path}");
                    }
                }, TaskContinuationOptions.ExecuteSynchronously);
        }

        return bytesRead;
    }

    private void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private async Task<long> DownloadFile(Uri zsyncUri, string objFilePath, Uri objUri, string fileName, InstallingProgress installProgress)
    {
        long bytesRead;

        try
        {
            bytesRead = await SyncFileWithZsync(zsyncUri, objFilePath, objUri, fileName, installProgress);

            if (bytesRead == 0)
            {
                Logger.Info($"ZSync returned 0 bytes, trying direct download for: {fileName}");
                bytesRead = await DirectDownloadFile(objUri, objFilePath);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, $"File download failed for {fileName}");
            bytesRead = 0;

            DeleteFileIfExists(objFilePath);

            try
            {
                Logger.Info($"Trying direct download fallback for: {fileName}");
                bytesRead = await DirectDownloadFile(objUri, objFilePath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Direct download fallback failed for {fileName}");
                bytesRead = 0;
            }
        }

        return bytesRead;
    }

    private void DeleteFileIfExists(string filePath)
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

    private static async Task<long> SyncFileWithZsync(Uri zsyncUri, string objFilePath, Uri objUri, string fileName, InstallingProgress installProgress)
    {
        Logger.Info($"Attempting zsync for: {fileName}");

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LoE-Launcher/1.0");
        client.Timeout = TimeSpan.FromMinutes(5);

        var downloader = new RangeDownloader(objUri, client);

        try
        {
            var controlFile = DownloadControlFile(zsyncUri);
            var outputDir = new DirectoryInfo(Path.GetDirectoryName(objFilePath));

            zsyncnet.Zsync.Sync(controlFile, downloader, outputDir, (ss) => {
                var flavor = ss switch
                {
                    zsyncnet.SyncState.CalcDiff => $"{fileName} diff",
                    zsyncnet.SyncState.CopyExisting => $"{fileName} copying parts",
                    zsyncnet.SyncState.DownloadPatch => $"{fileName} downloading patch",
                    zsyncnet.SyncState.DownloadNew => $"{fileName} downloading",
                    zsyncnet.SyncState.PatchFile => $"{fileName} patching",
                    _ => ""
                };
                installProgress.FlavorText = flavor;
                Logger.Debug($"ZSync state: {ss} - {flavor}");
            });

            var fileInfo = new FileInfo(objFilePath);
            if (fileInfo.Exists)
            {
                return fileInfo.Length;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error during zsync: {ex.Message}");
            throw;
        }
    }

    private static async Task<long> DirectDownloadFile(Uri fileUri, string filePath)
    {
        Logger.Info($"Attempting direct download: {fileUri} -> {filePath}");
        var tempFilePath = filePath + ".tmp";

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LoE-Launcher/1.0");

            using var response = await client.GetAsync(fileUri, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            // Download to a temporary file first to avoid file locking issues
            await using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalBytesRead += bytesRead;
            }

            // Close the filestream before attempting to move the file
            await fileStream.FlushAsync();
            await fileStream.DisposeAsync();

            // Delete the existing file if it exists before moving
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            // Now safely move the temp file to the actual destination
            File.Move(tempFilePath, filePath);

            Logger.Info($"Direct download complete: {totalBytesRead} bytes");
            return totalBytesRead;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Direct download failed: {ex.Message}");

            // Clean up both the temp file and target file if they exist
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // Ignore deletion failures
                }
            }

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
            throw;
        }
    }

    private static zsyncnet.ControlFile DownloadControlFile(Uri uri)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LoE-Launcher/1.0");

        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        var response = client.Send(request);
        response.EnsureSuccessStatusCode();

        using var stream = response.Content.ReadAsStream();
        return new zsyncnet.ControlFile(stream);
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

    private static async Task UnzipFile(IAbsoluteFilePath file)
    {
        var outputFile = file.GetBrotherFileWithName(file.FileNameWithoutExtension).Path;

        if (!await FileIsReadyAsync(file.Path, MaxFileCheckRetries, InitialFileCheckDelayMs))
        {
            Logger.Error($"File not ready for unzipping after {MaxFileCheckRetries} attempts: {file.Path}");
            throw new IOException($"File locked: {file.Path}");
        }

        try
        {
            await using (var inputStream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            await using (var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await Task.Run(() => {
                    GZip.Decompress(inputStream, outputStream, false);
                });
            }
            Logger.Info($"Successfully unzipped: {file.Path}");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error unzipping file {file.Path}");
            throw;
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

    private async Task<DownloadData> DownloadMainControlFile(Uri url)
    {
        var mainControlFile = await DownloadJson<MainControlFile>(url);
        if (mainControlFile == null)
        {
            return null;
        }

        var data = new DownloadData(mainControlFile);
        var systemVersion = data.ControlFile.Version.ToSystemVersion();

        if (systemVersion.CompareTo(MaxVersionSupported) < 0 ||
            systemVersion.CompareTo(MaxVersionSupported) > 0)
        {
            _state = GameState.LauncherOutOfDate;
            return null;
        }

        Progress.ResetCounter(data.ControlFile.Content.Count, true);

        foreach (var item in data.ControlFile.Content)
        {
            try
            {
                var realFile = item.GetUnzippedFileName().GetAbsolutePathFrom(GameInstallFolder);

                if (realFile.Exists &&
                    realFile.ToString().GetFileHash(HashType.MD5) == item.FileHash)
                {
                    continue;
                }

                data.ToProcess.Add(item);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Issue with control file item {item.InstallPath}");
                throw;
            }
            finally
            {
                Progress.Count();
            }
        }
        return data;
    }

    private async Task<TType> DownloadJson<TType>(Uri url)
        where TType : class
    {
        string result;
        try
        {
            using (var client = new HttpClient())
            {
                result = await client.GetStringAsync(url);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Could not download json @ {url}");
            _state = GameState.Offline;
            return null;
        }
        return JsonConvert.DeserializeObject<TType>(result, Settings);
    }

    private void CompressOriginalFile(IAbsoluteFilePath realFile)
    {
        if (realFile.Exists)
        {
            var compressedFile = realFile.GetBrotherFileWithName(realFile.FileName + ".jar");
            using (var inputStream = new FileStream(realFile.Path, FileMode.Open))
            using (var outputStream = new FileStream(compressedFile.Path, FileMode.Create))
            {
                GZip.Compress(inputStream, outputStream, false);
            }
        }
    }

    public class DownloadData(MainControlFile controlFile)
    {
        public MainControlFile ControlFile { get; private set; } = controlFile;
        public List<ControlFileItem> ToProcess { get; set; } = new();

    }
}

public enum GameState
{
    Unknown,
    NotFound,
    UpdateAvailable,
    UpToDate,
    Offline,
    LauncherOutOfDate
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