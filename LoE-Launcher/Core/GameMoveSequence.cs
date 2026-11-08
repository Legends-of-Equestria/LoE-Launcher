using System;
using NLog;

namespace LoE_Launcher.Core;

internal static class GameMoveSequence
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static void Run(Settings settings, string target, string? newActive, Action save, Action copy, Action deleteOld)
    {
        settings.PendingMoveTarget = target;
        try
        {
            save();
        }
        catch
        {
            settings.PendingMoveTarget = null;
            throw;
        }

        try
        {
            copy();
        }
        catch
        {
            settings.PendingMoveTarget = null;
            try
            {
                save();
            }
            catch (Exception ex)
            {
                Logger.Warn($"Couldn't clear the pending move: {ex.Message}");
            }

            throw;
        }

        var previous = settings.ActiveCustomGameFolder;
        settings.ActiveCustomGameFolder = newActive;
        settings.PendingMoveTarget = null;
        try
        {
            save();
        }
        catch
        {
            settings.ActiveCustomGameFolder = previous;
            settings.PendingMoveTarget = target;
            throw;
        }

        deleteOld();
    }
}
