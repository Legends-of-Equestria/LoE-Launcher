using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using LoE_Launcher.Utils;
using NLog;

namespace LoE_Launcher.Services;

public class DialogService(Window owner)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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

        await confirmBox.ShowDialog(owner);
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

        await messageBox.ShowDialog(owner);
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

        await messageBox.ShowDialog(owner);
    }

    public async Task ShowLauncherUpdateDialog()
    {
        var updateDialog = new Window
        {
            Title = "Launcher Update Required",
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
            Text = "Launcher Update Required",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var messageText = new TextBlock
        {
            Text = "Your launcher is out of date. Please download the latest version to continue.",
            FontSize = 14,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 15
        };

        var downloadButton = CreateCustomButton("Download Latest", "#D686D2", "#E8A6E2", 140);
        var cancelButton = CreateCustomButton("Cancel", "#8A7AB8", "#A691C7", 100);

        downloadButton.Click += (s, args) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://legendsofequestria.com/downloads",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open download URL");
            }
            updateDialog.Close();
        };

        cancelButton.Click += (s, args) => updateDialog.Close();

        buttonPanel.Children.Add(downloadButton);
        buttonPanel.Children.Add(cancelButton);

        contentPanel.Children.Add(titleText);
        contentPanel.Children.Add(messageText);
        contentPanel.Children.Add(buttonPanel);

        updateDialog.Content = contentPanel;
        await updateDialog.ShowDialog(owner);
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
