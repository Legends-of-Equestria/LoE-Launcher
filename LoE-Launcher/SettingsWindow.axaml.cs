using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using LoE_Launcher.Core;
using LoE_Launcher.Services;
using LoE_Launcher.Utils;
using NLog;

namespace LoE_Launcher;

public partial class SettingsWindow : Window
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Downloader _downloader;
    private readonly DialogService _dialogService;
    private readonly ProgressBar _progressBar;
    private readonly IBrush _launchColor;
    private readonly Stopwatch _downloadStopwatch;

    private CheckBox _closeAfterLaunchCheckBox = null!;

    public SettingsWindow(Downloader downloader, DialogService dialogService, ProgressBar progressBar, IBrush launchColor, Stopwatch downloadStopwatch)
    {
        _downloader = downloader;
        _dialogService = dialogService;
        _progressBar = progressBar;
        _launchColor = launchColor;
        _downloadStopwatch = downloadStopwatch;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _closeAfterLaunchCheckBox = this.FindControl<CheckBox>("CloseAfterLaunchCheckBox")!;
        _closeAfterLaunchCheckBox.IsChecked = _downloader.LauncherSettings.CloseAfterLaunch;
        _closeAfterLaunchCheckBox.IsCheckedChanged += OnCloseAfterLaunchChanged;
    }

    private void OnCloseAfterLaunchChanged(object? sender, RoutedEventArgs e)
    {
        _downloader.LauncherSettings.CloseAfterLaunch = _closeAfterLaunchCheckBox.IsChecked ?? true;
        _downloader.SaveSettings();
    }

    private async void OnRepairGameClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            this.Close();
            var confirmResult = await _dialogService.ShowConfirmDialog(
                "Repair Game",
                "This will verify all game files and re-download any corrupted or missing files. Continue?");

            if (!confirmResult)
            {
                return;
            }

            _downloadStopwatch.Restart();
            Logger.Info("Starting game repair process");

            await Task.Run(() => _downloader.RepairGame());

            Logger.Info("Game repair completed successfully");

            var originalBrush = _progressBar.Foreground;
            _progressBar.Foreground = _launchColor;
            await Task.Delay(1000);
            _progressBar.Foreground = originalBrush;

            await _dialogService.ShowInfoMessage("Repair Complete", "Game files have been verified and repaired.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Game repair failed");
            await _dialogService.ShowErrorMessage("Repair Error", ex.Message);
        }
        finally
        {
            _downloadStopwatch.Stop();
        }
    }

    private async void OnDeleteGameClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            this.Close();

            var confirmResult = await _dialogService.ShowConfirmDialog(
                "Delete Game Files",
                "This will permanently delete all downloaded game files. You will need to download the game again to play. Continue?");

            if (!confirmResult)
            {
                return;
            }

            Logger.Info("Starting game files deletion");

            if (_downloader.GameInstallFolder.Exists)
            {
                try
                {
                    Directory.Delete(_downloader.GameInstallFolder.Path, true);
                    Logger.Info($"Deleted game directory: {_downloader.GameInstallFolder.Path}");
                }
                catch (Exception deleteEx)
                {
                    Logger.Error(deleteEx, "Failed to delete game directory");
                    throw new Exception($"Failed to delete game files: {deleteEx.Message}");
                }
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

    private void OnOpenLogFolderClicked(object? sender, RoutedEventArgs e)
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

    private async void OnOpenGameLogsClicked(object? sender, RoutedEventArgs e)
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
