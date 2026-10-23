using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NLog;

namespace LoE_Launcher.Services;

// Windows: DPAPI for the current user. Linux/macOS: owner-only file, like an SSH key
public class DevTokenStore(string launcherPath)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly byte[] Entropy = "LoE-Launcher dev token v1"u8.ToArray();

    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    public string TokenFilePath => Path.Combine(launcherPath,
        System.OperatingSystem.IsWindows() ? "dev-token.bin" : "dev-token");

    public string Load()
    {
        try
        {
            if (!File.Exists(TokenFilePath))
            {
                return "";
            }

            var bytes = File.ReadAllBytes(TokenFilePath);
            if (System.OperatingSystem.IsWindows())
            {
                bytes = ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.CurrentUser);
            }
            else
            {
                RestrictPermissions(TokenFilePath);
            }

            return Encoding.UTF8.GetString(bytes).Trim();
        }
        catch (Exception ex)
        {
            // Usually a token file from another user or PC, never log its contents
            Logger.Warn($"Could not read the stored dev token ({ex.GetType().Name}); falling back to live builds");
            return "";
        }
    }

    public void Save(string token)
    {
        token = token.Trim();
        if (token.Length == 0)
        {
            Delete();
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(token);
        if (System.OperatingSystem.IsWindows())
        {
            bytes = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
        }

        var tempPath = TokenFilePath + ".tmp";
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!System.OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OwnerOnly;
        }

        using (var stream = new FileStream(tempPath, options))
        {
            stream.Write(bytes);
        }

        if (!System.OperatingSystem.IsWindows())
        {
            RestrictPermissions(tempPath);
        }

        File.Move(tempPath, TokenFilePath, true);
    }

    public void Delete()
    {
        if (File.Exists(TokenFilePath))
        {
            File.Delete(TokenFilePath);
        }
    }

    private static void RestrictPermissions(string path)
    {
        if (System.OperatingSystem.IsWindows())
        {
            return;
        }

        if (File.GetUnixFileMode(path) != OwnerOnly)
        {
            File.SetUnixFileMode(path, OwnerOnly);
        }
    }
}
