using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LoE_Launcher.Core;
using LoE_Launcher.Services;
using LoE_Launcher.Utils;
using Models.Utils;
using NLog;
using Path = System.IO.Path;

namespace LoE_Launcher;

public partial class MainWindow : Window
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Downloader _downloader;
    private readonly DialogService _dialogService;
    private readonly CacheManager _cacheManager;

    private DispatcherTimer _timer;
    private Stopwatch _downloadStopwatch = new();

    private Image _backgroundImage;
    private Image _logoImage;
    private ProgressBar _pbState;
    private Button _btnAction;
    private StackPanel _changelogPanel;
    private Grid _progressOverlay;
    private TextBlock _progressPercentage;
    private TextBlock _downloadSpeed;
    private TextBlock _downloadStatus;
    private Rectangle _titleBarArea;

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
    private bool _actionButtonClicked;

    public MainWindow()
    {
        Logger.Info("Starting LoE Launcher");

        _downloadColor = BrushFactory.CreateHorizontalGradient("#9C69B5", "#D686D2");
        _updateColor = BrushFactory.CreateHorizontalGradient("#D69D45", "#E8B75C");
        _launchColor = BrushFactory.CreateHorizontalGradient("#9C69B5", "#D686D2");
        _errorColor = BrushFactory.CreateHorizontalGradient("#D32F2F", "#F44336");

        InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        _downloader = new Downloader();
        _dialogService = new DialogService(this);
        _cacheManager = new CacheManager(Path.Combine(_downloader.LauncherFolder.Path, Constants.CacheDirectoryName));

        _ = LoadBackgroundImages();
        _ = LoadChangelog();
        SetupDraggableLogo();
        SetupTitleBarDrag();

        Logger.Info($"Running on platform: {PlatformUtils.OperatingSystem}");

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        _pbState.IsVisible = true;
        _btnAction.IsVisible = false;
        _progressOverlay.IsVisible = true;

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
        _titleBarArea = this.FindControl<Rectangle>("titleBarArea")!;
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

    private void SetupTitleBarDrag()
    {
        _titleBarArea.PointerPressed += OnTitleBarPointerPressed;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(_titleBarArea).Properties.IsLeftButtonPressed)
        {
            this.BeginMoveDrag(e);
        }
    }

    private void OnLogoPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(_logoImage).Properties.IsLeftButtonPressed)
        {
            _isDraggingLogo = true;
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

        _logoRotateTransform.Angle += _logoAngularVelocity * deltaTime;

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
            
            finalTimer.Tick += (_, _) =>
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
                await _dialogService.ShowErrorMessage("Connection Error",
                    "Cannot connect to the game servers. Check your internet connection and try again.");
            }
            else if (_downloader.State == GameState.ServerMaintenance)
            {
                Logger.Warn("Game servers are under maintenance or temporarily unavailable.");
                await _dialogService.ShowErrorMessage("Server Maintenance",
                    "The game servers are currently under maintenance or temporarily unavailable. Please try again in a few hours.");
            }

            _downloadStopwatch.Stop();
            Logger.Info($"Initialization took {_downloadStopwatch.Elapsed.TotalSeconds:F1} seconds");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize downloader");
            await _dialogService.ShowErrorMessage("Initialization Error", ex.Message);
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
            var cachedImage = await _cacheManager.LoadCachedImageImmediately(Constants.BackgroundImageFileName);
            if (cachedImage != null)
            {
                _backgroundImage.Source = cachedImage;
            }

            _ = Task.Run(async () => {
                try
                {
                    var updatedImage = await _cacheManager.UpdateCachedImage(Constants.BackgroundImageUrl, Constants.BackgroundImageFileName);
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

    private void OnTimerTick(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() => {
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
                    _btnAction.Content = "Validating...";
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
                case GameState.ServerMaintenance:
                    _btnAction.Content = "Maintenance";
                    _btnAction.Background = _updateColor;
                    enabledState = false;
                    break;
                case GameState.LauncherOutOfDate:
                    _btnAction.Content = "Update Required";
                    _btnAction.Background = _errorColor;
                    enabledState = true;
                    break;
            }

            if (_downloader.Progress.Processing)
            {
                enabledState = false;
            }

            var showProgressLayout = _downloader.Progress.Processing || _downloader.State == GameState.Unknown;
            _pbState.IsVisible = showProgressLayout;
            _btnAction.IsVisible = !showProgressLayout;
            _btnAction.IsEnabled = enabledState && !_actionButtonClicked;

            if (showProgressLayout)
            {
                _progressOverlay.IsVisible = true;
                
                var progressPercentage = _downloader.Progress.Max > 0
                    ? (double)_downloader.Progress.Current / _downloader.Progress.Max
                    : 0;

                if (_downloader.State == GameState.Unknown && !_downloader.Progress.Processing)
                {
                    _progressPercentage.Text = "0%";
                }
                else
                {
                    _progressPercentage.Text = $"{Math.Round(progressPercentage * 100)}%";
                }
                
                if (_downloader.DownloadStats.HasValidSpeed)
                {
                    _downloadSpeed.Text = _downloader.DownloadStats.CurrentSpeed;
                }
                else
                {
                    _downloadSpeed.Text = "";
                }

                if (_downloader.State == GameState.Unknown)
                {
                    _downloadStatus.Text = "Validating";
                }
                else
                {
                    _downloadStatus.Text = statusText;
                }
            }
            else
            {
                _progressOverlay.IsVisible = false;
            }
        });
    }

    private async void OnActionButtonClicked(object sender, RoutedEventArgs e)
    {
        if (!_btnAction.IsEnabled)
        {
            return;
        }
            
        _actionButtonClicked = true;
        _btnAction.IsEnabled = false;

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
                case GameState.LauncherOutOfDate:
                    await _dialogService.ShowLauncherUpdateDialog();
                    break;
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorMessage("Error", ex.Message);
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
            _actionButtonClicked = false;
        }
    }

    private async void LaunchGame()
    {
        try
        {
            ProcessLauncher.LaunchGame(_downloader.GameInstallFolder.Path);

            await Task.Delay(1500);

            if (_downloader.LauncherSettings.CloseAfterLaunch)
            {
                Logger.Info("Closing launcher after game launch");
                this.Close();
            }
            else
            {
                Logger.Info("Keeping launcher open after game launch");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to launch game");
            await _dialogService.ShowErrorMessage("Launch Error", ex.Message);
        }
    }

    private async void OnSettingsButtonClicked(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_downloader, _dialogService, _pbState, _launchColor, _downloadStopwatch);
        await settingsWindow.ShowDialog(this);
    }
    
    private void OnYoutubeButtonClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            ProcessLauncher.LaunchUrl(Constants.YouTubeUrl);
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
            ProcessLauncher.LaunchUrl(Constants.DiscordUrl);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to open Discord link");
        }
    }

    private void OnXButtonClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            ProcessLauncher.LaunchUrl(Constants.XUrl);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to open X link");
        }
    }

    private void OnFacebookButtonClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            ProcessLauncher.LaunchUrl(Constants.FacebookUrl);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to open Facebook link");
        }
    }

    private async Task LoadChangelog()
    {
        ChangelogFormatter.SetChangelogContent(_changelogPanel, "Loading...");

        try
        {
            var cachedChangelog = await _cacheManager.LoadCachedTextImmediately(Constants.ChangelogFileName);
            if (cachedChangelog != null)
            {
                ChangelogFormatter.FormatAndDisplayChangelog(_changelogPanel, cachedChangelog);
            }

            _ = Task.Run(async () => {
                try
                {
                    var updatedChangelog = await _cacheManager.UpdateCachedText(Constants.ChangelogUrl, Constants.ChangelogFileName);
                    if (updatedChangelog != null)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => {
                            ChangelogFormatter.FormatAndDisplayChangelog(_changelogPanel, updatedChangelog);
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
}
