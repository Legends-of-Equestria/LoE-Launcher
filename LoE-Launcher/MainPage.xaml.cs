using System.Diagnostics;
using System.Globalization;
using LoE_Launcher.Core;
using LoE_Launcher.Core.Utils;
using Platform = LoE_Launcher.Core.Utils.Platform;

namespace LoE_Launcher;

public partial class MainPage : ContentPage
{
    private readonly Downloader _downloader;

    private long _lastBytesDownloaded = 0;
    private DateTime _lastSpeedCheck = DateTime.Now;
    private double _downloadSpeed = 0; // bytes per second
    private TimeSpan _estimatedTimeRemaining = TimeSpan.Zero;
    private bool _showTimeRemaining = false;

    private Queue<double> _speedSamples = new Queue<double>();
    private const int MaxSpeedSamples = 5;
    private TimeSpan _lastReportedTimeRemaining = TimeSpan.Zero;
    private const double TimeRemainingChangeThreshold = 0.25;

    private IDispatcherTimer _timer;
    private Stopwatch _downloadStopwatch = new Stopwatch();

    private readonly Color _downloadColor = Color.FromArgb("#3D85C6"); // Blue
    private readonly Color _updateColor = Color.FromArgb("#FFA500"); // Orange
    private readonly Color _launchColor = Color.FromArgb("#4CAF50"); // Green
    private readonly Color _errorColor = Color.FromArgb("#F44336"); // Red

    public MainPage()
    {
        InitializeComponent();

        // Create your downloader directly - no adapter needed
        _downloader = new Downloader();

        // Hide time remaining initially
        lblTimeRemaining.IsVisible = false;

        // Load the images from URLs
        LoadBackgroundImages();

        // Set platform information
        lblVersion.Text = $"Launcher Version: 0.5 Platform: {Platform.OperatingSystem}";

        // Start the timer for UI updates
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(500);
        _timer.Tick += OnTimerTick;
        _timer.Start();

        // Initially disable button until we know state
        btnAction.Text = "Checking...";
        btnAction.IsEnabled = false;

        // Start initialization process
        InitializeDownloader();
    }

    private async void InitializeDownloader()
    {
        try
        {
            await Task.Run(async () => {
                await _downloader.Cleanup();
                await _downloader.RefreshState();
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Initialization Error", ex.Message, "OK");
        }
    }

    private async void LoadBackgroundImages()
    {
        try
        {
            var backgroundImageSource = ImageSource.FromUri(new Uri("https://i.imgur.com/KMHXf0h.png"));
            backgroundImage.Source = backgroundImageSource;

            var logoImageSource = ImageSource.FromUri(new Uri("https://whatsourlogourlplzanyonereadingthis/w.png"));
            logoImage.Source = logoImageSource;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Image Load Error", $"Failed to load images: {ex.Message}", "OK");
            BackgroundColor = Colors.DarkSlateBlue;
        }
    }

    private void OnTimerTick(object sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() => {
            // Update progress bar
            if (_downloader.Progress.Marquee)
            {
                pbState.Progress = 0;
                pbState.ProgressTo(1, 1500, Easing.Linear);
            }
            else
            {
                var progressPercentage = _downloader.Progress.Max > 0
                    ? (double)_downloader.Progress.Current / _downloader.Progress.Max
                    : 0;
                pbState.Progress = progressPercentage;
            }

            if (_downloader.Progress.Processing && _downloadStopwatch.IsRunning)
            {
                UpdateDownloadStats();
            }

            lblDownloadedAmount.Text = $"{_downloader.Progress.Text}\n{BytesToString(_downloader.BytesDownloaded)} downloaded";

            lblTimeRemaining.IsVisible = _showTimeRemaining;
            if (_showTimeRemaining)
            {
                lblTimeRemaining.Text = $"Time remaining: {FormatTimeRemaining(_estimatedTimeRemaining)}";
            }

            var enabledState = true;

            switch (_downloader.State)
            {
                case GameState.Unknown:
                    btnAction.Text = "Checking...";
                    btnAction.BackgroundColor = _downloadColor;
                    enabledState = false;
                    break;
                case GameState.NotFound:
                    btnAction.Text = "Install";
                    btnAction.BackgroundColor = _downloadColor;
                    enabledState = true;
                    break;
                case GameState.UpdateAvailable:
                    btnAction.Text = "Update";
                    btnAction.BackgroundColor = _updateColor;
                    enabledState = true;
                    break;
                case GameState.UpToDate:
                    btnAction.Text = "Launch";
                    btnAction.BackgroundColor = _launchColor;
                    enabledState = true;
                    break;
                case GameState.Offline:
                    btnAction.Text = "Offline";
                    btnAction.BackgroundColor = _errorColor;
                    enabledState = false;
                    break;
                case GameState.LauncherOutOfDate:
                    btnAction.Text = "Error";
                    btnAction.BackgroundColor = _errorColor;
                    enabledState = false;
                    break;
            }

            if (_downloader.Progress.Processing)
            {
                enabledState = false;
            }

            btnAction.IsEnabled = enabledState;
        });
    }

    private void UpdateDownloadStats()
    {
        if (_downloader.BytesDownloaded <= 0)
        {
            return;
        }

        var currentTime = DateTime.Now;
        var elapsedSeconds = (currentTime - _lastSpeedCheck).TotalSeconds;

        if (elapsedSeconds >= 0.5)
        {
            var bytesDelta = _downloader.BytesDownloaded - _lastBytesDownloaded;

            if (bytesDelta > 0 && elapsedSeconds > 0)
            {
                var instantSpeed = bytesDelta / elapsedSeconds;

                _speedSamples.Enqueue(instantSpeed);
                if (_speedSamples.Count > MaxSpeedSamples)
                {
                    _speedSamples.Dequeue();
                }

                _downloadSpeed = _speedSamples.Average();
            }

            _lastBytesDownloaded = _downloader.BytesDownloaded;
            _lastSpeedCheck = currentTime;

            if (_downloadSpeed > 0 && _downloader.Progress is { Current: > 0, Max: > 0 })
            {
                var progressFraction = (double)_downloader.Progress.Current / _downloader.Progress.Max;
                var estimatedTotalBytes = _downloader.BytesDownloaded / progressFraction;
                var bytesRemaining = Math.Max(0, estimatedTotalBytes - _downloader.BytesDownloaded);

                var secondsRemaining = bytesRemaining / _downloadSpeed;
                var newEstimate = TimeSpan.FromSeconds(secondsRemaining);

                // Apply hysteresis/smoothing to prevent wild jumps
                if (_lastReportedTimeRemaining == TimeSpan.Zero ||
                    Math.Abs(1 - (newEstimate.TotalSeconds / Math.Max(1, _lastReportedTimeRemaining.TotalSeconds))) > TimeRemainingChangeThreshold)
                {
                    // Only update if the change is significant
                    _estimatedTimeRemaining = TimeSpan.FromSeconds(Math.Round(secondsRemaining));
                    _lastReportedTimeRemaining = _estimatedTimeRemaining;
                }
            }
            else if (_downloadSpeed <= 0 || _downloader.Progress.Current <= 0)
            {
                // Avoid displaying "NaN" or weird estimates by reverting to "calculating..."
                _estimatedTimeRemaining = TimeSpan.Zero;
            }
        }
    }

    private static string FormatTimeRemaining(TimeSpan timeSpan)
    {
        if (timeSpan == TimeSpan.Zero || timeSpan.TotalSeconds < 0)
        {
            return "calculating...";
        }

        if (timeSpan.TotalHours >= 1)
        {
            // Round to nearest minute when in hours
            var hours = (int)timeSpan.TotalHours;
            var minutes = (int)Math.Round(timeSpan.Minutes / 5.0) * 5; // Round to nearest 5 minutes
            return $"about {hours}h {(minutes > 0 ? $"{minutes}m" : "")}";
        }
        else if (timeSpan.TotalMinutes >= 1)
        {
            if (timeSpan.TotalMinutes > 10)
            {
                return $"about {(int)timeSpan.TotalMinutes}m";
            }
            else
            {
                return $"{(int)timeSpan.TotalMinutes}m {timeSpan.Seconds}s";
            }
        }
        else if (timeSpan.TotalSeconds >= 5)
        {
            return $"{(int)timeSpan.TotalSeconds}s";
        }
        else
        {
            return "almost done";
        }
    }

    private async void OnActionButtonClicked(object sender, EventArgs e)
    {
        // Disable button to prevent multiple clicks
        btnAction.IsEnabled = false;

        // Button press animation
        await btnAction.ScaleTo(0.95, 100, Easing.CubicOut);
        await btnAction.ScaleTo(1, 100, Easing.CubicIn);

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
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            // Button will be re-enabled by the timer based on state
        }
    }

