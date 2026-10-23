using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoE_Launcher.Core;
using LoE_Launcher.Services;
using NLog;
using System;
using System.Threading.Tasks;

namespace LoE_Launcher.ViewModels;

public partial class TesterAccessViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Downloader _downloader;
    private readonly IDialogService _dialogService;

    // Only tokens from a successful login end up here
    private string _pendingToken;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLogin), nameof(ShowChannel))]
    private bool _hasTesterAccess;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLogin), nameof(ShowChannel))]
    private bool _loginExpired;

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _loginStatus = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LogInCommand))]
    private bool _isLoggingIn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseLiveChannel))]
    private bool _useDevChannel;

    public bool ShowLogin => !HasTesterAccess || LoginExpired;
    public bool ShowChannel => !ShowLogin;

    public bool UseLiveChannel
    {
        get => !UseDevChannel;
        set => UseDevChannel = !value;
    }

    public Action? CloseAction { get; set; }

    public TesterAccessViewModel(Downloader downloader, IDialogService dialogService)
    {
        _downloader = downloader;
        _dialogService = dialogService;
        _pendingToken = _downloader.LauncherSettings.DevToken;
        _hasTesterAccess = !string.IsNullOrWhiteSpace(_pendingToken);
        _useDevChannel = _hasTesterAccess && _downloader.LauncherSettings.Channel == GameChannel.Dev;

        if (_hasTesterAccess && _downloader.State == GameState.DevAccessDenied)
        {
            _loginExpired = true;
            _loginStatus = "Your tester login expired, log in again";
        }
    }

    [RelayCommand(CanExecute = nameof(CanLogIn))]
    private async Task LogIn()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            LoginStatus = "Enter your forum username and password";
            return;
        }

        IsLoggingIn = true;
        LoginStatus = "Logging in...";
        try
        {
            var result = await _downloader.LogInForDevToken(Username, Password);
            Password = "";
            switch (result.Status)
            {
                case DevLoginStatus.Ok:
                    _pendingToken = result.Token;
                    // They logged in to get dev builds, so start there
                    if (!LoginExpired)
                    {
                        UseDevChannel = true;
                    }
                    HasTesterAccess = true;
                    LoginExpired = false;
                    LoginStatus = "";
                    break;
                case DevLoginStatus.WrongLogin:
                    LoginStatus = "Wrong username or password";
                    break;
                case DevLoginStatus.NoAccess:
                    LoginStatus = "This account doesn't have tester access";
                    break;
                case DevLoginStatus.TooManyAttempts:
                    LoginStatus = "Too many attempts, wait a minute";
                    break;
                default:
                    LoginStatus = "Couldn't reach the server, try again later";
                    break;
            }
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    private bool CanLogIn() => !IsLoggingIn;

    [RelayCommand]
    private void RemoveTesterAccess()
    {
        _pendingToken = "";
        UseDevChannel = false;
        LoginExpired = false;
        HasTesterAccess = false;
    }

    [RelayCommand]
    private void Done() => CloseAction?.Invoke();

    private GameChannel PendingChannel => UseDevChannel && HasTesterAccess ? GameChannel.Dev : GameChannel.Live;

    // Applied when the popup closes so switching back and forth doesn't refresh each time
    private bool ApplyChannelChanges()
    {
        var settings = _downloader.LauncherSettings;
        var newToken = _pendingToken;
        var newChannel = PendingChannel;

        if (settings.DevToken == newToken && settings.Channel == newChannel)
        {
            return false;
        }

        var wasDev = settings.IsDevChannelActive;
        var oldToken = settings.DevToken;

        settings.DevToken = newToken;
        settings.Channel = newChannel;
        _downloader.SaveSettings();

        Logger.Info($"Channel settings changed. Dev active: {settings.IsDevChannelActive}");
        return wasDev != settings.IsDevChannelActive || (settings.IsDevChannelActive && oldToken != newToken);
    }

    public async Task ApplyChannelChangesAsync()
    {
        if (_downloader.Progress.Processing)
        {
            var settings = _downloader.LauncherSettings;
            if (_pendingToken != settings.DevToken || PendingChannel != settings.Channel)
            {
                await _dialogService.ShowErrorMessage("Channel Not Changed",
                    "Wait for the current download to finish before switching channels.");
            }

            return;
        }

        if (ApplyChannelChanges())
        {
            await Task.Run(() => _downloader.RefreshState());
        }
    }
}
