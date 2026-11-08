using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoE_Launcher.Core;
using LoE_Launcher.Services;
using LoE_Launcher.Utils;
using Avalonia.Threading;
using Models;
using Models.Utils;
using NLog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace LoE_Launcher.ViewModels;

public enum OptionsPage { Game, Launcher, Help, About }

public partial class SettingsViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Downloader _downloader;
    private readonly IDialogService _dialogService;
    private readonly GameLocationFlow _locationFlow;

    [ObservableProperty]
    private bool _closeAfterLaunch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGamePage), nameof(IsLauncherPage), nameof(IsHelpPage), nameof(IsAboutPage))]
    private OptionsPage _selectedPage = OptionsPage.Game;

    [ObservableProperty] private bool _isTesterLoginOpen;

    [ObservableProperty] private string _gameFolderPath = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LocationNote), nameof(HasLocationNote))]
    private string _gameSizeText = "";
    [ObservableProperty] private string _launcherVersionText = "";
    [ObservableProperty] private string _gameVersionText = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRepair), nameof(CanDelete), nameof(CanMove))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRepair), nameof(CanDelete))]
    private bool _isFolderMissing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDelete), nameof(CanMove), nameof(ShowFileListHint))]
    private bool _knowsGameFiles = true;

    public bool CanRepair => !IsBusy && !IsFolderMissing;
    public bool CanDelete => !IsBusy && KnowsGameFiles && !IsFolderMissing;
    public bool CanMove => !IsBusy && KnowsGameFiles;
    public bool ShowFileListHint => !KnowsGameFiles;

    public string LocationNote
    {
        get
        {
            var size = string.IsNullOrEmpty(GameSizeText) ? "" : $"{GameSizeText} on disk.";
            var change = CanChangeLocation ? "Changing it moves your installed game to the new folder." : "";
            return $"{size} {change}".Trim();
        }
    }

    public bool HasLocationNote => !string.IsNullOrEmpty(LocationNote);

    public bool CanChangeLocation => !PlatformUtils.IsFlatpak;
    public bool IsCustomLocation => _downloader.LauncherSettings.ActiveCustomGameFolder != null;
    public bool ShowResetLocation => CanChangeLocation && IsCustomLocation;

    public bool IsGamePage => SelectedPage == OptionsPage.Game;
    public bool IsLauncherPage => SelectedPage == OptionsPage.Launcher;
    public bool IsHelpPage => SelectedPage == OptionsPage.Help;
    public bool IsAboutPage => SelectedPage == OptionsPage.About;

    public Action? CloseAction { get; set; }

    public event EventHandler? Closed;

    public TesterAccessViewModel Tester { get; }

    public SettingsViewModel(Downloader downloader, IDialogService dialogService, GameLocationFlow locationFlow,
        TesterAccessViewModel tester)
    {
        _downloader = downloader;
        _dialogService = dialogService;
        _locationFlow = locationFlow;
        Tester = tester;
        Tester.ChangesApplied = OnTesterChangesApplied;
        _closeAfterLaunch = _downloader.LauncherSettings.CloseAfterLaunch;
        _downloader.StateRefreshed += (_, _) => Dispatcher.UIThread.Post(OnStateRefreshed);
    }

    public void Refresh()
    {
        CloseAfterLaunch = _downloader.LauncherSettings.CloseAfterLaunch;
        var folder = _downloader.GameInstallFolder.Path;
        GameFolderPath = folder;
        OnPropertyChanged(nameof(IsCustomLocation));
        OnPropertyChanged(nameof(ShowResetLocation));
        LauncherVersionText = FormatVersion(Downloader.CurrentLauncherVersion);
        GameVersionText = FormatGameVersion();
        Tester.Refresh();
        RefreshBusy();
        _ = RefreshGameSizeAsync();
    }

    private void OnStateRefreshed()
    {
        RefreshBusy();
        if (!IsTesterLoginOpen)
        {
            Tester.Refresh();
        }
    }

    public void NotifyClosed()
    {
        IsTesterLoginOpen = false;
        Tester.Password = "";
        Closed?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshBusy()
    {
        IsBusy = _downloader.Progress.Processing;
        IsFolderMissing = _downloader.State == GameState.FolderMissing;
        KnowsGameFiles = ComputeKnowsGameFiles(_downloader.GameInstallFolder.Path);
    }

    private async Task RefreshGameSizeAsync()
    {
        if (string.IsNullOrEmpty(GameSizeText))
        {
            GameSizeText = "…";
        }

        try
        {
            var folder = _downloader.GameInstallFolder.Path;
            var hasFiles = await Task.Run(() => Directory.Exists(folder) && _downloader.ListedGameFiles(folder)?.Count > 0);
            GameSizeText = hasFiles ? SizeText.Format(await Task.Run(() => _downloader.TotalGameSize)) : "";
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Couldn't work out the game size");
            GameSizeText = "";
        }
    }

    private bool ComputeKnowsGameFiles(string folder) =>
        _downloader.ListedGameFiles(folder) != null || !Directory.Exists(folder);

    private string FormatGameVersion()
    {
        var channel = _downloader.IsDevChannel ? "Dev" : "Live";
        var build = _downloader.CurrentBuild;
        var buildText = string.IsNullOrEmpty(build?.Id) ? build?.ToString() : build.Id;

        if (!string.IsNullOrEmpty(buildText))
        {
            return $"{buildText} · {channel}";
        }

        return _downloader.State == GameState.NotFound ? "Not installed" : channel;
    }

    private static string FormatVersion(VersionInfo version) =>
        $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";

    [RelayCommand]
    private void SelectPage(OptionsPage page)
    {
        IsTesterLoginOpen = false;
        SelectedPage = page;
    }

    [RelayCommand]
    private void Close() => CloseAction?.Invoke();

    [RelayCommand]
    private void OpenTesterLogin() => IsTesterLoginOpen = true;

    [RelayCommand]
    private void CloseTesterLogin()
    {
        Tester.Password = "";
        IsTesterLoginOpen = false;
    }

    [RelayCommand]
    private async Task SwitchToLiveBuilds()
    {
        await Tester.RemoveTesterAccessCommand.ExecuteAsync(null);
        if (!Tester.HasTesterAccess)
        {
            IsTesterLoginOpen = false;
        }
    }

    private void OnTesterChangesApplied()
    {
        Refresh();
        if (Tester.ShowChannel)
        {
            IsTesterLoginOpen = false;
        }
    }

    [RelayCommand]
    private async Task ChangeLocation()
    {
        await _locationFlow.ChooseFolder(CloseAction);
        Refresh();
    }

    [RelayCommand]
    private async Task ResetLocation()
    {
        await _locationFlow.ResetToDefault(CloseAction);
        Refresh();
    }

    [RelayCommand]
    private static void OpenThirdPartyNotices() => ProcessLauncher.LaunchUrl(Constants.ThirdPartyNoticesUrl);

    partial void OnCloseAfterLaunchChanged(bool value)
    {
        if (_downloader.LauncherSettings.CloseAfterLaunch == value) return;
        
        _downloader.LauncherSettings.CloseAfterLaunch = value;
        _downloader.SaveSettings();
    }

    [RelayCommand]
    private async Task RepairGame()
    {
        var confirmResult = await _dialogService.ShowConfirmDialog(
            "Repair Game",
            "This will verify all game files and re-download any corrupted or missing files. Continue?");

        if (!confirmResult)
        {
            return;
        }

        CloseAction?.Invoke();

        try
        {
            Logger.Info("Starting game repair process");
            await Task.Run(() => _downloader.RepairGame());
            Logger.Info("Game repair completed successfully");
            await _dialogService.ShowInfoMessage("Repair Complete", "Game files have been verified and repaired.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Game repair failed");
            await _dialogService.ShowErrorMessage("Repair Error", ex.Message);
        }
    }

    [RelayCommand]
    private async Task DeleteGame()
    {
        var deleteFolder = _downloader.GameInstallFolder.Path;
        var listed = _downloader.ListedGameFiles(deleteFolder);
        if (listed == null && Directory.Exists(deleteFolder))
        {
            return;
        }

        var confirmResult = await _dialogService.ShowConfirmDialog(
            "Delete Game Files",
            "This will permanently delete all downloaded game files. You will need to download the game again to play. Continue?");

        if (!confirmResult)
        {
            return;
        }

        CloseAction?.Invoke();

        try
        {
            Logger.Info("Starting game files deletion");
            if (listed != null)
            {
                var deleted = await Task.Run(() => GameFiles.Delete(deleteFolder, listed));
                Logger.Info($"Deleted {deleted} game files from {deleteFolder}");
            }

            await Task.Run(() => _downloader.RefreshState());
            Logger.Info("Game files deleted and state refreshed");
            await _dialogService.ShowInfoMessage("Delete Complete", "Game files have been successfully deleted.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Game files deletion failed");
            await _dialogService.ShowErrorMessage("Delete Error", ex.Message);
        }
    }

    [RelayCommand]
    private void OpenLauncherLogs()
    {
        try
        {
            var logDir = Path.Combine(Directory.GetCurrentDirectory(), "Launcher Logs");
            ProcessLauncher.OpenFolder(logDir);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to open log directory");
        }
    }

    [RelayCommand]
    private async Task OpenGameLogs()
    {
        try
        {
            var logPath = UnityPlayerLogHelper.GetPlayerLogPath(Constants.GameName, Constants.GameFullName);

            if (UnityPlayerLogHelper.PlayerLogExists(Constants.GameName, Constants.GameFullName))
            {
                ProcessLauncher.OpenFileLocation(logPath);
            }
            else
            {
                await _dialogService.ShowGameLogsNotFoundDialog();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to open game logs directory");
        }
    }
}
