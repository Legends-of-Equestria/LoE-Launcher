using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LoE_Launcher.Services;
using Models;
using Models.Paths;
using Models.Paths.Json;
using Models.Utils;
using Newtonsoft.Json;
using NLog;
using Velopack;

namespace LoE_Launcher.Core;

public partial class Downloader
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly int OfflineRetryIntervalSeconds = 30;
    private static readonly VersionInfo CurrentLauncherVersion = new() { Major = 1, Minor = 2, Build = 0, Revision = 0 };

    private readonly IRelativeFilePath _settingsFile = "settings.json".ToRelativeFilePathAuto();
    private readonly IRelativeDirectoryPath _gameInstallationFolder = ".\\Game".ToRelativeDirectoryPathAuto();
    private readonly IAbsoluteDirectoryPath _launcherPath =
        GetLauncherDirectory().ToAbsoluteDirectoryPathAuto();
    private readonly Settings _settings;

    private readonly FileOperationsService _fileOps;
    private readonly NetworkDownloadService _network;
    private readonly HashCacheService _hashCache;
    private readonly FileUpdateService _fileUpdate;
    private readonly UpdateManager? _updateManager;

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
    public Settings LauncherSettings => _settings;
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

    public Downloader()
    {
        Progress = new ProgressData(this);

        _fileOps = new FileOperationsService(_launcherPath.Path);
        _hashCache = new HashCacheService(CacheDirectory);
        _network = new NetworkDownloadService(_fileOps);
        _fileUpdate = new FileUpdateService(_fileOps, _network, _hashCache);

        _updateManager = VelopackHelper.CreateUpdateManager();

        var settingsFile = SettingsFile;
        _settings = settingsFile.Exists
            ? JsonConvert.DeserializeObject<Settings>(File.ReadAllText(settingsFile.Path), NetworkDownloadService.JsonSettings)
            : new Settings();

        Task.Run(_fileOps.HandlePendingDeletions);
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

                    if (string.IsNullOrEmpty(_versionDownload) || _versionDownload == "default")
                    {
                        Logger.Warn("No valid version path available, marking as offline but allowing local gameplay");

                        if (GameInstallFolder.Exists && File.Exists(Path.Combine(GameInstallFolder.Path, "loe.exe")))
                        {
                            Logger.Info("Found existing game installation, allowing launch in offline mode");
                            _state = GameState.UpToDate;
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

                    var data = await _fileUpdate.DownloadMainControlFile(url, Progress, GameInstallFolder, state => _state = state);
                    if (data == null)
                    {
                        Logger.Warn("Control file data is null, likely offline or server issue");
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
                    throw;
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
#if !FLATPAK
        if (_updateManager != null)
        {
            Logger.Info("Checking for Velopack launcher updates...");
            try
            {
                var updateInfo = await _updateManager.CheckForUpdatesAsync();
                if (updateInfo != null)
                {
                    Logger.Info($"Velopack update available: {updateInfo.TargetFullRelease.Version}");
                    _state = GameState.LauncherOutOfDate;
                    return;
                }
                else
                {
                    Logger.Info("No Velopack launcher updates available.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to check for Velopack launcher updates.");
            }
        }
#endif

        Logger.Info($"Checking launcher version compatibility from {_settings.LauncherVersionUrl}");

        try
        {
            var launcherVersionInfo = await _network.DownloadJson<LauncherVersionInfo>(new Uri(_settings.LauncherVersionUrl), state => _state = state);

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
        }
    }

    private async Task GetVersion()
    {
        Logger.Info($"Getting version info from {_settings.Stream}");
        var data = await _network.DownloadJson<VersionsControlFile>(new Uri(_settings.Stream), state => _state = state);

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

            Logger.Info("Starting compression phase");
            Progress = new PreparingProgress(this) { Marquee = true };
            using (new Processing(Progress))
            {
                await _fileUpdate.PrepareFilesForUpdate(_data.ToProcess, Progress, GameInstallFolder);
            }

            Logger.Info("Starting installation phase");
            Progress = new InstallingProgress(this) { Marquee = true };
            using (new Processing(Progress))
            {
                await _fileUpdate.UpdateFiles(_data, Progress, GameInstallFolder, OnDownloadProgress, state => _state = state, 3);
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
            await _fileUpdate.PrepareFilesForUpdate(_data.ToProcess, Progress, GameInstallFolder);
        }
    }

    public async Task InstallUpdate()
    {
        Progress = new InstallingProgress(this) { Marquee = true };

        using (new Processing(Progress))
        {
            await _fileUpdate.UpdateFiles(_data, Progress, GameInstallFolder, OnDownloadProgress, state => _state = state, 3);
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
                    _fileOps.CleanGameFolder(GameInstallFolder);
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
                _bytesDownloaded = 0;

                _hashCache.ClearHashCache();

                _state = GameState.Unknown;

                await GetVersion();

                if (_state == GameState.Offline)
                {
                    Logger.Warn("Cannot repair in offline mode");
                    throw new InvalidOperationException("Cannot repair game files while offline. Please check your internet connection.");
                }

                var url = new Uri($"{ZsyncLocation}.zsync-control.jar");
                Logger.Info($"Downloading control file from: {url}");

                var data = await _fileUpdate.DownloadMainControlFile(url, Progress, GameInstallFolder, state => _state = state);
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

        var testUrl = new Uri(_settings.Stream);
        if (await _network.CheckConnectivity(testUrl))
        {
            Logger.Info("Connectivity restored, refreshing state");
            await RefreshState();
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
