using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

//using NDepend.Path;

namespace LoE_Launcher.Core
{
    public static class FileSystemInfoExtensions
    {
        public static string SetExeName(this string name)
        {
            if (Downloader.OperatingSystem == OS.WindowsX64 || Downloader.OperatingSystem == OS.WindowsX86)
                return name + ".exe";
            return name;
        }

        public static void Rename(this FileSystemInfo item, string newName)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            FileInfo fileInfo = item as FileInfo;
            if (fileInfo != null)
            {
                fileInfo.Rename(newName);
                return;
            }

            DirectoryInfo directoryInfo = item as DirectoryInfo;
            if (directoryInfo != null)
            {
                directoryInfo.Rename(newName);
                return;
            }

            throw new ArgumentException("Item", "Unexpected subclass of FileSystemInfo " + item.GetType());
        }

        public static void Rename(this FileInfo file, string newName)
        {
            // Validate arguments.
            if (file == null)
            {
                throw new ArgumentNullException("file");
            }
            else if (newName == null)
            {
                throw new ArgumentNullException("newName");
            }
            else if (newName.Length == 0)
            {
                throw new ArgumentException("The name is empty.", "newName");
            }
            else if (newName.IndexOf(Path.DirectorySeparatorChar) >= 0
                     || newName.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                throw new ArgumentException("The name contains path separators. The file would be moved.", "newName");
            }

            // Rename file.
            string newPath = Path.Combine(file.DirectoryName, newName);
            file.MoveTo(newPath);
        }

        public static void Rename(this DirectoryInfo directory, string newName)
        {
            // Validate arguments.
            if (directory == null)
            {
                throw new ArgumentNullException("directory");
            }
            else if (newName == null)
            {
                throw new ArgumentNullException("newName");
            }
            else if (newName.Length == 0)
            {
                throw new ArgumentException("The name is empty.", "newName");
            }
            else if (newName.IndexOf(Path.DirectorySeparatorChar) >= 0
                     || newName.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                throw new ArgumentException("The name contains path separators. The directory would be moved.", "newName");
            }

            // Rename directory.
            string newPath = Path.Combine(directory.Parent.FullName, newName);
            directory.MoveTo(newPath);
        }
        public static string GetFileHash(this string filePath, HashType type)
        {
            if (!File.Exists(filePath))
                return string.Empty;


            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    return md5.ComputeHash(stream).ByteArrayToString();
                }
            }
        }
        public static string ByteArrayToString(this byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        public static IRelativeFilePath GetUnzippedFileName(this ControlFileItem item)
        {
            return item.InstallPath.GetBrotherFileWithName(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(item.InstallPath.FileNameWithoutExtension)));
        }

        public static Uri GetContentUri(this ControlFileItem item, MainControlFile controlData)
        {
            return new Uri(controlData.RootUri, item.RelativeContentUrl.ToString().Replace("../","./"));
        }
        public static int RunInlineAndWait(this Process p, ProcessStartInfo startInfo)
        {
            p.StartInfo = startInfo;
            //p.StartInfo.UseShellExecute = false;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.Start();
            try{
                p.WaitForExit();
            }catch{

            }
            return p.ExitCode;
        }

        public static string Format(this string @this, params object[] args)
        {
            return string.Format(@this, args);
        }

        public static void CopyResource(string resourceName, string file)
        {
            using (Stream resource = Assembly.GetExecutingAssembly()
                                              .GetManifestResourceStream(resourceName))
            {
                if (resource == null)
                {
                    throw new ArgumentException("No such resource", "resourceName");
                }
                using (Stream output = File.OpenWrite(file))
                {
                    resource.CopyTo(output);
                }
            }
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
}