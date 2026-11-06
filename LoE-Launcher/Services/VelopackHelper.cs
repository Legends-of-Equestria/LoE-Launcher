using System;
using NLog;
using Velopack;
using Velopack.Sources;

namespace LoE_Launcher.Services;

public static class VelopackHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const string GitHubRepoUrl = "https://github.com/Legends-of-Equestria/LoE-Launcher";

    public static UpdateManager? CreateUpdateManager()
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(GitHubRepoUrl, null, false));
            Logger.Info($"Velopack installed version: {manager.CurrentVersion?.ToString() ?? "not installed"}");
            return manager;
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to create UpdateManager, expected outside a Velopack install");
            return null;
        }
    }
}
