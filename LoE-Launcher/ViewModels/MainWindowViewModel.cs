using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoE_Launcher.Core;
using LoE_Launcher.Models;
using LoE_Launcher.Services;
using LoE_Launcher.Utils;
using NLog;

namespace LoE_Launcher.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Downloader _downloader;
    private readonly IDialogService _dialogService;
    private readonly CacheManager _cacheManager;
    private readonly ChangelogParser _changelogParser;
    private readonly GameImportFlow _importFlow;
    private readonly GameLocationFlow _locationFlow;

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _downloadStopwatch = new();

    [ObservableProperty] private string _actionButtonText = "Validating...";
    [ObservableProperty] private IBrush _actionButtonBackground;
    [ObservableProperty] private bool _isActionButtonEnabled;
    [ObservableProperty] private bool _isProgressOverlayVisible = true;
    [ObservableProperty] private bool _isProgressBarIndeterminate = true;
    [ObservableProperty] private double _progressBarValue;
    [ObservableProperty] private string _progressPercentageText = "0%";
    [ObservableProperty] private string _downloadSpeedText = "";
    [ObservableProperty] private string _downloadStatusText = "Validating";
    [ObservableProperty] private Bitmap? _backgroundImageSource;
    [ObservableProperty] private Bitmap? _logoImageSource;
    [ObservableProperty] private bool _isDevChannel;
    [ObservableProperty] private string _channelInfoText = "";
    [ObservableProperty] private bool _isOptionsOpen;

    public SettingsViewModel Options { get; }

    public ObservableCollection<ChangelogLine> ChangelogLines { get; } = [];

    private readonly IBrush _downloadColor = BrushFactory.CreateHorizontalGradient("#9C69B5", "#D686D2");
    private readonly IBrush _updateColor = BrushFactory.CreateHorizontalGradient("#D69D45", "#E8B75C");
    private readonly IBrush _launchColor = BrushFactory.CreateHorizontalGradient("#9C69B5", "#D686D2");
    private readonly IBrush _errorColor = BrushFactory.CreateHorizontalGradient("#D32F2F", "#F44336");

    public MainWindowViewModel(Downloader downloader, IDialogService dialogService, CacheManager cacheManager,
        ChangelogParser changelogParser, GameImportFlow importFlow, GameLocationFlow locationFlow,
        SettingsViewModel options)
    {
        Logger.Info("Starting LoE Launcher ViewModel");

        _downloader = downloader;
        _dialogService = dialogService;
        _cacheManager = cacheManager;
        _changelogParser = changelogParser;
        _importFlow = importFlow;
        _locationFlow = locationFlow;
        _downloader.StateRefreshed += (_, _) => Dispatcher.UIThread.Post(() => _ = OfferOldCopyDeletion());

        Options = options;
        Options.CloseAction = () => IsOptionsOpen = false;

        _actionButtonBackground = _downloadColor;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += OnTimerTick;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        _timer.Start();
        _downloadStopwatch.Start();

        await LoadInitialImagesAsync();

        _ = UpdateBackgroundImageAsync();
        _ = LoadChangelogAsync();

        await InitializeDownloader();
    }

    private async Task LoadInitialImagesAsync()
    {
        try
        {
            LogoImageSource =
                new Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri("avares://LoE-Launcher/Assets/Logo.png")));

            var cachedBg = await _cacheManager.LoadCachedImageImmediately(Constants.BackgroundImageFileName);
            BackgroundImageSource = cachedBg ??
                                    new Bitmap(Avalonia.Platform.AssetLoader.Open(
                                        new Uri("avares://LoE-Launcher/Assets/Default-Background.png")));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load initial images.");
            BackgroundImageSource ??=
                new Bitmap(Avalonia.Platform.AssetLoader.Open(
                    new Uri("avares://LoE-Launcher/Assets/Default-Background.png")));
        }
    }

    private async Task UpdateBackgroundImageAsync()
    {
        try
        {
            var updatedImage =
                await _cacheManager.UpdateCachedImage(Constants.BackgroundImageUrl, Constants.BackgroundImageFileName);
            if (updatedImage != null)
            {
                BackgroundImageSource = updatedImage;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Unable to update remote background image");
        }
    }

    private async Task LoadChangelogAsync()
    {
        ChangelogLines.Add(new ChangelogLine(ChangelogLineType.Bullet, "Loading..."));

        try
        {
            var cachedChangelog = await _cacheManager.LoadCachedTextImmediately(Constants.ChangelogFileName);
            if (!string.IsNullOrEmpty(cachedChangelog))
            {
                var parsedLines = _changelogParser.Parse(cachedChangelog);
                ChangelogLines.Clear();
                foreach (var line in parsedLines)
                {
                    ChangelogLines.Add(line);
                }
            }

            _ = Task.Run(async () =>
            {
                var updatedChangelog =
                    await _cacheManager.UpdateCachedText(Constants.ChangelogUrl, Constants.ChangelogFileName);
                if (!string.IsNullOrEmpty(updatedChangelog))
                {
                    var parsedLines = _changelogParser.Parse(updatedChangelog);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ChangelogLines.Clear();
                        foreach (var line in parsedLines)
                        {
                            ChangelogLines.Add(line);
                        }
                    });
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Unable to load cached changelog");
        }
    }

    private async Task InitializeDownloader()
    {
        try
        {
            Logger.Info("Initializing downloader");

            await Task.Run(async () =>
            {
                await _downloader.Cleanup();
                await _downloader.RefreshState();
            });

            Logger.Info($"Initialization complete. Game state: {_downloader.State}");

            if (_downloader.State == GameState.Offline)
            {
                await _dialogService.ShowErrorMessage("Connection Error",
                    "Cannot connect to the game servers. Check your internet connection and try again.");
            }
            else if (_downloader.State == GameState.ServerMaintenance)
            {
                await _dialogService.ShowErrorMessage("Server Maintenance",
                    "The game servers are currently under maintenance or temporarily unavailable. Please try again in a few hours.");
            }
            else if (_downloader.State == GameState.PlatformUnsupported)
            {
                await _dialogService.ShowErrorMessage("System Not Supported",
                    "Legends of Equestria needs 64-bit Windows, macOS or 64-bit Linux.");
            }
            else if (_downloader.State == GameState.DevAccessDenied)
            {
                await _dialogService.ShowErrorMessage("Tester Login Expired",
                    "Your tester login expired or was revoked. Log in again in Options, or switch back to live builds.");
            }

            else if (_downloader.State == GameState.FolderMissing)
            {
                UpdateState();
                await HandleMissingFolder();
            }

            if (_downloader.State == GameState.NotFound)
            {
                UpdateState();
                await _importFlow.OfferFoundInstall();
            }

            _downloadStopwatch.Stop();
            Logger.Info($"Initialization took {_downloadStopwatch.Elapsed.TotalSeconds:F1} seconds");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize downloader");
            await _dialogService.ShowErrorMessage("Initialization Error", ex.Message);
        }
        finally
        {
            UpdateState();
        }
    }

    private bool _missingFolderDialogOpen;

    private async Task HandleMissingFolder()
    {
        if (_missingFolderDialogOpen)
        {
            return;
        }

        _missingFolderDialogOpen = true;
        try
        {
            while (_downloader.State == GameState.FolderMissing)
            {
                var path = _downloader.LauncherSettings.ActiveCustomGameFolder;
                var choice = await _dialogService.ShowChoiceDialog("Game Folder Not Found",
                    $"Game folder not found:\n{path}\n\nIs the drive connected?",
                    "Try again", "Choose folder…", "Use default location");
                switch (choice)
                {
                    case 0:
                        await Task.Run(_downloader.RefreshState);
                        break;
                    case 1:
                        await _locationFlow.ChooseFolder();
                        break;
                    case 2:
                        _downloader.LauncherSettings.ActiveCustomGameFolder = null;
                        _downloader.SaveSettings();
                        await Task.Run(_downloader.RefreshState);
                        return;
                    default:
                        return;
                }

                UpdateState();
            }
        }
        finally
        {
            _missingFolderDialogOpen = false;
            UpdateState();
        }
    }

    private bool _oldCopyDialogOpen;

    private async Task OfferOldCopyDeletion()
    {
        if (_oldCopyDialogOpen || _downloader.LauncherSettings.PendingOldCopy == null
                               || _downloader.State != GameState.UpToDate || _downloader.CurrentFileList == null
                               || _downloader.Progress.Processing)
        {
            return;
        }

        _oldCopyDialogOpen = true;
        try
        {
            await _locationFlow.OfferOldCopyDeletion();
        }
        finally
        {
            _oldCopyDialogOpen = false;
        }
    }

    private void OnTimerTick(object? sender, EventArgs e) => UpdateState();

    private void UpdateState()
    {
        var progress = _downloader.Progress;
        IsProgressBarIndeterminate = progress.Marquee;
        ProgressBarValue = progress.Max > 0 ? (double)progress.Current / progress.Max * 100 : 0;

        DownloadStatusText = _downloader.State == GameState.Unknown ? "Validating" : progress.Text;

        bool enabledState;
        switch (_downloader.State)
        {
            case GameState.Unknown:
                ActionButtonText = "Validating...";
                ActionButtonBackground = _downloadColor;
                enabledState = false;
                break;
            case GameState.NotFound:
                ActionButtonText = "Install";
                ActionButtonBackground = _downloadColor;
                enabledState = true;
                break;
            case GameState.UpdateAvailable:
                ActionButtonText = "Update";
                ActionButtonBackground = _updateColor;
                enabledState = true;
                break;
            case GameState.UpToDate:
                ActionButtonText = "Launch";
                ActionButtonBackground = _launchColor;
                enabledState = true;
                break;
            case GameState.Offline:
                ActionButtonText = "Offline";
                ActionButtonBackground = _errorColor;
                enabledState = false;
                break;
            case GameState.ServerMaintenance:
                ActionButtonText = "Maintenance";
                ActionButtonBackground = _updateColor;
                enabledState = false;
                break;
            case GameState.LauncherOutOfDate:
                ActionButtonText = "Update Required";
                ActionButtonBackground = _errorColor;
                enabledState = true;
                break;
            case GameState.DevAccessDenied:
                ActionButtonText = "Access Denied";
                ActionButtonBackground = _errorColor;
                enabledState = false;
                break;
            case GameState.DevBuildUnavailable:
                ActionButtonText = "No Dev Build";
                ActionButtonBackground = _errorColor;
                enabledState = false;
                break;
            case GameState.PlatformUnsupported:
                ActionButtonText = "Not Supported";
                ActionButtonBackground = _errorColor;
                enabledState = false;
                break;
            case GameState.FolderMissing:
                ActionButtonText = "Folder Missing";
                ActionButtonBackground = _errorColor;
                enabledState = true;
                break;
            default:
                enabledState = false;
                break;
        }

        IsActionButtonEnabled = enabledState && !_downloader.Progress.Processing;

        if (IsOptionsOpen)
        {
            Options.RefreshBusy();
        }

        IsDevChannel = _downloader.IsDevChannel;
        var build = _downloader.CurrentBuild?.ToString();
        ChannelInfoText = string.IsNullOrEmpty(build) ? "DEV BUILD" : $"DEV BUILD · {build}";

        bool showProgress = _downloader.Progress.Processing || _downloader.State == GameState.Unknown;
        IsProgressOverlayVisible = showProgress;

        if (showProgress)
        {
            ProgressPercentageText = $"{Math.Round(ProgressBarValue)}%";
            DownloadSpeedText = _downloader.DownloadStats.HasValidSpeed ? _downloader.DownloadStats.CurrentSpeed : "";
        }
    }

    [RelayCommand]
    private async Task ActionButtonClicked()
    {
        IsActionButtonEnabled = false;

        try
        {
            switch (_downloader.State)
            {
                case GameState.NotFound:
                case GameState.UpdateAvailable:
                    await InstallOrUpdateGameAsync();
                    break;
                case GameState.UpToDate:
                    LaunchGame();
                    break;
                case GameState.LauncherOutOfDate:
                    await _dialogService.ShowLauncherUpdateDialog();
                    break;
                case GameState.FolderMissing:
                    await HandleMissingFolder();
                    break;
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorMessage("Error", ex.Message);
        }
        finally
        {
            UpdateState();
        }
    }

    private async Task InstallOrUpdateGameAsync()
    {
        _downloadStopwatch.Restart();
        Logger.Info($"Starting game {(_downloader.State == GameState.NotFound ? "installation" : "update")}");

        try
        {
            await Task.Run(() => _downloader.DoInstallation());
            Logger.Info("Installation/update completed successfully");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Installation/update failed");
            await _dialogService.ShowErrorMessage("Installation Error", ex.Message);
        }
        finally
        {
            _downloadStopwatch.Stop();
            Logger.Debug($"Installation/update process took {_downloadStopwatch.Elapsed.TotalSeconds:F1} seconds");
            await _downloader.RefreshState();
            UpdateState();
        }
    }

    private void LaunchGame()
    {
        try
        {
            ProcessLauncher.LaunchGame(_downloader.GameInstallFolder.Path);

            if (_downloader.LauncherSettings.CloseAfterLaunch
                && Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to launch game");
            _dialogService.ShowErrorMessage("Launch Error", ex.Message);
        }
    }

    [RelayCommand]
    private void SettingsButtonClicked()
    {
        Options.Refresh();
        IsOptionsOpen = true;
    }

    [RelayCommand]
    private void CloseOptions() => IsOptionsOpen = false;

    [RelayCommand]
    private static void OpenYoutube() => ProcessLauncher.LaunchUrl(Constants.YouTubeUrl);

    [RelayCommand]
    private static void OpenDiscord() => ProcessLauncher.LaunchUrl(Constants.DiscordUrl);

    [RelayCommand]
    private static void OpenX() => ProcessLauncher.LaunchUrl(Constants.XUrl);

    [RelayCommand]
    private static void OpenFacebook() => ProcessLauncher.LaunchUrl(Constants.FacebookUrl);
}