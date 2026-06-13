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
        Background = new SolidColorBrush(Color.Parse("#F21A1028")),
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        CanResize = false,
        FontFamily = new FontFamily("Segoe UI, SF Pro Display, Arial, sans-serif")
    };

    public async Task<bool> ShowConfirmDialog(string title, string message)
    {
        var result = false;
        var confirmBox = CreateDialogWindow(title);

        var panel = new StackPanel
        {
            Margin = new Thickness(20, 50, 20, 20),
            Spacing = 20
        };

        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Center,
            FontSize = 14
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 15
        };

        var yesButton = new Button
        {
            Content = "Yes",
            Width = 100,
            Height = 38,
            CornerRadius = new CornerRadius(8),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderBrush = new SolidColorBrush(Color.Parse("#f0be4a")),
            BorderThickness = new Thickness(1),
            Theme = GlassDialogButton
        };

        var noButton = new Button
        {
            Content = "No",
            Width = 100,
            Height = 38,
            CornerRadius = new CornerRadius(8),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderBrush = new SolidColorBrush(Color.Parse("#80f0be4a")),
            BorderThickness = new Thickness(1),
            Theme = GlassDialogButton
        };

        yesButton.Click += (s, e) =>
        {
            result = true;
            confirmBox.Close();
        };

        noButton.Click += (s, e) =>
        {
            result = false;
            confirmBox.Close();
        };

        buttonPanel.Children.Add(yesButton);
        buttonPanel.Children.Add(noButton);
        panel.Children.Add(messageText);
        panel.Children.Add(buttonPanel);
        confirmBox.Content = panel;

        await confirmBox.ShowDialog(GetOwner()!);
        return result;
    }

    public async Task ShowInfoMessage(string title, string message)
    {
        var messageBox = CreateDialogWindow(title);

        var panel = new StackPanel
        {
            Margin = new Thickness(20, 50, 20, 20),
            Spacing = 20
        };

        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Center,
            FontSize = 14
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 120,
            Height = 38,
            CornerRadius = new CornerRadius(8),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            BorderBrush = new SolidColorBrush(Color.Parse("#f0be4a")),
            BorderThickness = new Thickness(1),
            Theme = GlassDialogButton
        };

        okButton.Click += (s, e) => messageBox.Close();

        panel.Children.Add(messageText);
        panel.Children.Add(okButton);
        messageBox.Content = panel;

        await messageBox.ShowDialog(GetOwner()!);
    }

    public async Task ShowErrorMessage(string title, string message)
    {
        var messageBox = CreateDialogWindow(title);

        var panel = new StackPanel
        {
            Margin = new Thickness(20, 50, 20, 20),
            Spacing = 20
        };

        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Center,
            FontSize = 14
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 120,
            Height = 38,
            CornerRadius = new CornerRadius(8),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            BorderBrush = new SolidColorBrush(Color.Parse("#E05555")),
            BorderThickness = new Thickness(1),
            Theme = GlassDialogButton
        };

        okButton.Click += (s, e) => messageBox.Close();

        panel.Children.Add(messageText);
        panel.Children.Add(okButton);
        messageBox.Content = panel;

        await messageBox.ShowDialog(GetOwner()!);
    }

    public async Task ShowGameLogsNotFoundDialog()
    {
        var messageBox = CreateDialogWindow("Game Logs Not Found", width: 300);
        messageBox.SizeToContent = SizeToContent.Manual;
        messageBox.Height = 150;

        var messagePanel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 15,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var messageText = new TextBlock
        {
            Text = "Game logs not found. Launch the game at least once to generate logs.",
            FontSize = 14,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderBrush = new SolidColorBrush(Color.Parse("#f0be4a")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Theme = GlassDialogButton
        };

        okButton.Click += (s, e) => messageBox.Close();

        messagePanel.Children.Add(messageText);
        messagePanel.Children.Add(okButton);
        messageBox.Content = messagePanel;

        await messageBox.ShowDialog(GetOwner()!);
    }

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
            Foreground = new SolidColorBrush(Color.Parse("#f0be4a")),
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
            if (_updateManager == null)
            {
                Logger.Warn("UpdateManager is not available. Cannot perform update.");
                messageText.Text = "Update system not available.";
                await Task.Delay(2000);
                updateDialog.Close();
                await ShowErrorMessage("Update Unavailable", "The update system is not available in this build.");
                return;
            }

            var updateInfo = await _updateManager.CheckForUpdatesAsync();
            if (updateInfo == null)
            {
                messageText.Text = "No updates found, closing dialog.";
                await Task.Delay(1500);
                updateDialog.Close();
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
            await ShowErrorMessage("Update Failed", "Failed to update the launcher. Please try again later.");
        }
    }

    public async Task ShowSettingsWindowAsync()
    {
        var viewModel = _serviceProvider.GetRequiredService<SettingsViewModel>();
        var settingsWindow = new Window
        {
            Title = "Options",
            Width = 340,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#F21A1028")),
            CanResize = false,
            FontFamily = new FontFamily("Segoe UI, SF Pro Display, Arial, sans-serif"),
            Content = viewModel
        };

        viewModel.CloseAction = settingsWindow.Close;

        await settingsWindow.ShowDialog(GetOwner()!);
    }
}
