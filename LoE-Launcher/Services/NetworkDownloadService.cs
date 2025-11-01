using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LoE_Launcher.Core;
using Models.Paths.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using zsyncnet;
using zsyncnet.Sync;
using SyncState = zsyncnet.SyncState;

namespace LoE_Launcher.Services;

public class NetworkDownloadService(FileOperationsService fileOps)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly int MaxNetworkRetries = 5;

    public static readonly JsonSerializerSettings JsonSettings = new()
    {
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
        DateParseHandling = DateParseHandling.None,
        Converters =
        {
            new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
        },
        ContractResolver = new RelativeFilePathContractResolver()
    };

    public async Task<TType?> DownloadJson<TType>(Uri url, Action<GameState> setState)
        where TType : class
    {
        for (int attempt = 0; attempt < MaxNetworkRetries; attempt++)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(Math.Min(10 + attempt * 5, 30));
                client.DefaultRequestHeaders.UserAgent.ParseAdd("LoE-Launcher/1.0");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Min(10 + attempt * 5, 30)));
                using var response = await client.GetAsync(url, cts.Token);

                if (response.StatusCode
                    is System.Net.HttpStatusCode.NotFound
                    or System.Net.HttpStatusCode.InternalServerError
                    or System.Net.HttpStatusCode.BadGateway
                    or System.Net.HttpStatusCode.ServiceUnavailable
                    or System.Net.HttpStatusCode.GatewayTimeout)
                {
                    Logger.Error($"Server error {(int)response.StatusCode} {response.StatusCode} @ {url}");
                    setState(GameState.ServerMaintenance);
                    return null;
                }

                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync(cts.Token);

                Logger.Info($"JSON download successful (attempt {attempt + 1})");
                return JsonConvert.DeserializeObject<TType>(result, JsonSettings);
            }
            catch (OperationCanceledException) when (attempt < MaxNetworkRetries - 1)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                Logger.Warn($"Request timed out @ {url}, retrying in {delay.TotalSeconds}s");
                await Task.Delay(delay);
            }
            catch (HttpRequestException) when (attempt < MaxNetworkRetries - 1)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                Logger.Warn($"HTTP request failed @ {url}, retrying in {delay.TotalSeconds}s");
                await Task.Delay(delay);
            }
            catch (Exception) when (attempt < MaxNetworkRetries - 1)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                Logger.Warn($"Could not download json @ {url}, retrying in {delay.TotalSeconds}s");
                await Task.Delay(delay);
            }
        }

        Logger.Error($"All download attempts failed for {url}");
        setState(GameState.Offline);
        return null;
    }

    public async Task<bool> TestConnectionIfNeeded(Uri testUri, HttpClient httpClient)
    {
        for (int attempt = 0; attempt < MaxNetworkRetries; attempt++)
        {
            try
            {
                Logger.Info($"Testing connection to: {testUri} (attempt {attempt + 1}/{MaxNetworkRetries})");
                var response = await httpClient.GetAsync(testUri, HttpCompletionOption.ResponseHeadersRead);
                Logger.Info($"Test connection successful: {response.StatusCode}");
                return true;
            }
            catch (Exception ex) when (attempt < MaxNetworkRetries - 1)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                Logger.Warn($"Connection test failed, retrying in {delay.TotalSeconds}s: {ex.Message}");
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "All connection test attempts failed");
                return false;
            }
        }

        return false;
    }

    public ControlFile? DownloadControlFile(Uri uri, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LoE-Launcher/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = client.Send(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Error($"Failed to download control file. Status code: {response.StatusCode}");
                return null;
            }

            using var stream = response.Content.ReadAsStream(cancellationToken);
            return new ControlFile(stream);
        }
        catch (OperationCanceledException ocex)
        {
            Logger.Error(ocex, "Control file download was canceled");
            return null;
        }
        catch (HttpRequestException hrex)
        {
            Logger.Error(hrex, $"HTTP error downloading control file: {hrex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error downloading control file: {ex.Message}");
            return null;
        }
    }

    public async Task<long> DownloadFile(Uri zsyncUri, string objFilePath, Uri objUri, string fileName,
        Downloader.InstallingProgress installProgress, Downloader.DownloadProgressCallback? progressCallback = null)
    {
        long bytesRead;
        try
        {
            bytesRead = await SyncFileWithZsync(zsyncUri, objFilePath, objUri, fileName, installProgress, progressCallback);
            if (bytesRead == 0)
            {
                Logger.Info($"ZSync returned 0 bytes, trying direct download for: {fileName}");
                bytesRead = await DirectDownloadFile(objUri, objFilePath, progressCallback);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, $"File download failed for {fileName}");
            fileOps.DeleteFileIfExists(objFilePath);
            try
            {
                Logger.Info($"Trying direct download fallback for: {fileName}");
                bytesRead = await DirectDownloadFile(objUri, objFilePath, progressCallback);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Direct download fallback failed for {fileName}");
                bytesRead = 0;
            }
        }
        return bytesRead;
    }

    public async Task<long> SyncFileWithZsync(Uri zsyncUri, string objFilePath, Uri objUri,
        string fileName, Downloader.InstallingProgress installProgress, Downloader.DownloadProgressCallback? progressCallback = null)
    {
        Logger.Info($"Attempting zsync for: {fileName}");

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LoE-Launcher/1.0");
        client.Timeout = TimeSpan.FromMinutes(2);

        var downloader = new ProgressReportingRangeDownloader(objUri, client, progressCallback);
        try
        {
            Logger.Info($"Downloading control file from: {zsyncUri}");

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                var controlFile = await Task.Run(() => DownloadControlFile(zsyncUri, cts.Token), cts.Token);
                if (controlFile == null)
                {
                    Logger.Error($"Failed to download control file for {fileName}");
                    return 0;
                }

                Logger.Info($"Control file downloaded successfully for {fileName}");
                var outputDir = new DirectoryInfo(Path.GetDirectoryName(objFilePath));

                var progressAdapter = progressCallback != null
                    ? new Progress<ulong>(bytes => progressCallback((long)bytes))
                    : null;

                var syncTask = Task.Run(() => {
                    Zsync.Sync(controlFile, downloader, outputDir, (ss) => {
                        var flavor = ss switch
                        {
                            SyncState.CalcDiff => "syncing",
                            SyncState.CopyExisting => "copying",
                            SyncState.DownloadPatch => "patching",
                            SyncState.DownloadNew => "fetching",
                            SyncState.PatchFile => "applying",
                            _ => "updating"
                        };
                        installProgress.FlavorText = flavor;
                        Logger.Debug($"ZSync state: {ss} - {flavor}");
                    }, progressAdapter, cts.Token);
                }, cts.Token);

                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(3), cts.Token);
                if (await Task.WhenAny(syncTask, timeoutTask) == timeoutTask)
                {
                    Logger.Error($"Zsync operation timed out for {fileName}");
                    throw new TimeoutException($"Zsync operation timed out for {fileName}");
                }

                await syncTask;
            }

            var fileInfo = new FileInfo(objFilePath);
            if (fileInfo.Exists)
            {
                Logger.Info($"Zsync complete. File exists: {objFilePath}, size: {fileInfo.Length} bytes");
                return fileInfo.Length;
            }

            Logger.Warn($"File does not exist after zsync: {objFilePath}");
            return 0;
        }
        catch (TimeoutException tex)
        {
            Logger.Error(tex, $"Zsync timed out: {tex.Message}");
            return 0;
        }
        catch (TaskCanceledException tcex)
        {
            Logger.Error(tcex, $"Zsync operation was canceled: {tcex.Message}");
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error during zsync: {ex.Message}");
            return 0;
        }
    }

    public async Task<long> DirectDownloadFile(Uri fileUri, string filePath, Downloader.DownloadProgressCallback? progressCallback = null)
    {
        Logger.Info($"Attempting direct download: {fileUri} -> {filePath}");

        var tempFilePath = filePath + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Logger.Info($"Created directory: {directory}");
            }

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LoE-Launcher/1.0");

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var response = await client.GetAsync(
                fileUri,
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Error($"Server returned error status: {response.StatusCode}");
                return 0;
            }

            var contentLength = response.Content.Headers.ContentLength ?? -1;
            Logger.Debug($"Content length: {contentLength} bytes");

            await using var contentStream = await response.Content.ReadAsStreamAsync(cts.Token);

            long totalBytesRead = 0;
            var startTime = DateTime.UtcNow;
            var lastProgressReport = DateTime.UtcNow;

            Logger.Debug($"Creating temporary file: {tempFilePath}");
            await using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                var buffer = new byte[81920];

                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, cts.Token)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token);
                    totalBytesRead += bytesRead;

                    progressCallback?.Invoke(bytesRead);

                    var now = DateTime.UtcNow;
                    if ((now - lastProgressReport).TotalSeconds >= 2)
                    {
                        var progressElapsed = (now - startTime).TotalSeconds;
                        var bytesPerSecond = totalBytesRead / progressElapsed;
                        var percentComplete = contentLength > 0 ? (totalBytesRead * 100.0 / contentLength) : 0;

                        Logger.Info($"Download progress: {totalBytesRead:N0}/{contentLength:N0} bytes ({percentComplete:F1}%) - {bytesPerSecond:N0} bytes/sec");
                        lastProgressReport = now;
                    }
                }

                Logger.Info("Download completed, closing stream");
                await fileStream.FlushAsync(cts.Token);
            }

            var actualTargetPath = await fileOps.GetSafeTargetPathAsync(filePath);

            Logger.Debug($"Moving temp file to destination: {tempFilePath} -> {actualTargetPath}");
            File.Move(tempFilePath, actualTargetPath, true);

            if (actualTargetPath != filePath)
            {
                Logger.Info($"Used alternative path due to file lock. Original: {filePath}, Actual: {actualTargetPath}");
            }

            var totalElapsed = (DateTime.UtcNow - startTime).TotalSeconds;
            var overallSpeed = totalBytesRead / totalElapsed;
            Logger.Info($"Direct download complete: {totalBytesRead:N0} bytes in {totalElapsed:F1} seconds ({overallSpeed:N0} bytes/sec)");

            return totalBytesRead;
        }
        catch (TaskCanceledException tcex)
        {
            Logger.Error(tcex, "Direct download timed out or was canceled");
            await fileOps.CleanupDownloadFiles(tempFilePath, filePath);
            return 0;
        }
        catch (HttpRequestException hrex)
        {
            Logger.Error(hrex, $"HTTP error during direct download: {hrex.Message}");
            await fileOps.CleanupDownloadFiles(tempFilePath, filePath);
            return 0;
        }
        catch (IOException ioex)
        {
            Logger.Error(ioex, $"IO error during direct download: {ioex.Message}");

            if (ioex.Message.Contains("being used by another process"))
            {
                if (File.Exists(tempFilePath))
                {
                    var fileInfo = new FileInfo(tempFilePath);
                    if (fileInfo.Length > 0)
                    {
                        try
                        {
                            var alternativePath = await fileOps.GetSafeTargetPathAsync(filePath + ".alt");
                            Logger.Info($"Attempting to save to alternative location due to lock: {alternativePath}");
                            File.Move(tempFilePath, alternativePath, true);

                            return fileInfo.Length;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "Failed to use alternative path after lock");
                        }
                    }
                }
            }

            await fileOps.CleanupDownloadFiles(tempFilePath, filePath);
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Direct download failed: {ex.Message}");
            await fileOps.CleanupDownloadFiles(tempFilePath, filePath);
            return 0;
        }
    }

    public async Task<bool> CheckConnectivity(Uri testUrl)
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LoE-Launcher/1.0");

            using var response = await client.GetAsync(testUrl, HttpCompletionOption.ResponseHeadersRead);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Debug($"Connectivity check failed: {ex.Message}");
            return false;
        }
    }
}
