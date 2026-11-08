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

        var other = downloader.OtherChannelGameFolder;
        var folder = found.FirstOrDefault(f => string.IsNullOrEmpty(other)
                                               || !(GameFolderRules.IsSameOrInside(f, other, Os)
                                                    || GameFolderRules.IsSameOrInside(other, f, Os)));
        if (folder == null || !CanImport)
        {
            return;
        }

        try
        {
            var choice = await dialogs.ShowChoiceDialog("Existing Install Found",
                $"Legends of Equestria is already installed at:\n{folder}\n\nUse it?",
                "Use it", "Not now");

            if (choice == 0)
            {
                downloader.LauncherSettings.GameFolder = folder;
                downloader.SaveSettings();
                await Task.Run(downloader.RefreshState);
            }

            downloader.LauncherSettings.ImportOffered = true;
            downloader.SaveSettings();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Couldn't use the existing install");
            await dialogs.ShowErrorMessage("Couldn't Use the Existing Install", ex.Message);
        }
    }
}
