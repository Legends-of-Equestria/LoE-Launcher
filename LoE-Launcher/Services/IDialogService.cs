using System.Threading.Tasks;

namespace LoE_Launcher.Services;

public interface IDialogService
{
    Task<bool> ShowConfirmDialog(string title, string message);
    Task ShowInfoMessage(string title, string message);
    Task ShowErrorMessage(string title, string message);
    Task ShowGameLogsNotFoundDialog();
    Task ShowLauncherUpdateDialog();
    Task ShowSettingsWindowAsync();
}
