using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using LoE_Launcher.Core;
using LoE_Launcher.Utils;
using Models.Utils;
using NLog;

namespace LoE_Launcher.Services;

public enum LocationStep { Nothing, Refuse, Switch, Move, UseInPlace }

internal static class LocationPlan
{
    public static LocationStep Decide(LocationChoice choice, bool hasGame) => choice switch
    {
        LocationChoice.Refused => LocationStep.Refuse,
        LocationChoice.Unchanged => LocationStep.Nothing,
        _ when !hasGame => LocationStep.Switch,
        LocationChoice.NewFolder => LocationStep.Move,
        LocationChoice.UseInPlace => LocationStep.UseInPlace,
        _ => LocationStep.Nothing
    };

    public static bool HasGame(GameState state, string folder) =>
        state is not (GameState.NotFound or GameState.FolderMissing)
        && Directory.Exists(folder)
        && Directory.EnumerateFileSystemEntries(folder).Any();
}

public class GameLocationFlow(Downloader downloader, IDialogService dialogs, IWindowProvider windows)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const string CantUse = "Can't Use That Folder";

    private static OS Os => PlatformUtils.OperatingSystem;

    private bool _running;

    public async Task ChooseFolder(Action? closePanel = null)
    {
        var window = windows.GetMainWindow();
        if (window == null)
        {
            return;
        }

        string? picked;
        try
        {
            var storage = window.StorageProvider;
            var start = await storage.TryGetFolderFromPathAsync(NearestExisting(downloader.GameInstallFolder.Path));
            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose Game Folder",
                AllowMultiple = false,
                SuggestedStartLocation = start
            });
            picked = folders.FirstOrDefault()?.TryGetLocalPath();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Folder picker failed");
            await dialogs.ShowErrorMessage(CantUse, ex.Message);
            return;
        }

        if (picked != null)
        {
            await Apply(picked, closePanel);
        }
    }

    public Task Apply(string picked, Action? closePanel = null) =>
        Run((current, files) => GameLocation.Resolve(picked, new LocationContext(current,
            downloader.OtherChannelGameFolder, files is { Count: > 0 } ? files.Max(f => f.Length) : 0,
            KnownFolders.ForCurrentUser(Os), Os)), closePanel);

    public Task ResetToDefault(Action? closePanel = null) =>
        Run((current, _) => GameLocation.ResolveDefault(downloader.DefaultGameFolder, current,
            downloader.OtherChannelGameFolder, Os), closePanel);

    public async Task OfferOldCopyDeletion()
    {
        var settings = downloader.LauncherSettings;
        var old = settings.PendingOldCopy;
        if (old == null)
        {
            return;
        }

        try
        {
            if (!Directory.Exists(old)
                || !GameLocation.IsSeparateCopy(old, downloader.GameInstallFolder.Path, downloader.OtherChannelGameFolder, Os))
            {
                Logger.Info($"Older copy at {old} is gone or in use, not asking about it");
                ForgetOldCopy();
                return;
            }

            var files = await Task.Run(() => downloader.ListedGameFiles(old));
            if (files == null)
            {
                Logger.Info("No game file list yet, asking about the older copy later");
                return;
            }

            var size = await Task.Run(() => GameFiles.Size(old, files));
            var choice = await dialogs.ShowChoiceDialog("Older Copy Found",
                $"You still have an older copy at {old} ({SizeText.Format(size)}). Delete it to free up space?",
                "Keep it", "Delete");

            if (choice == 1)
            {
                var deleted = await Task.Run(() => GameFiles.Delete(old, files));
                Logger.Info($"Removed {deleted} files from the older copy at {old}");
            }

            ForgetOldCopy();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Couldn't handle the older copy");
        }
    }

    private void ForgetOldCopy()
    {
        downloader.LauncherSettings.PendingOldCopy = null;
        downloader.SaveSettings();
    }

    private async Task Run(Func<string, IReadOnlyList<string>?, LocationChoice> resolve, Action? closePanel)
    {
        if (_running || downloader.Progress.Processing)
        {
            return;
        }

        _running = true;
        var moving = false;
        try
        {
            if (PlatformUtils.IsGameRunning(Os))
            {
                await dialogs.ShowErrorMessage("Can't Move the Game", "Close the game first.");
                return;
            }

            var current = downloader.GameInstallFolder.Path;
            var files = await Task.Run(() => downloader.ListedGameFiles(current));
            var choice = await Task.Run(() => resolve(current, files));
            var hasGame = await Task.Run(() => LocationPlan.HasGame(downloader.State, current));
            Logger.Info($"Game folder choice: {choice}, game installed: {hasGame}");

            if (hasGame && files == null && choice is not (LocationChoice.Refused or LocationChoice.Unchanged))
            {
                Logger.Warn("No game file list yet, not changing the game folder");
                return;
            }

            switch (LocationPlan.Decide(choice, hasGame))
            {
                case LocationStep.Refuse:
                    await dialogs.ShowErrorMessage(CantUse, ((LocationChoice.Refused)choice).Message);
                    break;
                case LocationStep.Nothing:
                    ClearDefaultSetting(current);
                    break;
                case LocationStep.Switch:
                    await SwitchEmpty(FolderOf(choice));
                    break;
                case LocationStep.UseInPlace:
                    await UseInPlace(FolderOf(choice), current);
                    break;
                case LocationStep.Move:
                    var target = FolderOf(choice);
                    var size = await Task.Run(() => GameFiles.Size(current, files!));
                    if (!await Task.Run(() => GameMover.HasSpaceFor(target, size)))
                    {
                        await dialogs.ShowErrorMessage(CantUse, "There isn't enough free space in that folder.");
                        return;
                    }

                    var confirm = await dialogs.ShowChoiceDialog("Move Game",
                        $"Move Legends of Equestria to {target}? {SizeText.Format(size)}, this can take a few minutes.",
                        "Move", "Cancel");
                    if (confirm != 0)
                    {
                        return;
                    }

                    closePanel?.Invoke();
                    moving = true;
                    try
                    {
                        await downloader.MoveGameFolder(target, files!);
                    }
                    finally
                    {
                        await Task.Run(downloader.RefreshState);
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, moving ? "Moving the game failed" : "Couldn't use the chosen game folder");
            await dialogs.ShowErrorMessage(moving ? "Couldn't Move the Game" : CantUse, ex.Message);
        }
        finally
        {
            _running = false;
        }
    }

    private async Task SwitchEmpty(string folder)
    {
        if (!Directory.Exists(folder))
        {
            var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)));
            if (parent == null || !Directory.Exists(parent))
            {
                await dialogs.ShowErrorMessage(CantUse, GameLocation.NotWritable);
                return;
            }

            Directory.CreateDirectory(folder);
        }

        Logger.Info($"Game folder set to {folder}");
        downloader.LauncherSettings.ActiveCustomGameFolder = downloader.CustomFolderSetting(folder);
        downloader.SaveSettings();
        await Task.Run(downloader.RefreshState);
    }

    private async Task UseInPlace(string folder, string current)
    {
        Logger.Info($"Using the game at {folder}, keeping {current} as the older copy");
        var settings = downloader.LauncherSettings;
        settings.PendingOldCopy = current;
        settings.ActiveCustomGameFolder = downloader.CustomFolderSetting(folder);
        downloader.SaveSettings();
        await Task.Run(downloader.RefreshState);
    }

    private void ClearDefaultSetting(string current)
    {
        var settings = downloader.LauncherSettings;
        if (settings.ActiveCustomGameFolder != null && downloader.CustomFolderSetting(current) == null)
        {
            settings.ActiveCustomGameFolder = null;
            downloader.SaveSettings();
        }
    }

    private static string FolderOf(LocationChoice choice) => choice switch
    {
        LocationChoice.NewFolder n => n.Folder,
        LocationChoice.UseInPlace u => u.Folder,
        _ => throw new ArgumentOutOfRangeException(nameof(choice))
    };

    private static string NearestExisting(string path)
    {
        var current = path;
        while (!Directory.Exists(current) && Path.GetDirectoryName(current) is { } parent)
        {
            current = parent;
        }

        return current;
    }
}
