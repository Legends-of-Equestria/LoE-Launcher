using System;
using System.IO;
using System.Runtime.InteropServices;

public static class UnityPlayerLogHelper
{
    public static string GetPlayerLogPath(string companyName, string productName)
    {
        string basePath;
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", companyName, productName, "Player.log"
            );
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Logs", companyName, productName, "Player.log"
            );
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "unity3d", companyName, productName, "Player.log"
            );
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported platform");
        }
        
        return basePath;
    }
    
    public static bool PlayerLogExists(string companyName, string productName)
    {
        string logPath = GetPlayerLogPath(companyName, productName);
        return File.Exists(logPath);
    }
    
    public static string ReadPlayerLog(string companyName, string productName)
    {
        string logPath = GetPlayerLogPath(companyName, productName);
        
        if (!File.Exists(logPath))
        {
            return null;
        }
        
        try
        {
            // Use FileShare.ReadWrite to handle cases where the game is still running
            using var fileStream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fileStream);
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            // Handle file access exceptions
            throw new IOException($"Failed to read Player.log: {ex.Message}", ex);
        }
    }
}