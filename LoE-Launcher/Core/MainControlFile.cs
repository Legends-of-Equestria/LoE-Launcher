using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;

namespace LoE_Launcher.Core
{
    public class VersionsControlFile
    {
        public string Win32 { get; set; }
        public string Win64 { get; set; }
        public string Linux { get; set; }
        public string Mac { get; set; }
    }
    public class MainControlFile
    {

        public MainControlFile()
        {
            Content = new List<ControlFileItem>();
            Version = new Version(0, 2);
        }

        public Version Version { get; set; }
        public List<ControlFileItem> Content { get; set; }
        public Uri RootUri { get; set; }
    }

    [DebuggerDisplay("{InstallPath} - {FileHash}")]
    public class ControlFileItem
    {
        public Uri RelativeContentUrl { get; set; }
        [JsonIgnore]
        public IRelativeFilePath InstallPath { get; set; }
        public string FileHash { get; set; }


        public string _installPath
        {
            get
            {
                if (InstallPath == null)
                    return null;
                return InstallPath.ToString();
            }
            set
            {
                if (value == null)
                    InstallPath = null;
                InstallPath = value.ToRelativeFilePathAuto();
            }
        }
    }
}