using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using LoE_Launcher.Core;
using NLog;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.VisualTree;
using Models.Utils;

namespace LoE_Launcher;

public partial class MainWindow : Window
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Downloader _downloader;
    private readonly HttpClient _httpClient = new HttpClient();

    private DispatcherTimer _timer;
    private Stopwatch _downloadStopwatch = new Stopwatch();
    private bool _shownInfoMessage = false;
    private bool _shownOfflineMessage = false;

    private Image _backgroundImage;
    private Image _logoImage;
    private TextBlock _lblDownloadedAmount;
    private TextBlock _lblVersion;
    private ProgressBar _pbState;
    private Button _btnAction;

    private readonly IBrush _downloadColor;
    private readonly IBrush _updateColor;
    private readonly IBrush _launchColor;
    private readonly IBrush _errorColor;

    public MainWindow()
    {
        Logger.Info("Starting LoE Launcher");

        _downloadColor = CreateHorizontalGradientBrush("#9C69B5", "#D686D2");
        _updateColor = CreateHorizontalGradientBrush("#D69D45", "#E8B75C");
        _launchColor = CreateHorizontalGradientBrush("#9C69B5", "#D686D2");
        _errorColor = CreateHorizontalGradientBrush("#D32F2F", "#F44336");

        InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        _downloader = new Downloader();
            
        LoadBackgroundImages();

        var platform = PlatformUtils.OperatingSystem;
        _lblVersion.Text = $"Launcher Version: 0.5 Platform: {platform}";
        Logger.Info($"Running on platform: {platform}");

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        _btnAction.Content = "Checking...";
        _btnAction.IsEnabled = false;

        _downloadStopwatch.Start();
        InitializeDownloader();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Get references to UI elements
        // If these return null, something is seriously wrong
        _backgroundImage = this.FindControl<Image>("backgroundImage")!;
        _logoImage = this.FindControl<Image>("logoImage")!;
        _lblDownloadedAmount = this.FindControl<TextBlock>("lblDownloadedAmount")!;
        _lblVersion = this.FindControl<TextBlock>("lblVersion")!;
        _pbState = this.FindControl<ProgressBar>("pbState")!;
        _btnAction = this.FindControl<Button>("btnAction")!;
    }

    private async void InitializeDownloader()
    {
        try
        {
            Logger.Info("Initializing downloader");
            await Task.Run(async () => {
                try
                {
                    await _downloader.Cleanup();
                    await _downloader.RefreshState();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error during initialization");
                    throw;
                }
            });

            Logger.Info($"Initialization complete. Game state: {_downloader.State}");

            if (_downloader.State == GameState.Offline)
            {
                Logger.Warn("Cannot connect to update servers. App is in offline mode.");
                await ShowErrorMessage("Connection Error",
                    "Cannot connect to the game servers. Check your internet connection and try again.\n\nDetailed logs have been saved to your AppData folder.");

                _shownOfflineMessage = true;
            }

            _downloadStopwatch.Stop();
            Logger.Info($"Initialization took {_downloadStopwatch.Elapsed.TotalSeconds:F1} seconds");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize downloader");
            await ShowErrorMessage("Initialization Error", ex.Message);
        }
    }

    private async void LoadBackgroundImages()
    {
        try
        {
            // Todo: Move to our game server
            _backgroundImage.Source = await LoadImageFromUrl("https://i.imgur.com/KMHXf0h.png");
            _logoImage.Source = await LoadImageFromUrl("https://www.legendsofequestria.com/img/header.png");
        }
        catch (Exception ex)
        {
            await ShowErrorMessage("Image Load Error", $"Failed to load images: {ex.Message}");
            Background = new SolidColorBrush(Color.Parse("#9381BD")); // Fallback to lavender color
        }
    }

    private static LinearGradientBrush CreateGradientBrush(string topColor, string bottomColor)
    {
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.Parse(topColor), 0),
                new GradientStop(Color.Parse(bottomColor), 1)
            ]
        };
    }
        
    private static LinearGradientBrush CreateHorizontalGradientBrush(string leftColor, string rightColor)
    {
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.Parse(leftColor), 0),
                new GradientStop(Color.Parse(rightColor), 1)
            ]
        };
    }

    private void OnTitleBarPointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private async Task<Avalonia.Media.Imaging.Bitmap?> LoadImageFromUrl(string url)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            return new Avalonia.Media.Imaging.Bitmap(memoryStream);
        }
        catch
        {
            return null;
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() => {
            // Update progress bar
            if (_downloader.Progress.Marquee)
            {
                _pbState.IsIndeterminate = true;
            }
            else
            {
                _pbState.IsIndeterminate = false;
                var progressPercentage = _downloader.Progress.Max > 0
                    ? (double)_downloader.Progress.Current / _downloader.Progress.Max
                    : 0;
                _pbState.Value = progressPercentage * 100;
            }

            var statusText = _downloader.Progress.Text;
            var sizeInfo = "";

            if (_downloader.Progress.Processing || 
                _downloader.State == GameState.NotFound || 
                _downloader.State == GameState.UpdateAvailable)
            {
                sizeInfo = $"\n{BytesToString(_downloader.BytesDownloaded)} downloaded";
            }
            else if (_downloader.State == GameState.UpToDate)
            {
                sizeInfo = $"\nGame size: {BytesToString(_downloader.TotalGameSize)}";
            }
            
            _lblDownloadedAmount.Text = $"{statusText}{sizeInfo}";

            var enabledState = true;

            switch (_downloader.State)
            {
                case GameState.Unknown:
                    _btnAction.Content = "Checking...";
                    _btnAction.Background = _downloadColor;
                    enabledState = false;
                    break;
                case GameState.NotFound:
                    _btnAction.Content = "Install";
                    _btnAction.Background = _downloadColor;
                    enabledState = true;
                    break;
                case GameState.UpdateAvailable:
                    _btnAction.Content = "Update";
                    _btnAction.Background = _updateColor;
                    enabledState = true;
                    break;
                case GameState.UpToDate:
                    _btnAction.Content = "Launch";
                    _btnAction.Background = _launchColor;
                    enabledState = true;
                    break;
                case GameState.Offline:
                    _btnAction.Content = "Offline";
                    _btnAction.Background = _errorColor;
                    enabledState = false;
                    break;
                case GameState.LauncherOutOfDate:
                    _btnAction.Content = "Error";
                    _btnAction.Background = _errorColor;
                    enabledState = false;
                    break;
            }

            if (_downloader.Progress.Processing)
            {
                enabledState = false;
            }

            _btnAction.IsEnabled = enabledState;
        });
    }

    private async void OnActionButtonClicked(object sender, RoutedEventArgs e)
    {
        // Disable button to prevent multiple clicks
        _btnAction.IsEnabled = false;

        // Button press animation
        _btnAction.Classes.Add("pressed");
        await Task.Delay(200);
        _btnAction.Classes.Remove("pressed");

        try
        {
            switch (_downloader.State)
            {
                case GameState.NotFound:
                case GameState.UpdateAvailable:
                    await InstallOrUpdateGame();
                    break;
                case GameState.UpToDate:
                    LaunchGame();
                    break;
            }
        }
        catch (Exception ex)
        {
            await ShowErrorMessage("Error", ex.Message);
        }
    }

    private async Task InstallOrUpdateGame()
    {
        _downloadStopwatch.Restart();
        Logger.Info($"Starting game {(_downloader.State == GameState.NotFound ? "installation" : "update")}");

        try
        {
            await Task.Run(() => _downloader.DoInstallation());
            Logger.Info("Installation/update completed successfully");

            var originalBrush = _pbState.Foreground;
            _pbState.Foreground = _launchColor;
            await Task.Delay(1000);
            _pbState.Foreground = originalBrush;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Installation/update failed");
            throw;
        }
        finally
        {
            _downloadStopwatch.Stop();
            Logger.Debug($"Installation/update process took {_downloadStopwatch.Elapsed.TotalSeconds:F1} seconds");
        }
    }

    private void LaunchGame()
    {
        try
        {
            var currentOS = PlatformUtils.OperatingSystem;
            Logger.Info($"Launching game on {currentOS}");

            switch (currentOS)
            {
                case OS.WindowsX64:
                case OS.WindowsX86:
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.Combine(_downloader.GameInstallFolder.Path, "loe.exe"),
                        UseShellExecute = PlatformUtils.UseShellExecute
                    });
                    break;

                case OS.MacIntel:
                case OS.MacArm:
                    var macAppPath = Path.Combine(_downloader.GameInstallFolder.Path, "LoE.app");

                    var permissionProcess = new Process();
                    permissionProcess.RunInlineAndWait(new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"-R 777 \"{macAppPath}\"",
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Minimized
                    });

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = macAppPath,
                        UseShellExecute = PlatformUtils.UseShellExecute
                    });
                    break;

                case OS.X11:
                    var is64Bit = Environment.Is64BitProcess;
                    var linuxExePath = Path.Combine(_downloader.GameInstallFolder.Path, $"LoE.x86{(is64Bit ? "_64" : "")}");

                    var linuxPermProcess = new Process();
                    linuxPermProcess.RunInlineAndWait(new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"-R 777 \"{linuxExePath}\"",
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Minimized
                    });

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = linuxExePath,
                        UseShellExecute = PlatformUtils.UseShellExecute
                    });
                    break;

                case OS.Other:
                default:
                    throw new PlatformNotSupportedException("This platform is not supported.");
            }
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(async () => {
                await ShowErrorMessage("Launch Error", ex.Message);
            });
        }
    }

    private async void OnSettingsButtonClicked(object sender, RoutedEventArgs e)
    {
        var settingsMenu = new Window
        {
            Title = "Settings",
            Width = 320,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#7A68B5")),
            TransparencyLevelHint = [WindowTransparencyLevel.AcrylicBlur],
            ExtendClientAreaToDecorationsHint = true,
            ExtendClientAreaTitleBarHeightHint = 30
        };

        var panel = new StackPanel
        {
            Margin = new Thickness(20, 50, 20, 20),
            Spacing = 15
        };

        var repairButton = CreateSettingsButton("Repair Game Files");
        var logFolderButton = CreateSettingsButton("Open Log Folder");

        repairButton.Click += OnRepairGameClicked;
        logFolderButton.Click += OnOpenLogFolderClicked;

        panel.Children.Add(repairButton);
        panel.Children.Add(logFolderButton);

        settingsMenu.Content = panel;
        await settingsMenu.ShowDialog(this);
    }

    private Button CreateSettingsButton(string text)
    {
        var button = new Button
        {
            Content = text,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Height = 44,
            FontSize = 15,
            FontWeight = FontWeight.Medium,
            CornerRadius = new CornerRadius(22),
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.Parse("#D686D2")),
            Padding = new Thickness(0),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#E8B75C"))
        };

        // Add custom hover style
        button.Classes.Add("settings-button");

        return button;
    }

    private async void OnRepairGameClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            ((Window)((Button)sender).FindAncestorOfType<Window>()).Close();
            var confirmResult = await ShowConfirmDialog(
                "Repair Game",
                "This will verify all game files and re-download any corrupted or missing files. Continue?");

            if (!confirmResult)
            {
                return;
            }

            _btnAction.IsEnabled = false;

            _downloadStopwatch.Restart();
            Logger.Info("Starting game repair process");

            await Task.Run(() => _downloader.RepairGame());

            Logger.Info("Game repair completed successfully");

            var originalBrush = _pbState.Foreground;
            _pbState.Foreground = _launchColor;
            await Task.Delay(1000);
            _pbState.Foreground = originalBrush;

            await ShowInfoMessage("Repair Complete", "Game files have been verified and repaired.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Game repair failed");
            await ShowErrorMessage("Repair Error", ex.Message);
        }
        finally
        {
            _downloadStopwatch.Stop();
        }
    }

    private static void OnOpenLogFolderClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            ((Window)((Button)sender).FindAncestorOfType<Window>()).Close();

            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LoE_Launcher", "Logs");

            Process.Start(new ProcessStartInfo
            {
                FileName = logDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to open log directory");
        }
    }

    private async Task<bool> ShowConfirmDialog(string title, string message)
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
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 15
        };

        var yesButton = new Button
        {
            Content = "Yes",
            Width = 100,
            Height = 38,
            CornerRadius = new CornerRadius(19),
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
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
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.Parse("#7A68B5")),
            FontWeight = FontWeight.Medium
        };

        yesButton.Click += (s, e) => {
            result = true;
            confirmBox.Close();
        };

        noButton.Click += (s, e) => {
            result = false;
            confirmBox.Close();
        };

        buttonPanel.Children.Add(yesButton);
        buttonPanel.Children.Add(noButton);

        panel.Children.Add(messageText);
        panel.Children.Add(buttonPanel);

        confirmBox.Content = panel;

        await confirmBox.ShowDialog(this);
        return result;
    }

    private async Task ShowInfoMessage(string title, string message)
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
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = Brushes.White,
            Background = CreateGradientBrush("#D686D2", "#9C69B5"),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            FontWeight = FontWeight.Medium
        };

        okButton.Click += (s, e) => messageBox.Close();

        panel.Children.Add(messageText);
        panel.Children.Add(okButton);

        messageBox.Content = panel;

        await messageBox.ShowDialog(this);
    }

    private async Task ShowErrorMessage(string title, string message)
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
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = Brushes.White,
            Background = CreateHorizontalGradientBrush("#D32F2F", "#F44336"),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            FontWeight = FontWeight.Medium
        };

        okButton.Click += (s, e) => messageBox.Close();

        panel.Children.Add(messageText);
        panel.Children.Add(okButton);

        messageBox.Content = panel;

        await messageBox.ShowDialog(this);
    }
    
    private static string BytesToString(long byteCount)
    {
        string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        if (byteCount == 0)
        {
            return $"0{suf[0]}";
        }

        var bytes = Math.Abs(byteCount);
        var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        var num = Math.Round(bytes / Math.Pow(1024, place), 1);
        return (Math.Sign(byteCount) * num).ToString(CultureInfo.InvariantCulture) + suf[place];
    }
}