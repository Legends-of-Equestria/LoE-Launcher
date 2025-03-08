using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace LoE_Launcher;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Configure NLog
        ConfigureLogging();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void ConfigureLogging()
    {
        var config = new LoggingConfiguration();

        var logFile = new FileTarget("logfile")
        {
            FileName = Path.Combine(GetLogDirectory(), "LoE-Launcher.log"),
            Layout = "${longdate}|${level:uppercase=true}|${logger}|${message} ${exception:format=tostring}",
            ArchiveFileName = Path.Combine(GetLogDirectory(), "LoE-Launcher.{#}.log"),
            ArchiveNumbering = ArchiveNumberingMode.Date,
            ArchiveEvery = FileArchivePeriod.Day,
            MaxArchiveFiles = 7
        };

        var logConsole = new ConsoleTarget("console")
        {
            Layout = "${longdate}|${level:uppercase=true}|${logger}|${message} ${exception:format=tostring}"
        };

        config.AddTarget(logFile);
        config.AddTarget(logConsole);

        config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, logFile);
        config.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, logConsole);

        LogManager.Configuration = config;
    }

    private static string GetLogDirectory()
    {
        var logDirectory = Path.Combine(FileSystem.AppDataDirectory, "Logs");
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }
        
        return logDirectory;
    }
}