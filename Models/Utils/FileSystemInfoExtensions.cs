using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Models.Paths;
using NLog;

namespace Models.Utils;

public static class FileSystemInfoExtensions
{
    private static Logger logger = LogManager.GetCurrentClassLogger();

    public static string SetExeName(this string name)
    {
        if (PlatformUtils.OperatingSystem == OS.WindowsX64 || PlatformUtils.OperatingSystem == OS.WindowsX86)
        {
            return $"{name}.exe";
        }

        return name;
    }

    public static string GetFileHash(this string filePath, HashType type)
    {
        if (!File.Exists(filePath))
        {
            return string.Empty;
        }

        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        return md5.ComputeHash(stream).ByteArrayToString();
    }

    public static string ByteArrayToString(this byte[] ba)
    {
        var hex = new StringBuilder(ba.Length * 2);
        foreach (var b in ba)
        {
            hex.Append($"{b:x2}");
        }

        return hex.ToString();
    }

    public static IRelativeFilePath GetUnzippedFileName(this ControlFileItem item)
    {
        var installPath = item.RequireInstallPath();
        return installPath.GetBrotherFileWithName(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(installPath.FileNameWithoutExtension)));
    }

    public static Uri GetContentUri(this ControlFileItem item, MainControlFile controlData)
    {
        var rootUri = controlData.RootUri ?? throw new InvalidDataException("The control file has no root URI");
        var relativeUrl = item.RelativeContentUrl ?? throw new InvalidDataException($"Control file entry {item.InstallPath} has no content URL");
        return new Uri(rootUri, relativeUrl.ToString().Replace("../", "./"));
    }

    public static int RunInlineAndWait(this Process p, ProcessStartInfo startInfo)
    {
        p.StartInfo = startInfo;
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.Start();

        var output = p.StandardOutput.ReadToEnd();
        var err = p.StandardError.ReadToEnd();

        try
        {
            p.WaitForExit();
        }
        catch (Exception e)
        {
            logger.Error(e, $"could not RunInlineAndWait {startInfo.FileName} {startInfo.Arguments}");
        }

        if (p.ExitCode == 0)
        {
            logger.Info(output);
        }
        else
        {
            logger.Error($"{startInfo.Arguments} failed. Console output: {err}");
        }

        return p.ExitCode;
    }

    public static Task<int> RunAsTask(this Process p, ProcessStartInfo startInfo)
    {
        p.StartInfo = startInfo;
        p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        p.Start();
        return Task.Run(() => {
            try
            {
                p.WaitForExit();
            }
            catch (Exception e)
            {
                logger.Error(e, $"could not RunAsTask {startInfo.FileName} {startInfo.Arguments}");
            }
            return p.ExitCode;
        });
    }

    public static string Format(this string @this, params object[] args)
    {
        return string.Format(@this, args);
    }

    public static void CopyResource(string resourceName, string file)
    {
        using var resource = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(resourceName);
        if (resource == null)
        {
            throw new ArgumentException("No such resource", nameof(resourceName));
        }
        using Stream output = File.OpenWrite(file);
        resource.CopyTo(output);
    }
}

public enum HashType
{
    [Description("SHA-1")]
    SHA1,
    [Description("SHA-256")]
    SHA256,
    [Description("SHA-384")]
    SHA384,
    [Description("SHA-512")]
    SHA512,
    [Description("MD5")]
    MD5,
    [Description("RIPEMD-160")]
    RIPEMD160
}