    private async Task InstallOrUpdateGame()
    {
        _showTimeRemaining = true;
        _downloadStopwatch.Restart();
        _lastBytesDownloaded = 0;
        _lastSpeedCheck = DateTime.Now;

        try
        {
            await Task.Run(() => _downloader.DoInstallation());

            // Completion indication
            var originalColor = pbState.ProgressColor;
            pbState.ProgressColor = Color.FromArgb("#4CAF50"); // Green
            await Task.Delay(1000);
            pbState.ProgressColor = originalColor;
        }
        finally
        {
            _downloadStopwatch.Stop();
            _showTimeRemaining = false;
        }
    }

    private void LaunchGame()
    {
        try
        {
            var currentOS = Platform.OperatingSystem;

            switch (currentOS)
            {
                case OS.WindowsX64:
                case OS.WindowsX86:
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.Combine(_downloader.GameInstallFolder.Path, "loe.exe"),
                        UseShellExecute = Platform.UseShellExecute
                    });
                    break;

                case OS.MacIntel:
                case OS.MacArm:
                    var macAppPath = Path.Combine(_downloader.GameInstallFolder.Path, "LoE.app");
                    // Set execute permissions
                    var permissionProcess = new Process();
                    permissionProcess.RunInlineAndWait(new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"-R 777 {macAppPath}",
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Minimized
                    });

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = macAppPath,
                        UseShellExecute = Platform.UseShellExecute
                    });
                    break;

                case OS.X11:
                    var is64Bit = Environment.Is64BitProcess;
                    var linuxExePath = Path.Combine(_downloader.GameInstallFolder.Path, $"LoE.x86{(is64Bit ? "_64" : "")}");
                    // Set execute permissions
                    var linuxPermProcess = new Process();
                    linuxPermProcess.RunInlineAndWait(new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"-R 777 {linuxExePath}",
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Minimized
                    });

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = linuxExePath,
                        UseShellExecute = Platform.UseShellExecute
                    });
                    break;

                default:
                    throw new PlatformNotSupportedException("This platform is not supported.");
            }
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(async () => {
                await DisplayAlert("Launch Error", ex.Message, "OK");
            });
        }
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
