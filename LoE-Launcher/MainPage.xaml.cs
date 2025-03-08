using System.Diagnostics;
using System.Globalization;
using LoE_Launcher.Core;
using LoE_Launcher.Core.Utils;

namespace LoE_Launcher;

public partial class MainPage : ContentPage
{
    private readonly Downloader _downloader;

    private int _gameState = 0; // 0=Unknown, 1=NotFound, 2=UpdateAvailable, 3=UpToDate, 4=Offline, 5=OutOfDate
    private int _progressCurrent = 0;
    private int _progressMax = 100;
    private string _progressText = "Ready";
    private bool _progressMarquee = false;
    private bool _progressProcessing = false;
    private long _bytesDownloaded = 0;
    
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

        // Load the images from URLs
        LoadBackgroundImages();

        // Set platform information
        lblVersion.Text = $"Launcher Version: 0.5 Platform: {PlatformUtils.OperatingSystem}";

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
                lblDownloadedAmount.Text = $"{_progressText}\n{BytesToString(_bytesDownloaded)} downloaded";
            }

            lblDownloadedAmount.Text = $"{_downloader.Progress.Text}\n{BytesToString(_downloader.BytesDownloaded)} downloaded";

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
        _downloadStopwatch.Restart();

        try
        {
            await Task.Run(() => _downloader.DoInstallation());

            var originalColor = pbState.ProgressColor;
            pbState.ProgressColor = Color.FromArgb("#4CAF50"); // Green
            await Task.Delay(1000);
            pbState.ProgressColor = originalColor;
        }
        finally
        {
            _downloadStopwatch.Stop();
        }
    }

    private void LaunchGame()
    {
        try
        {
            var currentOS = PlatformUtils.OperatingSystem;

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
                        UseShellExecute = PlatformUtils.UseShellExecute
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
                        UseShellExecute = PlatformUtils.UseShellExecute
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
