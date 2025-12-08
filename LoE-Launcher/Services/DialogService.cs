using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
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

    public async Task<bool> ShowConfirmDialog(string title, string message)
    {
        var result = false;
        var confirmBox = new Window
        {
            Title = title,
            Width = 350,
            SizeToContent = SizeToContent.Height,
            Background = new SolidColorBrush(Color.Parse("#9381BD")),
            TransparencyLevelHint = [WindowTransparencyLevel.AcrylicBlur],
            ExtendClientAreaToDecorationsHint = true,
            ExtendClientAreaTitleBarHeightHint = 30,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

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
            CornerRadius = new CornerRadius(19),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.Parse("#D686D2")),
            FontWeight = FontWeight.Medium
        };

        var noButton = new Button
        {
            Content = "No",
            Width = 100,
            Height = 38,
            CornerRadius = new CornerRadius(19),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.Parse("#7A68B5")),
            FontWeight = FontWeight.Medium
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
        var messageBox = new Window
        {
            Title = title,
            Width = 350,
            SizeToContent = SizeToContent.Height,
            Background = new SolidColorBrush(Color.Parse("#9381BD")),
            TransparencyLevelHint = [WindowTransparencyLevel.AcrylicBlur],
            ExtendClientAreaToDecorationsHint = true,
            ExtendClientAreaTitleBarHeightHint = 30,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

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
            CornerRadius = new CornerRadius(19),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White,
            Background = BrushFactory.CreateVerticalGradient("#D686D2", "#9C69B5"),
            HorizontalAlignment = HorizontalAlignment.Center,
            FontWeight = FontWeight.Medium
        };

        okButton.Click += (s, e) => messageBox.Close();

        panel.Children.Add(messageText);
        panel.Children.Add(okButton);

        messageBox.Content = panel;

        await messageBox.ShowDialog(GetOwner()!);
    }

    public async Task ShowErrorMessage(string title, string message)
    {
        var messageBox = new Window
        {
            Title = title,
            Width = 350,
            SizeToContent = SizeToContent.Height,
            Background = new SolidColorBrush(Color.Parse("#9381BD")),
            TransparencyLevelHint = [WindowTransparencyLevel.AcrylicBlur],
            ExtendClientAreaToDecorationsHint = true,
            ExtendClientAreaTitleBarHeightHint = 30,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

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
            CornerRadius = new CornerRadius(19),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White,
            Background = BrushFactory.CreateHorizontalGradient("#D32F2F", "#F44336"),
            HorizontalAlignment = HorizontalAlignment.Center,
            FontWeight = FontWeight.Medium
        };

        okButton.Click += (s, e) => messageBox.Close();

        panel.Children.Add(messageText);
        panel.Children.Add(okButton);

        messageBox.Content = panel;

        await messageBox.ShowDialog(GetOwner()!);
    }

    public async Task ShowGameLogsNotFoundDialog()
    {
        var messageBox = new Window
        {
            Title = "Game Logs Not Found",
            Width = 300,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#9C69B5")),
            CanResize = false
        };

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
            Height = 30,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new SolidColorBrush(Color.Parse("#B37DC7")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(5)
        };

        okButton.Click += (s, e) => messageBox.Close();

        messagePanel.Children.Add(messageText);
        messagePanel.Children.Add(okButton);
        messageBox.Content = messagePanel;

        await messageBox.ShowDialog(GetOwner()!);
    }

    public async Task ShowLauncherUpdateDialog()
    {
        var updateDialog = new Window
        {
            Title = "Launcher Update",
            Width = 450,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#9C69B5")),
            TransparencyLevelHint = [WindowTransparencyLevel.AcrylicBlur],
            ExtendClientAreaToDecorationsHint = true,
            ExtendClientAreaTitleBarHeightHint = 30,
            CanResize = false
        };

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
            Foreground = Brushes.White,
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
                await Task.Delay(1500); // Give user time to read
                updateDialog.Close();
                return;
            }

            messageText.Text = $"Downloading update {updateInfo.TargetFullRelease.Version}...";
            await _updateManager.DownloadUpdatesAsync(updateInfo, progress =>
            {
                progressBar.Value = progress;
            });

            messageText.Text = "Applying update and restarting...";
            _updateManager.ApplyUpdatesAndRestart(updateInfo);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to update launcher.");
            messageText.Text = $"Update failed: {ex.Message}";
            progressBar.IsIndeterminate = true;
            await Task.Delay(3000); // Give user time to read error
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
            Background = new SolidColorBrush(Color.Parse("#9C69B5")),
            TransparencyLevelHint = [WindowTransparencyLevel.AcrylicBlur],
            ExtendClientAreaToDecorationsHint = true,
            ExtendClientAreaTitleBarHeightHint = 30,
            CanResize = false,
            Content = viewModel
        };

        viewModel.CloseAction = settingsWindow.Close;
        
        await settingsWindow.ShowDialog(GetOwner()!);
    }
    
    private static Button CreateCustomButton(string text, string normalColor, string hoverColor, int width)
    {
        var normalBrush = new SolidColorBrush(Color.Parse(normalColor));
        var hoverBrush = new SolidColorBrush(Color.Parse(hoverColor));

        var border = new Border
        {
            Background = normalBrush,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(0),
            Width = width,
            Height = 40,
            Child = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var button = new Button
        {
            Content = border,
            Width = width,
            Height = 40,
            Cursor = new Cursor(StandardCursorType.Hand),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0)
        };

        button.PointerEntered += (s, args) => border.Background = hoverBrush;
        button.PointerExited += (s, args) => border.Background = normalBrush;
        button.PointerPressed += (s, args) => border.Background = new SolidColorBrush(Color.Parse(normalColor)) { Opacity = 0.8 };
        button.PointerReleased += (s, args) => border.Background = hoverBrush;

        return button;
    }
}
