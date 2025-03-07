using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using ICSharpCode.SharpZipLib.GZip;
using Newtonsoft.Json;
using NLog;

namespace LoE_Launcher.Core
{
    public partial class Downloader
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private IRelativeFilePath _settingsFile = "settings.json".ToRelativeFilePathAuto();
        private IRelativeDirectoryPath _gameInstallationFolder = ".\\game".ToRelativeDirectoryPathAuto();
        private IAbsoluteDirectoryPath _launcherPath =
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location).ToAbsoluteDirectoryPathAuto();

        private string ZsyncLocation => _settings.FormatZsyncLocation(_versionDownload);

        private string _versionDownload = "";
        public DownloadData _data = null;
        private Version _maxVersionSupported = new Version(0, 2);
        private GameState _state = GameState.Unknown;

        public static OS OperatingSystem => Platform.OperatingSystem;
        

        public ProgressData Progress { get; private set; }

        public Downloader()
        {
            Progress = new ProgressData(this);

            var settingsFile = SettingsFile;
            _settings = settingsFile.Exists ? 
                JsonConvert.DeserializeObject<Settings>(File.ReadAllText(settingsFile.Path)) 
                : new Settings();
        }

        public IAbsoluteDirectoryPath GameInstallFolder => _gameInstallationFolder.GetAbsolutePathFrom(_launcherPath);
        public IAbsoluteDirectoryPath LauncherFolder => _launcherPath;
        public IAbsoluteFilePath SettingsFile => _settingsFile.GetAbsolutePathFrom(_launcherPath);
        public GameState State => _state;

        public long BytesDownloaded { get; private set; }

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
                logger.Error(e, "RefreshState failed");
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
                logger.Error(ex, "DoInstallation->PrepareUpdate failed");

                await Cleanup();
                await RefreshState();
                MessageBox.Show("The Launcher ran into a critical error while trying to patch your game. Please try again later.\n\nException: " + ex.ToString());
                return;
            }
            await InstallUpdate();
            await Cleanup();
            await RefreshState();
        }

        public async Task PrepareUpdate()
        {
            Progress = new PreparingProgress(this) { Marquee = true };
            using (new Processing(Progress))
            {
                var tasks = new List<Task>(_data.ToProcess.Count);
                Progress.ResetCounter(_data.ToProcess.Count, true);

                foreach (var controlFileItem in _data.ToProcess)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        CompressOriginalFile(controlFileItem.GetUnzippedFileName().GetAbsolutePathFrom(GameInstallFolder));
                        Progress.Count();
                    }));
                }

                await Task.WhenAll(tasks);
            }
        }

        private readonly Settings _settings;

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
            var installProgress = Progress as InstallingProgress;

            int tries = 0;
            var queue = new Queue<ControlFileItem>(_data.ToProcess);
            Progress.ResetCounter(queue.Count, true);
            BytesDownloaded = 0;
            while (tries <= retries)
            {
                tries++;
                var reProcess = new Queue<ControlFileItem>();
                //allows us to zsync the next file while we wait for unzip.
                Task lastUnzip = Task.FromResult(0);

				while (queue.Any())
				{
					try
					{

						var item = queue.Dequeue();
                        var zsyncUri = item.GetContentUri(_data.ControlFile);
                        var objUri = zsyncUri.ToString().Substring(0, zsyncUri.ToString().Length - ControlFileItem.ZsyncExtension.Length).ToString();

                        var zsyncFilePath = item.InstallPath.GetAbsolutePathFrom(GameInstallFolder).Path;
                        var objFilePath = zsyncFilePath.Substring(0, zsyncFilePath.Length - ControlFileItem.ZsyncExtension.Length);

                        var fileName = new CustomFilePath(objFilePath).FileNameWithoutExtension;

                        long bytesRead;
                        try
                        {
                            bytesRead = zsyncnet.Zsync.Sync(zsyncUri, new FileInfo(objFilePath), new Uri(objUri), (ss) =>
                            {
                                string flavor;
                                switch (ss)
                                {
                                    case zsyncnet.SyncState.CalcDiff: flavor = $"{fileName} diff"; break;
                                    case zsyncnet.SyncState.CopyExisting: flavor = $"{fileName} copying parts"; break;
                                    case zsyncnet.SyncState.DownloadPatch: flavor = $"{fileName} downloading patch"; break;
                                    case zsyncnet.SyncState.DownloadNew: flavor = $"{fileName} downloading"; break;
                                    case zsyncnet.SyncState.PatchFile: flavor = $"{fileName} patching"; break;
                                    default:
                                        flavor = "";
                                        break;
                                }
                                installProgress.FlavorText = flavor;
                            });
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("zsync exception: " + e);
                            bytesRead = 0;
                            File.Delete(objFilePath);
                        }

						if (bytesRead == 0)
                        {
                            reProcess.Enqueue(item);
                        }
						else
						{
                            BytesDownloaded += bytesRead;
                            //but don't do more than one unzip at a time, because it's probably hd speed limited.
                            //so wait for last one to finish before starting our next one.
                            await lastUnzip;

							var absolutePathFrom =
								item.InstallPath.GetAbsolutePathFrom(GameInstallFolder)
									.GetBrotherFileWithName(
										item.InstallPath.FileNameWithoutExtension.ToRelativeFilePathAuto()
											.FileNameWithoutExtension);
							lastUnzip = UnzipFile(absolutePathFrom);
							Progress.Count();
						}
					}
					catch (Exception e)
					{
						Console.WriteLine("Exception : " + e);
					}
				}
                
                //any more pending unzips?
                await lastUnzip;

                queue = reProcess;
                if (!reProcess.Any())
                    break;
                await Task.Delay(1000);

            }

            if (queue.Any())
            {
                //throw new Exception("Failed to get all files!");
            }

            installProgress.FlavorText = "";
        }

        public async Task Cleanup()
        {
            Progress = new CleanupProgress(this) { Marquee = true };
            using (new Processing(Progress))
            {
                CleanGameFolder();
            }
        }

        private Task UnzipFile(IAbsoluteFilePath file)
        {
            return Task.Run(() =>
            {
                using (var inputStream = new FileStream(file.Path, FileMode.Open))
                using (var outputStream = new FileStream(file.GetBrotherFileWithName(file.FileNameWithoutExtension).Path, FileMode.Create))
                {
                    GZip.Decompress(inputStream, outputStream, false);
                };
            });
        }

        private void CleanGameFolder()
        {
            try
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
            }
            catch (Exception e)
            {
                logger.Error(e, "CleanGameFolder failed");
                Progress = new ErrorProgress($"Could not clean game folder", this);
                _state = GameState.Unknown;
                throw;
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

                    if (realFile.Exists &&
                        realFile.ToString().GetFileHash(HashType.MD5) == item.FileHash)
                        continue;
                    data.ToProcess.Add(item);
                }
                catch(Exception e)
                {
                    logger.Error(e, $"issue with control file item {item.InstallPath}");
                    throw;
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
                logger.Error(e, $"could not download json @ {url}");
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
                var stream = await client.GetStreamAsync(contentUri);

                Directory.CreateDirectory(childFileWithName.ParentDirectoryPath.ToString());
                var filePath = childFileWithName.GetBrotherFileWithName(Path.GetFileNameWithoutExtension(childFileWithName.FileName))
                        .ToString();
                using (var fs = File.Create(filePath))
                {
                    await stream.CopyToAsync(fs);
                }
            }
        }

        private void CompressOriginalFile(IAbsoluteFilePath realFile)
        {
            if (realFile.Exists)
            {
                var compressedFile = realFile.GetBrotherFileWithName(realFile.FileName + ".jar");
                using (var inputStream = new FileStream(realFile.Path, FileMode.Open))
                using (var outputStream = new FileStream(compressedFile.Path, FileMode.Create))
                {
                    GZip.Compress(inputStream, outputStream, false);
                }
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
