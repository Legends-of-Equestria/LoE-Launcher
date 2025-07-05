using System;
using System.Collections.Generic;
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
    private const string CacheDirectoryName = "Cache";

    private readonly Downloader _downloader;
    private readonly HttpClient _httpClient = new HttpClient();

    private DispatcherTimer _timer;
    private Stopwatch _downloadStopwatch = new Stopwatch();
    private bool _shownInfoMessage = false;
    private bool _shownOfflineMessage = false;

    private Image _backgroundImage;
    private Image _logoImage;
    private ProgressBar _pbState;
    private Button _btnAction;
    private StackPanel _changelogPanel;
    private Grid _progressOverlay;
    private TextBlock _progressPercentage;
    private TextBlock _downloadSpeed;
    private TextBlock _downloadStatus;

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
    
    private Point _logoVelocity;
    private double _logoAngularVelocity;
    private DispatcherTimer _physicsTimer;
    private DateTime _lastMoveTime;
    private bool _hasShownTooltip;

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

        _ = LoadBackgroundImages();
        _ = LoadChangelog();
        SetupDraggableLogo();

        Logger.Info($"Running on platform: {PlatformUtils.OperatingSystem}");

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
        _pbState = this.FindControl<ProgressBar>("pbState")!;
        _btnAction = this.FindControl<Button>("btnAction")!;
        _changelogPanel = this.FindControl<StackPanel>("changelogPanel")!;
        _progressOverlay = this.FindControl<Grid>("progressOverlay")!;
        _progressPercentage = this.FindControl<TextBlock>("progressPercentage")!;
        _downloadSpeed = this.FindControl<TextBlock>("downloadSpeed")!;
        _downloadStatus = this.FindControl<TextBlock>("downloadStatus")!;
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

        _logoImage.Transitions =
        [
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(300)
            }
        ];

        _logoImage.Cursor = new Cursor(StandardCursorType.Hand);
        _logoImage.PointerEntered += OnLogoPointerEntered;

        _logoVelocity = new Point(0, 0);
        _logoAngularVelocity = 0;
        
        _physicsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _physicsTimer.Tick += OnPhysicsTick;
        
        _logoImage.PointerPressed += OnLogoPointerPressed;
        _logoImage.PointerMoved += OnLogoPointerMoved;
        _logoImage.PointerReleased += OnLogoPointerReleased;
        _logoImage.PointerCaptureLost += OnLogoPointerCaptureLost;
    }

    private void OnLogoPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(_logoImage).Properties.IsLeftButtonPressed)
        {
            _isDraggingLogo = true;

            // Clear tooltip while dragging
            ToolTip.SetTip(_logoImage, null);

            _pointerStartPosition = e.GetPosition(this);
            _lastPointerPosition = _pointerStartPosition;
            _lastLogoPosition = new Point(_logoTranslateTransform.X, _logoTranslateTransform.Y);
            _lastMoveTime = DateTime.UtcNow;

            _physicsTimer.Stop();
            _logoVelocity = new Point(0, 0);
            _logoAngularVelocity = 0;

            _logoScaleTransform.ScaleX = _logoScaleTransform.ScaleY = 0.9;
            _logoRotateTransform.Angle = (Random.Shared.NextDouble() - 0.5) * 6;

            e.Pointer.Capture(_logoImage);
            e.Handled = true;

        }
    }

    private void OnLogoPointerMoved(object sender, PointerEventArgs e)
    {
        if (!_isDraggingLogo) return;

        var currentPosition = e.GetPosition(this);
        var currentTime = DateTime.UtcNow;
        var deltaTime = (currentTime - _lastMoveTime).TotalSeconds;

        if (deltaTime > 0)
        {
            var velocityX = (currentPosition.X - _lastPointerPosition.X) / deltaTime;
            var velocityY = (currentPosition.Y - _lastPointerPosition.Y) / deltaTime;
            _logoVelocity = new Point(
                _logoVelocity.X * 0.7 + velocityX * 0.3,
                _logoVelocity.Y * 0.7 + velocityY * 0.3
            );
        }

        var dragDeltaX = currentPosition.X - _pointerStartPosition.X;
        var dragDeltaY = currentPosition.Y - _pointerStartPosition.Y;

        var newX = _lastLogoPosition.X + dragDeltaX;
        var newY = _lastLogoPosition.Y + dragDeltaY;

        var constrainedPosition = ApplyBoundaryConstraints(new Point(newX, newY));
        _logoTranslateTransform.X = constrainedPosition.X;
        _logoTranslateTransform.Y = constrainedPosition.Y;

        var speed = Math.Sqrt(_logoVelocity.X * _logoVelocity.X + _logoVelocity.Y * _logoVelocity.Y);
        var rotationAngle = Math.Clamp(_logoVelocity.X * 0.05, -15, 15);
        _logoRotateTransform.Angle = rotationAngle;
        var scaleBoost = Math.Min(speed * 0.0001, 0.05);
        var targetScale = 0.9 + scaleBoost;
        _logoScaleTransform.ScaleX = _logoScaleTransform.ScaleY = targetScale;

        _lastPointerPosition = currentPosition;
        _lastMoveTime = currentTime;

        e.Handled = true;
    }

    private void OnLogoPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingLogo) return;

        _logoAngularVelocity = _logoRotateTransform.Angle * 0.2;

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

        _physicsTimer.Start();

    }

    private void OnPhysicsTick(object? sender, EventArgs e)
    {
        const double friction = 0.95;
        const double angularFriction = 0.92;
        const double minVelocity = 5.0;
        const double minAngularVelocity = 0.5;

        _logoVelocity = new Point(_logoVelocity.X * friction, _logoVelocity.Y * friction);
        _logoAngularVelocity *= angularFriction;

        var deltaTime = 0.016;
        var newX = _logoTranslateTransform.X + _logoVelocity.X * deltaTime;
        var newY = _logoTranslateTransform.Y + _logoVelocity.Y * deltaTime;
        
        var unconstrainedPosition = new Point(newX, newY);
        var constrainedPosition = ApplyBoundaryConstraints(unconstrainedPosition);
        
        // Check for boundary collisions and apply bounce
        var hitBoundaryX = Math.Abs(constrainedPosition.X - newX) > 0.1;
        var hitBoundaryY = Math.Abs(constrainedPosition.Y - newY) > 0.1;
        
        if (hitBoundaryX || hitBoundaryY)
        {
            
            if (hitBoundaryX)
            {
                _logoVelocity = new Point(_logoVelocity.X * -0.4, _logoVelocity.Y * 0.8); // Bounce X
                _logoAngularVelocity += (Random.Shared.NextDouble() - 0.5) * 50; // Add spin
            }
            if (hitBoundaryY)
            {
                _logoVelocity = new Point(_logoVelocity.X * 0.8, _logoVelocity.Y * -0.4); // Bounce Y
                _logoAngularVelocity += (Random.Shared.NextDouble() - 0.5) * 50; // Add spin
            }
        }
        
        _logoTranslateTransform.X = constrainedPosition.X;
        _logoTranslateTransform.Y = constrainedPosition.Y;

        // Apply angular velocity to rotation
        _logoRotateTransform.Angle += _logoAngularVelocity * deltaTime;

        // Gradually return scale to normal
        var currentScale = _logoScaleTransform.ScaleX;
        var targetScale = 1.0;
        _logoScaleTransform.ScaleX = _logoScaleTransform.ScaleY = 
            currentScale + (targetScale - currentScale) * 0.1;

        var speed = Math.Sqrt(_logoVelocity.X * _logoVelocity.X + _logoVelocity.Y * _logoVelocity.Y);
        if (speed < minVelocity && Math.Abs(_logoAngularVelocity) < minAngularVelocity)
        {
            _physicsTimer.Stop();
            
            var finalTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            var startAngle = _logoRotateTransform.Angle;
            var startScale = _logoScaleTransform.ScaleX;
            var animationTime = 0.0;
            const double animationDuration = 0.5;
            
            finalTimer.Tick += (s, args) =>
            {
                animationTime += 0.016;
                var progress = Math.Min(animationTime / animationDuration, 1.0);
                var easeProgress = 1 - Math.Pow(1 - progress, 3); // Ease-out cubic
                
                _logoRotateTransform.Angle = startAngle * (1 - easeProgress);
                _logoScaleTransform.ScaleX = _logoScaleTransform.ScaleY = 
                    startScale + (1.0 - startScale) * easeProgress;
                
                if (progress >= 1.0)
                {
                    finalTimer.Stop();
                }
            };
            
            finalTimer.Start();
        }
    }

    private void OnLogoPointerEntered(object? sender, PointerEventArgs e)
    {
        // Only show tooltip if logo is not moving and not being dragged
        if (!_isDraggingLogo && !_physicsTimer.IsEnabled)
        {
            ToolTip.SetTip(_logoImage, "You discovered me! Drag me around!");
        }
    }

    private Point ApplyBoundaryConstraints(Point position)
    {
        var windowWidth = Math.Max(this.ClientSize.Width, 800);
        var windowHeight = Math.Max(this.ClientSize.Height, 500);
        var logoWidth = Math.Max(_logoImage.Bounds.Width, 203);
        var logoHeight = Math.Max(_logoImage.Bounds.Height, 108);
        
        var logoInitialX = windowWidth - 20 - logoWidth;
        var logoInitialY = 40;
        
        var currentAbsoluteX = logoInitialX + position.X;
        var currentAbsoluteY = logoInitialY + position.Y;
        
        var minAbsoluteX = -logoWidth * 0.7;
        var maxAbsoluteX = windowWidth - logoWidth * 0.3;
        var minAbsoluteY = -logoHeight * 0.7;
        var maxAbsoluteY = windowHeight - logoHeight * 0.3;
        
        var constrainedAbsoluteX = Math.Clamp(currentAbsoluteX, minAbsoluteX, maxAbsoluteX);
        var constrainedAbsoluteY = Math.Clamp(currentAbsoluteY, minAbsoluteY, maxAbsoluteY);
        
        return new Point(constrainedAbsoluteX - logoInitialX, constrainedAbsoluteY - logoInitialY);
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

    private async Task LoadBackgroundImages()
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
                    var updatedImage = await UpdateCachedImage("https://loedata.legendsofequestria.com/data/Background.png", "Background.png");
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
        var cacheDir = Path.Combine(_downloader.LauncherFolder.Path, CacheDirectoryName);
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
        var cacheDir = Path.Combine(_downloader.LauncherFolder.Path, CacheDirectoryName);
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

            // Show progress bar during processing, show action button when not processing
            _pbState.IsVisible = _downloader.Progress.Processing;
            _btnAction.IsVisible = !_downloader.Progress.Processing;
            _btnAction.IsEnabled = enabledState;

            if (_downloader.Progress.Processing)
            {
                _progressOverlay.IsVisible = true;
                
                var progressPercentage = _downloader.Progress.Max > 0
                    ? (double)_downloader.Progress.Current / _downloader.Progress.Max
                    : 0;
                _progressPercentage.Text = $"{Math.Round(progressPercentage * 100)}%";
                
                if (_downloader.DownloadStats.HasValidSpeed)
                {
                    _downloadSpeed.Text = _downloader.DownloadStats.CurrentSpeed;
                }
                else
                {
                    _downloadSpeed.Text = "";
                }
                
                _downloadStatus.Text = statusText;
            }
            else
            {
                _progressOverlay.IsVisible = false;
            }
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
            Width = 340,
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
            Margin = new Thickness(25, 50, 25, 25),
            Spacing = 15
        };

        // Title
        var titleText = new TextBlock
        {
            Text = "Settings",
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var repairButton = CreateSettingsButton("Repair Game Files", "Verify and fix corrupted game files");
        var logFolderButton = CreateSettingsButton("Open Log Folder", "View launcher logs and debug information");

        repairButton.Click += OnRepairGameClicked;
        logFolderButton.Click += OnOpenLogFolderClicked;

        contentPanel.Children.Add(titleText);
        contentPanel.Children.Add(repairButton);
        contentPanel.Children.Add(logFolderButton);

        settingsMenu.Content = contentPanel;
        await settingsMenu.ShowDialog(this);
    }

    private Button CreateSettingsButton(string text, string description)
    {
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Height = 55,
            CornerRadius = new CornerRadius(10),
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.Parse("#30FFFFFF")),
            Padding = new Thickness(20, 12),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#f0be4a")),
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0
        };

        var titleText = new TextBlock
        {
            Text = text,
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        var descText = new TextBlock
        {
            Text = description,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#D0FFFFFF")),
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, -2, 0, 0)
        };

        contentPanel.Children.Add(titleText);
        contentPanel.Children.Add(descText);

        button.Content = contentPanel;

        // Add hover effects similar to changelog panel
        button.PointerEntered += (s, e) => {
            button.Background = new SolidColorBrush(Color.Parse("#50FFFFFF"));
            button.BorderBrush = new SolidColorBrush(Color.Parse("#FFD700"));
            button.RenderTransform = new ScaleTransform { ScaleX = 1.02, ScaleY = 1.02 };
        };

        button.PointerExited += (s, e) => {
            button.Background = new SolidColorBrush(Color.Parse("#30FFFFFF"));
            button.BorderBrush = new SolidColorBrush(Color.Parse("#f0be4a"));
            button.RenderTransform = new ScaleTransform { ScaleX = 1.0, ScaleY = 1.0 };
        };

        // Add transitions
        button.Transitions = new Transitions
        {
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(200)
            },
            new BrushTransition
            {
                Property = Button.BackgroundProperty,
                Duration = TimeSpan.FromMilliseconds(200)
            },
            new BrushTransition
            {
                Property = Button.BorderBrushProperty,
                Duration = TimeSpan.FromMilliseconds(200)
            }
        };

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
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

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

    private void OnYoutubeButtonClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.youtube.com/@legendsofequestria",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to open YouTube link");
        }
    }

    private void OnDiscordButtonClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://discord.com/invite/legendsofeq",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to open Discord link");
        }
    }

    private void OnTwitterButtonClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://twitter.com/LegendsofEq",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to open Twitter link");
        }
    }

    private void OnFacebookButtonClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.facebook.com/LegendsOfEquestria",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to open Facebook link");
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

    private async Task LoadChangelog()
    {
        SetChangelogContent("Loading...");

        try
        {
            var cachedChangelog = await LoadCachedChangelogImmediately("Changelog.txt");
            if (cachedChangelog != null)
            {
                FormatAndDisplayChangelog(cachedChangelog);
            }

            _ = Task.Run(async () => {
                try
                {
                    var updatedChangelog = await UpdateCachedChangelog("https://loedata.legendsofequestria.com/data/Changelog.txt", "Changelog.txt");
                    if (updatedChangelog != null)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => {
                            FormatAndDisplayChangelog(updatedChangelog);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Unable to update remote changelog");
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Unable to load cached changelog");
        }
    }

    private async Task<string?> LoadCachedChangelogImmediately(string cacheFileName)
    {
        var cacheDir = Path.Combine(_downloader.LauncherFolder.Path, CacheDirectoryName);
        var cachePath = Path.Combine(cacheDir, cacheFileName);

        if (File.Exists(cachePath))
        {
            try
            {
                return await File.ReadAllTextAsync(cachePath);
            }
            catch
            {
                // ignore 
            }
        }

        return null;
    }

    private async Task<string?> UpdateCachedChangelog(string url, string cacheFileName)
    {
        var cacheDir = Path.Combine(_downloader.LauncherFolder.Path, CacheDirectoryName);
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

            var content = await response.Content.ReadAsStringAsync();
            
            await File.WriteAllTextAsync(tempPath, content);
            File.Move(tempPath, cachePath, true);

            return content;
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

    private void SetChangelogContent(string text)
    {
        _changelogPanel.Children.Clear();
        var textBlock = new TextBlock
        {
            Text = text,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse("#F0FFFFFF")),
            TextWrapping = TextWrapping.Wrap
        };
        _changelogPanel.Children.Add(textBlock);
    }

    private void FormatAndDisplayChangelog(string rawText)
    {
        _changelogPanel.Children.Clear();
        
        var lines = rawText.Split('\n');
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Handle empty lines for spacing
            if (string.IsNullOrEmpty(trimmed))
            {
                _changelogPanel.Children.Add(new Panel { Height = 8 });
                continue;
            }
            
            // Handle headers, which are # with at least one space
            if (trimmed.StartsWith("# "))
            {
                var headerText = trimmed[2..].Trim();
                var headerBlock = new TextBlock
                {
                    Text = headerText,
                    FontSize = 16,
                    FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(Color.Parse("#FFFFFF")),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 4)
                };
                _changelogPanel.Children.Add(headerBlock);
                continue;
            }
            
            // Handle regular content lines
            var contentText = trimmed;
            
            // Add bullet point if not already present
            if (!contentText.StartsWith('•') && !contentText.StartsWith('-') && !contentText.StartsWith('*'))
            {
                contentText = $"• {contentText}";
            }
            else
            {
                // Replace existing bullet types with •
                contentText = contentText.Replace("- ", "• ").Replace("* ", "• ");
            }
            
            var contentBlock = new TextBlock
            {
                Text = contentText,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.Parse("#F0FFFFFF")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 1, 0, 1)
            };
            _changelogPanel.Children.Add(contentBlock);
        }
    }
}
