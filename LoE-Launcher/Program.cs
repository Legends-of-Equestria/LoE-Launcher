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
            Layout = "${date:format=yyyy-MM-dd HH:mm:ss.fff} [${level:uppercase=true}] ${logger:shortName=true} - ${message} ${exception:format=tostring}",
            ArchiveFileName = Path.Combine(GetLogDirectory(), "LoE-Launcher.{#}.log"),
            ArchiveNumbering = ArchiveNumberingMode.Date,
            ArchiveEvery = FileArchivePeriod.Day,
            MaxArchiveFiles = 7
        };
        
        // Detailed log file with Debug level and above
        var detailedLogFile = new FileTarget("detailedlogfile")
        {
            FileName = Path.Combine(GetLogDirectory(), "LoE-Launcher-Debug.log"),
            Layout = "${date:format=yyyy-MM-dd HH:mm:ss.fff} [${level:uppercase=true}] ${logger:shortName=true} - ${message}${newline}  Call: ${callsite:className=true:methodName=true:fileName=false} ${exception:format=tostring}",
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
            Layout = "${date:format=yyyy-MM-dd HH:mm:ss.fff} [${level:uppercase=true}] ${message} ${exception:format=tostring}",
            ArchiveFileName = Path.Combine(GetLogDirectory(), "LoE-Launcher-Network.{#}.log"),
            ArchiveNumbering = ArchiveNumberingMode.Date,
            ArchiveEvery = FileArchivePeriod.Day,
            MaxArchiveFiles = 3
        };

        var logConsole = new ColoredConsoleTarget("console")
        {
            Layout = "${time} ${level:uppercase=true} ${logger:shortName=true} ${message} ${exception:format=tostring}",
            UseDefaultRowHighlightingRules = false,
            RowHighlightingRules = {
                new ConsoleRowHighlightingRule("level == LogLevel.Trace", ConsoleOutputColor.DarkGray, ConsoleOutputColor.NoChange),
                new ConsoleRowHighlightingRule("level == LogLevel.Debug", ConsoleOutputColor.Gray, ConsoleOutputColor.NoChange),
                new ConsoleRowHighlightingRule("level == LogLevel.Info", ConsoleOutputColor.White, ConsoleOutputColor.NoChange),
                new ConsoleRowHighlightingRule("level == LogLevel.Warn", ConsoleOutputColor.Yellow, ConsoleOutputColor.NoChange),
                new ConsoleRowHighlightingRule("level == LogLevel.Error", ConsoleOutputColor.Red, ConsoleOutputColor.NoChange),
                new ConsoleRowHighlightingRule("level == LogLevel.Fatal", ConsoleOutputColor.White, ConsoleOutputColor.DarkRed)
            }
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
        logger.Info("===============================================");
        logger.Info("LoE Launcher - NEW SESSION STARTED");
        logger.Info("===============================================");
        logger.Info($"Log Directory: {GetLogDirectory()}");
        logger.Info("Log Files:");
        logger.Info("  - LoE-Launcher.log (Main)");
        logger.Info("  - LoE-Launcher-Debug.log (Detailed)");
        logger.Info("  - LoE-Launcher-Network.log (Network)");
    }

    private static string GetLogDirectory()
    {
        var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Launcher Logs");
            
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }
            
        return logDirectory;
    }
}