using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace LoE_Launcher.Core
{
    public enum OS
    {
        WindowsX86,
        WindowsX64,
        Mac,
        X11,
        Other
    }

    static class Platform
    {
        public static OS OperatingSystem { get; }
        public static bool UseShellExecute => OperatingSystem == OS.WindowsX64 || OperatingSystem == OS.WindowsX86;


        static bool is64BitProcess = (IntPtr.Size == 8);
        static bool is64BitOperatingSystem = is64BitProcess || InternalCheckIsWow64();

        

        static Platform()
        {
            if (Path.DirectorySeparatorChar == '\\')
                OperatingSystem = is64BitOperatingSystem ? OS.WindowsX64 : OS.WindowsX86;
            else if (IsRunningOnMac())
                OperatingSystem = OS.Mac;
            else if (Environment.OSVersion.Platform == PlatformID.Unix)
                OperatingSystem = OS.X11;
            else
                OperatingSystem = OS.Other;
        }

        public static bool InternalCheckIsWow64()
        {
            return true;
            if ((Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1) ||
                Environment.OSVersion.Version.Major >= 6)
            {
                using (Process p = Process.GetCurrentProcess())
                {
                    bool retVal;
                    if (!IsWow64Process(p.Handle, out retVal))
                    {
                        return false;
                    }
                    return retVal;
                }
            }
            else
            {
                return false;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process(
            [In] IntPtr hProcess,
            [Out] out bool wow64Process

        );

        //From Managed.Windows.Forms/XplatUI
        [DllImport("libc")]
        static extern int uname(IntPtr buf);
        static bool IsRunningOnMac()
        {
            IntPtr buf = IntPtr.Zero;
            try
            {
                buf = Marshal.AllocHGlobal(8192);
                // This is a hacktastic way of getting sysname from uname ()
                if (uname(buf) == 0)
                {
                    string os = Marshal.PtrToStringAnsi(buf);
                    if (os == "Darwin")
                        return true;
                }
            }
            catch
            {
            }
            finally
            {
                if (buf != IntPtr.Zero)
                    Marshal.FreeHGlobal(buf);
            }
            return false;
        }
    }
}
