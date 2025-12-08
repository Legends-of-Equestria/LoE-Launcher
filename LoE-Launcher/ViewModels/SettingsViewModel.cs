using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoE_Launcher.Core;
using LoE_Launcher.Services;
using LoE_Launcher.Utils;
using NLog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace LoE_Launcher.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Downloader _downloader;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private bool _closeAfterLaunch;

    public Action? CloseAction { get; set; }

    public SettingsViewModel(Downloader downloader, IDialogService dialogService)
    {
        _downloader = downloader;
        _dialogService = dialogService;
        _closeAfterLaunch = _downloader.LauncherSettings.CloseAfterLaunch;
    }

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
            if (_downloader.GameInstallFolder.Exists)
            {
                Directory.Delete(_downloader.GameInstallFolder.Path, true);
                Logger.Info($"Deleted game directory: {_downloader.GameInstallFolder.Path}");
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
