using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LoE_Launcher.Core;
using Models.Utils;
using NLog;

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
    private TextBlock _lblDownloadStats;
    private TextBlock _lblVersion;
    private ProgressBar _pbState;
    private Button _btnAction;

    private readonly IBrush _downloadColor;
    private readonly IBrush _updateColor;
    private readonly IBrush _launchColor;
    private readonly IBrush _errorColor;

    private bool _isDraggingLogo;
    private Point _lastLogoPosition;
    private Point _pointerStartPosition;
    private Point _lastPointerPosition;
    private readonly TransformGroup _logoTransform = new TransformGroup();
    private ScaleTransform _logoScaleTransform;
    private RotateTransform _logoRotateTransform;
    private TranslateTransform _logoTranslateTransform;

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
        SetupDraggableLogo();

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

        _backgroundImage = this.FindControl<Image>("backgroundImage")!;
        _logoImage = this.FindControl<Image>("logoImage")!;
        _lblDownloadedAmount = this.FindControl<TextBlock>("lblDownloadedAmount")!;
        _lblVersion = this.FindControl<TextBlock>("lblVersion")!;
        _pbState = this.FindControl<ProgressBar>("pbState")!;
        _btnAction = this.FindControl<Button>("btnAction")!;
        _lblDownloadStats = this.FindControl<TextBlock>("lblDownloadStats")!;
    }

    private void SetupDraggableLogo()
    {
        _logoScaleTransform = new ScaleTransform();
        _logoRotateTransform = new RotateTransform();
        _logoTranslateTransform = new TranslateTransform();

        _logoTransform.Children.Add(_logoScaleTransform);
        _logoTransform.Children.Add(_logoRotateTransform);
        _logoTransform.Children.Add(_logoTranslateTransform);

        _logoImage.RenderTransform = _logoTransform;

        _logoImage.Transitions = new Transitions
        {
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(200)
            }
        };

        ToolTip.SetTip(_logoImage, "You discovered me!");
        _logoImage.Cursor = new Cursor(StandardCursorType.Hand);

        _logoImage.PointerPressed += OnLogoPointerPressed;
        _logoImage.PointerMoved += OnLogoPointerMoved;
        _logoImage.PointerReleased += OnLogoPointerReleased;
        _logoImage.PointerCaptureLost += OnLogoPointerCaptureLost;
    }

    private void OnLogoPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(_logoImage).Properties.IsLeftButtonPressed)
        {
            _isDraggingLogo = true;

            _pointerStartPosition = e.GetPosition(this);
            _lastPointerPosition = _pointerStartPosition;
            _lastLogoPosition = new Point(_logoTranslateTransform.X, _logoTranslateTransform.Y);

            _logoScaleTransform.ScaleX = _logoScaleTransform.ScaleY = 0.95;

            e.Pointer.Capture(_logoImage);
            e.Handled = true;

            Logger.Info("Logo drag started");
        }
    }

    private void OnLogoPointerMoved(object sender, PointerEventArgs e)
    {
        if (!_isDraggingLogo) return;

        var currentPosition = e.GetPosition(this);

        var dragDeltaX = currentPosition.X - _pointerStartPosition.X;
        var dragDeltaY = currentPosition.Y - _pointerStartPosition.Y;

        var moveDeltaX = currentPosition.X - _lastPointerPosition.X;

        var newX = _lastLogoPosition.X + dragDeltaX;
        var newY = _lastLogoPosition.Y + dragDeltaY;

        _logoTranslateTransform.X = newX;
        _logoTranslateTransform.Y = newY;

        _logoRotateTransform.Angle = Math.Clamp(moveDeltaX * 2, -10, 10);

        _lastPointerPosition = currentPosition;

        e.Handled = true;
    }

    private void OnLogoPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingLogo) return;

        FinalizeLogoDrag();
        e.Handled = true;
    }

    private void OnLogoPointerCaptureLost(object sender, PointerCaptureLostEventArgs e)
    {
        if (_isDraggingLogo)
        {
            FinalizeLogoDrag();
        }
    }

    private void FinalizeLogoDrag()
    {
        _isDraggingLogo = false;

        _logoScaleTransform.ScaleX = _logoScaleTransform.ScaleY = 1.0;

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        timer.Tick += (s, e) => {
            _logoRotateTransform.Angle = 0;
            timer.Stop();
        };

        timer.Start();

        Logger.Info($"Logo dropped at position: {_logoTranslateTransform.X}, {_logoTranslateTransform.Y}");
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
        _backgroundImage.Source = new Bitmap(
            AssetLoader.Open(new Uri("avares://LoE-Launcher/Assets/Default-Background.png")));

        _logoImage.Source = new Bitmap(
            AssetLoader.Open(new Uri("avares://LoE-Launcher/Assets/Logo.png")));

        try
        {
            var cachedImage = await LoadCachedImageImmediately("Background.png");
            if (cachedImage != null)
            {
                _backgroundImage.Source = cachedImage;
            }

            _ = Task.Run(async () => {
                try
                {
                    var updatedImage = await UpdateCachedImage("http://theslowly.me/downloads/Background.png", "Background.png");
                    if (updatedImage != null)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => {
                            _backgroundImage.Source = updatedImage;
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Unable to update remote background image");
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Unable to load cached background image");
        }
    }

    private async Task<Bitmap?> LoadCachedImageImmediately(string cacheFileName)
    {
        var cacheDir = Path.Combine(_downloader.LauncherFolder.Path, "ImageCache");
        var cachePath = Path.Combine(cacheDir, cacheFileName);

        if (File.Exists(cachePath))
        {
            try
            {
                await using var fileReader = File.OpenRead(cachePath);
                return new Bitmap(fileReader);
            }
            catch
            {
                // ignore 
            }
        }

        return null;
    }

    private async Task<Bitmap?> UpdateCachedImage(string url, string cacheFileName)
    {
        var cacheDir = Path.Combine(_downloader.LauncherFolder.Path, "ImageCache");
        Directory.CreateDirectory(cacheDir);
        var cachePath = Path.Combine(cacheDir, cacheFileName);
        var tempPath = Path.Combine(cacheDir, $"temp_{cacheFileName}");

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            if (File.Exists(cachePath))
            {
                var lastModified = File.GetLastWriteTimeUtc(cachePath);
                client.DefaultRequestHeaders.IfModifiedSince = lastModified;
            }

            using var response = await client.GetAsync(url);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var memoryStream = new MemoryStream();

            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            await using (var fileStream = File.Create(tempPath))
            {
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(fileStream);
            }

            File.Move(tempPath, cachePath, true);

            memoryStream.Position = 0;
            return new Bitmap(memoryStream);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // ignore
                }
            }

            return null;
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

    private void OnTitleBarPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private async Task<Bitmap?> LoadImageFromUrl(string url)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            return new Bitmap(memoryStream);
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

            if (_downloader.State is GameState.Unknown)
            {
                sizeInfo = "";
                _lblDownloadStats.IsVisible = false;
            } 
            else if (_downloader.State is GameState.NotFound or GameState.UpdateAvailable)
            {
                sizeInfo = $"\n{BytesToString(_downloader.BytesDownloaded)} downloaded";
            
                var showStats = _downloader.Progress.Processing && 
                    !_downloader.Progress.Marquee &&
                    _downloader.DownloadStats.HasValidSpeed;
                             
                _lblDownloadStats.IsVisible = showStats;
            
                if (showStats)
                {
                    var speedText = _downloader.DownloadStats.CurrentSpeed;
                    var statsText = speedText;
                
                    if (_downloader.DownloadStats.HasValidTimeEstimate)
                    {
                        statsText += $" • {_downloader.DownloadStats.TimeRemaining} remaining";
                    }
                
                    _lblDownloadStats.Text = statsText;
                }
            }
            else
            {
                sizeInfo = $"\nGame size: {BytesToString(_downloader.TotalGameSize)}";
                _lblDownloadStats.IsVisible = false;
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
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
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
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White,
            Background = CreateGradientBrush("#D686D2", "#9C69B5"),
            HorizontalAlignment = HorizontalAlignment.Center,
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
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White,
            Background = CreateHorizontalGradientBrush("#D32F2F", "#F44336"),
            HorizontalAlignment = HorizontalAlignment.Center,
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
        string[] suf = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];
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
