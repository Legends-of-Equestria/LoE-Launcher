using System.Collections.Generic;
using LoE_Launcher.Services;

namespace LoE_Launcher.Core;

internal static class PendingMoveRecovery
{
    public static bool Run(string target, IReadOnlyList<string>? listedFiles)
    {
        if (listedFiles == null)
        {
            return false;
        }

        GameFiles.Delete(target, listedFiles);
        return true;
    }
}
