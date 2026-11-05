using System;
using System.Linq;
using System.Threading.Tasks;
using LoE_Launcher.Core;
using Models.Utils;
using NLog;

namespace LoE_Launcher.Services;

public class GameImportFlow(Downloader downloader, IDialogService dialogs)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static OS Os => PlatformUtils.OperatingSystem;

    private bool CanImport => downloader.State == GameState.NotFound && !downloader.Progress.Processing
                              && !downloader.IsDevChannel;

    public async Task OfferFoundInstall()
    {
        if (!CanImport || DataFolder.IsCustom || downloader.LauncherSettings.ImportOffered)
        {
            return;
        }

        var target = downloader.GameInstallFolder.Path;
        var found = await Task.Run(() => GameImporter.FindInstalls(GameImporter.SearchRoots(Os), Os, target,
            downloader.LauncherFolder.Path));
        Logger.Info($"Found {found.Count} existing game installs");

        var folder = found.FirstOrDefault();
        if (folder == null || !CanImport)
        {
            return;
        }

        var choice = await dialogs.ShowChoiceDialog("Existing Install Found",
            $"Legends of Equestria is already installed at:\n{folder}\n\nMove it to the launcher and download only what's changed?",
            "Use these files", "Download everything");

        if (choice != 0 || await Import(folder))
        {
            downloader.LauncherSettings.ImportOffered = true;
            downloader.SaveSettings();
        }
    }

    private async Task<bool> Import(string source)
    {
        try
        {
            await downloader.ImportInstall(source);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Game import failed");
            await dialogs.ShowErrorMessage("Couldn't Move Game Files",
                $"{ex.Message}\n\nClose the game if it's running and restart the launcher to try again.");
            return false;
        }
        finally
        {
            await Task.Run(downloader.RefreshState);
        }
    }
}
