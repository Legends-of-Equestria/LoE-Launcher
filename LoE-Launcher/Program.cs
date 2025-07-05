using Avalonia;
using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.IO;

namespace LoE_Launcher;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        ConfigureLogging();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static void ConfigureLogging()
    {
        var config = new LoggingConfiguration();

        // Regular log file with Info level and above
        var logFile = new FileTarget("logfile")
        {
            FileName = Path.Combine(GetLogDirectory(), "LoE-Launcher.log"),
            Layout = "${longdate}|${level:uppercase=true}|${logger}|${message} ${exception:format=tostring}",
            ArchiveFileName = Path.Combine(GetLogDirectory(), "LoE-Launcher.{#}.log"),
            ArchiveNumbering = ArchiveNumberingMode.Date,
            ArchiveEvery = FileArchivePeriod.Day,
            MaxArchiveFiles = 7
        };
        
        // Detailed log file with Debug level and above
        var detailedLogFile = new FileTarget("detailedlogfile")
        {
            FileName = Path.Combine(GetLogDirectory(), "LoE-Launcher-Debug.log"),
            Layout = "${longdate}|${level:uppercase=true}|${logger}|${callsite:className=true:methodName=true:fileName=true:includeSourcePath=true}|${message} ${exception:format=tostring}",
            ArchiveFileName = Path.Combine(GetLogDirectory(), "LoE-Launcher-Debug.{#}.log"),
            ArchiveNumbering = ArchiveNumberingMode.Date,
            ArchiveEvery = FileArchivePeriod.Day,
            MaxArchiveFiles = 3,
            EnableFileDelete = true
        };

        // Special log file just for network operations to help troubleshoot connection issues
        var networkLogFile = new FileTarget("networklogfile")
        {
            FileName = Path.Combine(GetLogDirectory(), "LoE-Launcher-Network.log"),
            Layout = "${longdate}|${level:uppercase=true}|${logger}|${message} ${exception:format=tostring}",
            ArchiveFileName = Path.Combine(GetLogDirectory(), "LoE-Launcher-Network.{#}.log"),
            ArchiveNumbering = ArchiveNumberingMode.Date,
            ArchiveEvery = FileArchivePeriod.Day,
            MaxArchiveFiles = 3
        };

        var logConsole = new ConsoleTarget("console")
        {
            Layout = "${longdate}|${level:uppercase=true}|${logger}|${message} ${exception:format=tostring}"
        };

        config.AddTarget(logFile);
        config.AddTarget(detailedLogFile);
        config.AddTarget(networkLogFile);
        config.AddTarget(logConsole);

        config.AddRule(LogLevel.Info, LogLevel.Fatal, logFile);
        config.AddRule(LogLevel.Debug, LogLevel.Fatal, detailedLogFile);
        config.AddRule(LogLevel.Debug, LogLevel.Fatal, networkLogFile, "LoE_Launcher.Core.Downloader");
        config.AddRule(LogLevel.Info, LogLevel.Fatal, logConsole);

        LogManager.Configuration = config;
        
        var logger = LogManager.GetCurrentClassLogger();
        logger.Info("----------------- NEW SESSION STARTED -----------------");
        logger.Info($"Log files are located at: {GetLogDirectory()}");
        logger.Info($"Main log: LoE-Launcher.log");
        logger.Info($"Debug log: LoE-Launcher-Debug.log");
        logger.Info($"Network log: LoE-Launcher-Network.log");
    }

    private static string GetLogDirectory()
    {
        var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }
            
        return logDirectory;
    }
}