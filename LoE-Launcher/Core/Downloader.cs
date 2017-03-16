using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using LoE_Launcher.Properties;
//using NDepend.Path;
using Newtonsoft.Json;

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

    public class Downloader
    {
        private IRelativeFilePath _settingsFile = "settings.json".ToRelativeFilePathAuto();
        private IRelativeDirectoryPath _gameInstallationFolder = ".\\game".ToRelativeDirectoryPathAuto();
        private IRelativeDirectoryPath _toolsFolder = ".\\tools".ToRelativeDirectoryPathAuto();
        private IAbsoluteDirectoryPath _launcherPath =
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location).ToAbsoluteDirectoryPathAuto();

        private string ZsyncLocation => _settings.FormatZsyncLocation(_versionDownload);

        private string _versionDownload = "";
        public DownloadData _data = null;
        private Version _maxVersionSupported = new Version(0, 2);
        private GameState _state = GameState.Unknown;
        
        public static OS OperatingSystem { get; }

        static Downloader()
        {
            //OperatingSystem = OS.WindowsX86;
            //return;
            if (Path.DirectorySeparatorChar == '\\')
                OperatingSystem = is64BitOperatingSystem ? OS.WindowsX64 : OS.WindowsX86;
            else if (IsRunningOnMac())
                OperatingSystem = OS.Mac;
            else if (Environment.OSVersion.Platform == PlatformID.Unix)
                OperatingSystem = OS.X11;
            else
                OperatingSystem = OS.Other;

        }
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

        static bool is64BitProcess = (IntPtr.Size == 8);
        static bool is64BitOperatingSystem = is64BitProcess || InternalCheckIsWow64();
        public ProgressData Progress { get; private set; }

        public Downloader()
        {
            Progress = new ProgressData(this);

            var settingsFile = SettingsFile;
            _settings = settingsFile.Exists ? 
                JsonConvert.DeserializeObject<Settings>(File.ReadAllText(settingsFile.Path)) 
                : new Settings();

            if (OperatingSystem == OS.X11)
                _toolsFolder = "/usr/bin/".ToRelativeDirectoryPathAuto();
        }

        public IAbsoluteDirectoryPath GameInstallFolder => _gameInstallationFolder.GetAbsolutePathFrom(_launcherPath);
        public IAbsoluteDirectoryPath ToolsFolder => _toolsFolder.GetAbsolutePathFrom(_launcherPath);
        public IAbsoluteDirectoryPath LauncherFolder => _launcherPath;
        public IAbsoluteFilePath SettingsFile => _settingsFile.GetAbsolutePathFrom(_launcherPath);
        public GameState State => _state;

        public async Task RefreshState()
        {
            try
            {
                Progress = new RefreshProgress(this) {Marquee = true};
                using (new Processing(Progress))
                {
                    await GetVersion();
                    var url = new Uri(ZsyncLocation + ".zsync-control.jar");
                    var data = await DownloadMainControlFile(url);

                    if (data == null)
                        return;
                    _data = data;
                    if (_data.ControlFile.RootUri == null)
                    {
                        _data.ControlFile.RootUri = new Uri(ZsyncLocation);
                    }
                    if (_data.ToProcess.Count == 0)
                    {
                        _state = GameState.UpToDate;
                        return;
                    }
                    if (!GameInstallFolder.Exists)
                    {
                        _state = GameState.NotFound;
                        return;
                    }
                    _state = GameState.UpdateAvailable;
                }
            }
            catch (Exception e)
            {
                _state = GameState.Unknown;
            }
        }

        private async Task GetVersion()
        {
            var data = await DownloadJson<VersionsControlFile>(new Uri(_settings.Stream));
            switch (OperatingSystem)
            {
                case OS.WindowsX86:
                    _versionDownload = data.Win32;
                    break;
                case OS.WindowsX64:
                    _versionDownload = data.Win64;
                    break;
                case OS.Mac:
                    _versionDownload = data.Mac;
                    break;
                case OS.X11:
                    _versionDownload = data.Linux;
                    break;
                case OS.Other:
                    _versionDownload = data.Win32;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public async Task DoInstallation()
        {
            try
            {
                await PrepareUpdate();
            }
            catch (Exception ex)
            {
                await ExtractContent();
                await Cleanup();
                await RefreshState();
                MessageBox.Show("The Launcher ran into a critical error while trying to patch your game. Please try again later.\n\nException: " + ex.ToString());
                return;
            }
            await InstallUpdate();
            await ExtractContent();
            await Cleanup();
            await RefreshState();
        }

        public async Task PrepareUpdate()
        {
            InstallResources();

            Progress = new PreparingProgress(this) { Marquee = true };
            using (new Processing(Progress))
            {

                Progress.ResetCounter(_data.ToProcess.Count, true);
                foreach (var controlFileItem in _data.ToProcess)
                {
                    CompressOriginalFile(controlFileItem.GetUnzippedFileName().GetAbsolutePathFrom(GameInstallFolder));
                    await DownloadZyncFile(controlFileItem);
                    Progress.Count();
                }
            }
        }

        const int BYTES_TO_READ = sizeof(Int64);
        private readonly Settings _settings;

        static bool FilesAreEqual(FileInfo first, FileInfo second)
        {
            if (first.Length != second.Length)
                return false;

            int iterations = (int)Math.Ceiling((double)first.Length / BYTES_TO_READ);

            using (FileStream fs1 = first.OpenRead())
            using (FileStream fs2 = second.OpenRead())
            {
                byte[] one = new byte[BYTES_TO_READ];
                byte[] two = new byte[BYTES_TO_READ];

                for (int i = 0; i < iterations; i++)
                {
                    fs1.Read(one, 0, BYTES_TO_READ);
                    fs2.Read(two, 0, BYTES_TO_READ);

                    if (BitConverter.ToInt64(one, 0) != BitConverter.ToInt64(two, 0))
                        return false;
                }
            }

            return true;
        }

        private void InstallResources()
        {
            var location = "LoE_Data\\Resources\\unity default resources";
            if (File.Exists("game\\" + location))
                File.Delete("game\\" + location);

            Directory.CreateDirectory("game\\LoE_Data\\Resources\\");

            File.WriteAllBytes(GameInstallFolder.GetChildFileWithName(location).ToString(),
                Resources.unity_default_resources);
        }

        public async Task InstallUpdate()
        {
            Progress = new InstallingProgress(this) { Marquee = true };
            using (new Processing(Progress))
            {
                await UpdateFiles(3);
            }
        }

        private async Task UpdateFiles(int retries = 0)
        {
            int tries = 0;
            var queue = new Queue<ControlFileItem>(_data.ToProcess);
            Progress.ResetCounter(queue.Count, true);
            while (tries <= retries)
            {
                tries++;
                var reProcess = new Queue<ControlFileItem>();

				while (queue.Any())
				{
					try
					{

						var item = queue.Dequeue();
						var uri = item.GetContentUri(_data.ControlFile).ToString().Substring(0, item.GetContentUri(_data.ControlFile).ToString().Length - 10).ToString();

						var arguments = "-u \"" + uri.ToString().Replace(" ", "%20") + "\" -o \"" + Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(item.InstallPath.FileName)) + "\" -i \"" + Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(item.InstallPath.FileName)) + "\" \"" +
										   Path.GetFileNameWithoutExtension(item.InstallPath.ToString()) + "\"";
						Console.WriteLine(arguments);
						var fileName = ToolsFolder.GetChildFileWithName("zsync".SetExeName()).ToString();

						/* if(OperatingSystem == OS.Mac || OperatingSystem == OS.X11){
							 fileName = "zsync";
						 }*/

						Console.WriteLine(fileName);

						var resp = new Process().RunInlineAndWait(new ProcessStartInfo(fileName,
							arguments)
						{
							UseShellExecute = OperatingSystem == OS.WindowsX64 || OperatingSystem == OS.WindowsX86,
							WindowStyle = ProcessWindowStyle.Minimized,
							WorkingDirectory = item.InstallPath.ParentDirectoryPath.GetAbsolutePathFrom(GameInstallFolder).ToString()
						});
						if (resp != 0)
						{
							reProcess.Enqueue(item);
						}
						else
						{

							var absolutePathFrom =
								item.InstallPath.GetAbsolutePathFrom(GameInstallFolder)
									.GetBrotherFileWithName(
										item.InstallPath.FileNameWithoutExtension.ToRelativeFilePathAuto()
											.FileNameWithoutExtension);
							UnzipFile(absolutePathFrom);
							//UnzipFile(GameInstallFolder.GetChildFileWithName(item.InstallPath.ToString()).GetBrotherFileWithName(item.InstallPath.FileNameWithoutExtension.ToRelativeFilePathAuto()
							//                .FileNameWithoutExtension));
							Progress.Count();
						}
					}
					catch (Exception e)
					{
						Console.WriteLine("Exception : " + e);
					}
				}

                queue = reProcess;
                if (!reProcess.Any())
                    break;
                await Task.Delay(1000);

            }

            if (queue.Any())
            {
                //throw new Exception("Failed to get all files!");
            }
        }

        public async Task ExtractContent()
        {
            //throw new Exception("HAHA");
            Progress = new UnzipProgress(this) { Marquee = true };
            using (new Processing(Progress))
            {
                await UnzipAllContent();
            }
        }

        public async Task Cleanup()
        {
            Progress = new CleanupProgress(this) { Marquee = true };
            using (new Processing(Progress))
            {
                CleanGameFolder();
            }
        }

        private async Task UnzipAllContent()
        {
            return;
            foreach (var file in GameInstallFolder.DirectoryInfo.EnumerateFiles("*.jar", SearchOption.AllDirectories))
            {
                file.Rename(Path.GetFileNameWithoutExtension(file.Name) + ".gz");
            }
            
            var fileName = ToolsFolder.GetChildFileWithName("gunzip".SetExeName()).ToString();

            if(OperatingSystem == OS.Mac || OperatingSystem == OS.X11){
                    fileName = "gunzip";
                }

            Console.WriteLine(fileName);
            new Process().RunInlineAndWait(new ProcessStartInfo(fileName,
                "-r \"" + GameInstallFolder.DirectoryName + "\"")
            {
                UseShellExecute = OperatingSystem == OS.WindowsX64 || OperatingSystem == OS.WindowsX86,
                WindowStyle = ProcessWindowStyle.Minimized,
                WorkingDirectory = GameInstallFolder.ParentDirectoryPath.ToString()
            });
            
            CleanGameFolder();
        }

        private void UnzipFile(IAbsoluteFilePath file)
        {
            if(File.Exists(file.FileNameWithoutExtension + ".gz"))
                File.Delete(file.FileNameWithoutExtension + ".gz");

            file.FileInfo.Rename(file.FileNameWithoutExtension + ".gz");

            var nFile = file.GetBrotherFileWithName(file.FileNameWithoutExtension + ".gz");

            var fileName = ToolsFolder.GetChildFileWithName("gunzip".SetExeName()).ToString();

            if(OperatingSystem == OS.Mac || OperatingSystem == OS.X11){
                    fileName = "gunzip";
                }

            new Process().RunInlineAndWait(new ProcessStartInfo(fileName,
                "\"" + nFile + "\"")
            {
                UseShellExecute = OperatingSystem == OS.WindowsX64 || OperatingSystem == OS.WindowsX86,
                WindowStyle = ProcessWindowStyle.Minimized,
                WorkingDirectory = GameInstallFolder.ParentDirectoryPath.ToString()
            });
        }

        private void CleanGameFolder()
        {
            if (!GameInstallFolder.Exists)
            {
                Directory.CreateDirectory(GameInstallFolder.ToString());
            }
            foreach (var file in GameInstallFolder.DirectoryInfo.EnumerateFiles("*.zsync", SearchOption.AllDirectories))
            {
                file.Delete();
            }
            foreach (var file in GameInstallFolder.DirectoryInfo.EnumerateFiles("*.jar", SearchOption.AllDirectories))
            {
                file.Delete();
            }
            foreach (var file in GameInstallFolder.DirectoryInfo.EnumerateFiles("*.gz", SearchOption.AllDirectories))
            {
                file.Delete();
            }
            foreach (var file in GameInstallFolder.DirectoryInfo.EnumerateFiles("*.zs-old", SearchOption.AllDirectories))
            {
                file.Delete();
            }
            foreach (var file in GameInstallFolder.DirectoryInfo.EnumerateFiles("*%20*", SearchOption.AllDirectories))
            {
                file.Rename(file.Name.Replace("%20"," "));
            }
        }

        private async Task<DownloadData> DownloadMainControlFile(Uri url)
        {
            var mainControlFile = await DownloadJson<MainControlFile>(url);
            if (mainControlFile == null)
                return null;
            var data = new DownloadData(mainControlFile);
            if (data.ControlFile.Version.CompareTo(_maxVersionSupported) < 0 ||
                data.ControlFile.Version.CompareTo(_maxVersionSupported) > 0)
            {
                _state = GameState.LauncherOutOfDate;
                return null;
            }

            Progress.ResetCounter(data.ControlFile.Content.Count, true);
            
            foreach (var item in data.ControlFile.Content)
            {
                try
                {
                    var realFile = item.GetUnzippedFileName().GetAbsolutePathFrom(GameInstallFolder);
                    if(realFile.FileName.Contains(" "))
                        realFile = realFile.GetBrotherFileWithName(realFile.FileName.Replace(" ", "%20"));

                    if (realFile.Exists &&
                        realFile.ToString().GetFileHash(HashType.MD5) == item.FileHash || realFile.FileName.ToLower().Contains("default"))
                        continue;
                    data.ToProcess.Add(item);
                }
                finally
                {
                    Progress.Count();
                }
            }
            return data;
        }

        private async Task<TType> DownloadJson<TType>(Uri url)
            where TType : class
        {
            string result;
            try
            {
                using (var client = new HttpClient())
                {
                    //Console.WriteLine(url);
                    result = await client.GetStringAsync(url);
                    //Console.WriteLine(result);
                }
            }
            catch (Exception e)
            {
                _state = GameState.Offline;
                return null;
            }
            return JsonConvert.DeserializeObject<TType>(result);
        }

        private async Task DownloadZyncFile(ControlFileItem item)
        {
            using (var client = new HttpClient())
            {
                var childFileWithName = item.InstallPath.GetAbsolutePathFrom(GameInstallFolder);
                var contentUri = item.GetContentUri(_data.ControlFile);
                var str = await client.GetByteArrayAsync(contentUri);

                Directory.CreateDirectory(childFileWithName.ParentDirectoryPath.ToString());
                File.WriteAllBytes(
                    childFileWithName.GetBrotherFileWithName(Path.GetFileNameWithoutExtension(childFileWithName.FileName))
                        .ToString(), str);
            }
        }

        private void CompressOriginalFile(IAbsoluteFilePath realFile)
        {
            if (realFile.Exists)
            {
                var fileName = ToolsFolder.GetChildFileWithName("gzip".SetExeName()).ToString();
                
                if(OperatingSystem == OS.Mac || OperatingSystem == OS.X11){
                    fileName = "gzip";
                }

                new Process().RunInlineAndWait(new ProcessStartInfo(fileName,
                    "\"" + realFile + "\"")
                {
                    UseShellExecute = OperatingSystem == OS.WindowsX64 || OperatingSystem == OS.WindowsX86,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    WorkingDirectory = GameInstallFolder.ParentDirectoryPath.ToString()
                });
                var compressedFile = realFile.GetBrotherFileWithName(realFile.FileName + ".gz");
                var newName = compressedFile.FileNameWithoutExtension + ".jar";
                compressedFile.FileInfo.Rename(newName);
                compressedFile = realFile.GetBrotherFileWithName(realFile.FileName + ".jar");
                if (compressedFile.FileName.Contains(" "))
                {
                    compressedFile.FileInfo.Rename(compressedFile.FileName.Replace(" ", "%20"));
                }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process(
            [In] IntPtr hProcess,
            [Out] out bool wow64Process
        );

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

        public class DownloadData
        {
            public MainControlFile ControlFile { get; private set; }
            public List<ControlFileItem> ToProcess { get; set; }

            public DownloadData(MainControlFile controlFile)
            {
                ControlFile = controlFile;
                ToProcess = new List<ControlFileItem>();
            }
        }

        public class RefreshProgress : ProgressData
        {
            public RefreshProgress(Downloader model) : base(model)
            {
            }

            protected override string GetText()
            {
                if (IsFinished)
                {
                    if (Model._data?.ToProcess.Count != 0)
                    {
                        return "Files to Update: {0}".Format(Model._data?.ToProcess.Count);
                    }
                    return "Ready to Launch!";
                }
                else
                {
                    return "Preparing...";
                }
            }
        }
        public class PreparingProgress : ProgressData
        {
            public PreparingProgress(Downloader model) : base(model)
            {
            }

            protected override string GetText()
            {
                if (IsFinished)
                {
                    return "Preparing Install...";
                }
                else
                {
                    if(Marquee)
                        return "Preparing Install...";
                    return "Preparing Install ({0}/{1})...".Format(Current, Max);
                }
            }
        }
        public class InstallingProgress : ProgressData
        {
            public InstallingProgress(Downloader model) : base(model)
            {
            }

            protected override string GetText()
            {
                if (IsFinished)
                {
                    return "Installing...";
                }
                else
                {
                    if (Marquee)
                        return "Installing...";
                    return "Installing ({0}/{1})...".Format(Current, Max);
                }
            }
        }
        public class UnzipProgress : ProgressData
        {
            public UnzipProgress(Downloader model) : base(model)
            {
            }

            protected override string GetText()
            {
                if (IsFinished)
                {
                    return "Extracting...";
                }
                else
                {
                    return "Extracting...";
                }
            }
        }
        public class CleanupProgress : ProgressData
        {
            public CleanupProgress(Downloader model) : base(model)
            {
            }

            protected override string GetText()
            {
                if (IsFinished)
                {
                    return "Cleaning up...";
                }
                else
                {
                    return "Cleaning up...";
                }
            }
        }
        public class UpToDateProgress : ProgressData
        {
            public UpToDateProgress(Downloader model) : base(model)
            {
            }

            protected override string GetText()
            {
                return "Up to date";
            }
        }
    }

    public enum GameState
    {
        Unknown,
        NotFound,
        UpdateAvailable,
        UpToDate,
        Offline,
        LauncherOutOfDate
    }

    public class ProgressData
    {
        protected Downloader Model {  get; private set; }
        public int Max { get; set; } = 100;
        public int Current { get; set; } = 0;
        public bool Marquee { get; set; } = false;
        public bool Processing { get; set; } = false;
        public bool IsFinished { get; set; } = false;
        public string Text => GetText();

        public ProgressData(Downloader model)
        {
            Model = model;
        }

        public void ResetCounter(int count, bool changeFromMarquee = false)
        {
            Current = 0;
            Max = count;
            if (changeFromMarquee)
                Marquee = false;
        }

        public void Count(int count = 1)
        {
            if (Current + count > Max)
            {
                throw new ArithmeticException("Current can not be higher than Maximum");
                //return;
            }
            Current += count;
        }

        protected virtual string GetText()
        {
            return "Processing....";
        }
    }

    public class Processing : IDisposable
    {
        private readonly ProgressData _state;

        public Processing(ProgressData state)
        {
            _state = state;
            _state.Processing = true;
        }

        public void Dispose()
        {
            _state.Processing = false;
            _state.IsFinished = true;
        }
    }
}
