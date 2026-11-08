using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using LoE_Launcher.Core;
using LoE_Launcher.Utils;
using LoE_Launcher.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Velopack;

namespace LoE_Launcher.Services;

public class DialogService : IDialogService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IWindowProvider _windowProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly UpdateManager? _updateManager;

    public DialogService(IWindowProvider windowProvider, IServiceProvider serviceProvider)
    {
        _windowProvider = windowProvider;
        _serviceProvider = serviceProvider;
        _updateManager = VelopackHelper.CreateUpdateManager();
    }

    private Window? GetOwner() => _windowProvider.GetMainWindow();

    private static ControlTheme GlassDialogButton =>
        (ControlTheme)Application.Current!.Resources["GlassDialogButton"]!;

    private static Window CreateDialogWindow(string title, double width = 350) => new Window
    {
        Title = title,
        Width = width,
        SizeToContent = SizeToContent.Height,
        Background = BrushFactory.FromResource("PlumDialogBrush"),
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        CanResize = false,
        FontFamily = new FontFamily("Segoe UI, SF Pro Display, Arial, sans-serif")
    };

    private static Button CreateDialogButton(string content, string borderBrushKey) => new Button
    {
        Content = content,
        MinWidth = 120,
        Height = 38,
        Padding = new Thickness(16, 0),
        CornerRadius = new CornerRadius(8),
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
        BorderBrush = BrushFactory.FromResource(borderBrushKey),
        BorderThickness = new Thickness(1),
        Theme = GlassDialogButton
    };

    private static async Task<int> ShowMessageDialog(Window owner, string title, string message, string primaryBorderKey,
        params string[] choices)
    {
        var result = -1;
        var dialog = CreateDialogWindow(title);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 15
        };

        for (var i = 0; i < choices.Length; i++)
        {
            var index = i;
            var button = CreateDialogButton(choices[i], i == 0 ? primaryBorderKey : "GoldSoftBrush");
            button.Click += (_, _) =>
            {
                result = index;
                dialog.Close();
            };
            buttonPanel.Children.Add(button);
        }

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20, 50, 20, 20),
            Spacing = 20,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.White,
                    TextAlignment = TextAlignment.Center,
                    FontSize = 14
                },
                buttonPanel
            }
        };

        await dialog.ShowDialog(owner);
        return result;
    }

    public async Task<bool> ShowConfirmDialog(string title, string message) =>
        await ShowChoiceDialog(title, message, "Yes", "No") == 0;

    public Task<int> ShowChoiceDialog(string title, string message, params string[] choices) =>
        ShowMessageDialog(GetOwner()!, title, message, "GoldBrush", choices);

    public Task ShowInfoMessage(string title, string message) =>
        ShowMessageDialog(GetOwner()!, title, message, "GoldBrush", "OK");

    public Task ShowErrorMessage(string title, string message) =>
        ShowMessageDialog(GetOwner()!, title, message, "ErrorBrush", "OK");

    public Task ShowGameLogsNotFoundDialog() =>
        ShowInfoMessage("Game Logs Not Found", "Game logs not found. Launch the game at least once to generate logs.");

    public async Task ShowLauncherUpdateDialog()
    {
        var updateDialog = CreateDialogWindow("Launcher Update", width: 450);

        var contentPanel = new StackPanel
        {
            Margin = new Thickness(30, 50, 30, 30),
            Spacing = 20,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var titleText = new TextBlock
        {
            Text = "Updating Launcher...",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = BrushFactory.FromResource("GoldBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var messageText = new TextBlock
        {
            Text = "Downloading update...",
            FontSize = 14,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var progressBar = new ProgressBar
        {
            Width = 300,
            Height = 20,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            IsIndeterminate = false,
            Margin = new Thickness(0, 10, 0, 10)
        };

        contentPanel.Children.Add(titleText);
        contentPanel.Children.Add(messageText);
        contentPanel.Children.Add(progressBar);
        updateDialog.Content = contentPanel;

        _ = updateDialog.ShowDialog(GetOwner()!);

        try
        {
            var updateInfo = _updateManager == null ? null : await _updateManager.CheckForUpdatesAsync();
            if (_updateManager == null || updateInfo == null)
            {
                Logger.Warn("No automatic launcher update available, offering the downloads page");
                updateDialog.Close();
                await OfferManualDownload("This launcher can't update itself. Download the latest version from the website.");
                return;
            }

            messageText.Text = $"Downloading update {updateInfo.TargetFullRelease.Version}...";
            await _updateManager.DownloadUpdatesAsync(updateInfo, progress =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => progressBar.Value = progress);
            });

            messageText.Text = "Applying update and restarting...";
            _updateManager.ApplyUpdatesAndRestart(updateInfo);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to update launcher.");
            messageText.Text = $"Update failed: {ex.Message}";
            progressBar.IsIndeterminate = true;
            await Task.Delay(3000);
            updateDialog.Close();
            await OfferManualDownload("The launcher couldn't update itself. Download the latest version from the website.");
        }
    }

    private async Task OfferManualDownload(string message)
    {
        if (await ShowChoiceDialog("Launcher Update", message, "Open downloads page", "Cancel") == 0)
        {
            ProcessLauncher.LaunchUrl(Constants.LauncherDownloadUrl);
        }
    }

    public async Task ShowTesterAccessWindowAsync()
    {
        var viewModel = _serviceProvider.GetRequiredService<TesterAccessViewModel>();
        var window = CreateDialogWindow("Tester access", 320);
        window.Content = viewModel;
        viewModel.CloseAction = window.Close;

        await window.ShowDialog(GetOwner()!);
        await viewModel.ApplyChannelChangesAsync();
    }
}
