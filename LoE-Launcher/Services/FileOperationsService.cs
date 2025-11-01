using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Models.Paths;
using NLog;

namespace LoE_Launcher.Services;

public class FileOperationsService(string launcherPath)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const int MaxFileCheckRetries = 10;
    private const int InitialFileCheckDelayMs = 100;

    public async Task HandlePendingDeletions()
    {
        var pendingDeleteFile = Path.Combine(launcherPath, "pending_delete.txt");
        if (!File.Exists(pendingDeleteFile))
        {
            return;
        }

        try
        {
            var filesToDelete = await File.ReadAllLinesAsync(pendingDeleteFile);
            var successfulDeletions = new List<string>();

            foreach (var file in filesToDelete)
            {
                if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
                {
                    successfulDeletions.Add(file);
                    continue;
                }

                try
                {
                    if (await IsFileDeleteableAsync(file, 3))
                    {
                        File.Delete(file);
                        successfulDeletions.Add(file);
                        Logger.Info($"Deleted pending file: {file}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Failed to delete pending file: {file}");
                }
            }

            if (successfulDeletions.Count > 0)
            {
                var remainingFiles = filesToDelete.Except(successfulDeletions).ToArray();
                if (remainingFiles.Length > 0)
                {
                    File.WriteAllLines(pendingDeleteFile, remainingFiles);
                }
                else
                {
                    File.Delete(pendingDeleteFile);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling pending deletions");
        }
    }

    public async Task<bool> IsFileDeleteableAsync(string filePath, int maxRetries = 5, int initialDelayMs = 100)
    {
        if (!File.Exists(filePath))
        {
            return true;
        }

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);

                var tempDeleteTest = filePath + ".deletetest";
                try
                {
                    await File.Create(tempDeleteTest).DisposeAsync();
                    File.Delete(tempDeleteTest);
                    return true;
                }
                catch
                {
                    if (i == maxRetries - 1)
                    {
                        Logger.Warn($"Unable to create test file near {filePath}");
                        return false;
                    }
                }
            }
            catch (IOException ex) when (ex.Message.Contains("being used by another process"))
            {
                var delay = initialDelayMs * (1 << Math.Min(i, 10));
                Logger.Debug($"File locked for deletion, retry {i + 1}/{maxRetries} in {delay}ms: {filePath}");
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error checking if file is deleteable: {filePath}");
                return false;
            }
        }

        return false;
    }

    public async Task<string> GetSafeTargetPathAsync(string filePath)
    {
        if (!File.Exists(filePath) || await IsFileDeleteableAsync(filePath))
        {
            return filePath;
        }

        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        for (var i = 1; i <= 10; i++)
        {
            var alternativePath = Path.Combine(directory, $"{fileName}.new{i}{extension}");
            if (!File.Exists(alternativePath))
            {
                Logger.Info($"Using alternative path: {alternativePath}");
                return alternativePath;
            }
        }

        var guidPath = Path.Combine(directory, $"{fileName}.{Guid.NewGuid()}{extension}");
        Logger.Info($"Using GUID alternative path: {guidPath}");
        return guidPath;
    }

    public void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public void DeleteFileIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch
            {
                // Ignore deletion failures
            }
        }
    }

    public async Task<bool> UnzipFile(IAbsoluteFilePath file)
    {
        var outputFile = file.GetBrotherFileWithName(file.FileNameWithoutExtension).Path;
        if (!await FileIsReadyAsync(file.Path, MaxFileCheckRetries, InitialFileCheckDelayMs))
        {
            Logger.Error($"File not ready for unzipping after {MaxFileCheckRetries} attempts: {file.Path}");
            throw new IOException($"File locked: {file.Path}");
        }

        try
        {
            if (IsGzipFile(file.Path))
            {
                await using (var inputStream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                await using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
                await using (var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await gzipStream.CopyToAsync(outputStream);
                    await outputStream.FlushAsync();
                }

                var extractedSize = new FileInfo(outputFile).Length;
                var originalSize = new FileInfo(file.Path).Length;
                Logger.Info($"Unzipped file: {file.Path}, compressed size: {originalSize}, extracted size: {extractedSize}");
            }
            else
            {
                Logger.Info($"File is not GZip, copying directly: {file.Path}");
                File.Copy(file.Path, outputFile, true);
                Logger.Info($"Successfully copied: {file.Path} to {outputFile}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error unzipping file {file.Path}");
            return false;
        }
    }

    public bool IsGzipFile(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length < 2)
            {
                return false;
            }

            var header = new byte[2];
            fs.ReadExactly(header, 0, 2);
            return header[0] == 0x1F && header[1] == 0x8B;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error checking if file is GZip: {filePath}");
            return false;
        }
    }

    public async Task<bool> FileIsReadyAsync(string filePath, int maxRetries = 10, int initialDelayMs = 100)
    {
        if (!File.Exists(filePath))
        {
            Logger.Warn($"File does not exist: {filePath}");
            return false;
        }

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length > 0)
                {
                    var buffer = new byte[Math.Min(1024, fs.Length)];
                    await fs.ReadExactlyAsync(buffer, 0, buffer.Length);
                }

                return true;
            }
            catch (IOException ex) when (ex.Message.Contains("being used by another process"))
            {
                var delay = initialDelayMs * (1 << Math.Min(i, 10));
                Logger.Debug($"File locked, retry {i + 1}/{maxRetries} in {delay}ms: {filePath}");
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error checking if file is ready: {filePath}");
                return false;
            }
        }
        return false;
    }

    public async Task CompressOriginalFile(IAbsoluteFilePath realFile)
    {
        if (realFile.Exists)
        {
            await FileIsReadyAsync(realFile.Path, maxRetries: 6);
            var compressedFile = realFile.GetBrotherFileWithName(realFile.FileName + ".jar");

            await using var inputStream = new FileStream(realFile.Path, FileMode.Open);
            await using var outputStream = new FileStream(compressedFile.Path, FileMode.Create);
            await using var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal);

            await inputStream.CopyToAsync(gzipStream);
        }
    }

    public void CleanGameFolder(IAbsoluteDirectoryPath gameInstallFolder)
    {
        try
        {
            if (!gameInstallFolder.Exists)
            {
                Directory.CreateDirectory(gameInstallFolder.ToString());
            }

            var extensions = new[] { "*.zsync", "*.jar", "*.gz", "*.zs-old" };
            foreach (var extension in extensions)
            {
                foreach (var file in gameInstallFolder.DirectoryInfo.EnumerateFiles(extension, SearchOption.AllDirectories))
                {
                    try
                    {
                        file.Delete();
                    }
                    catch (IOException ex) when (ex.Message.Contains("being used by another process"))
                    {
                        Logger.Warn($"Could not delete {file.FullName}: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Error deleting {file.FullName}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "CleanGameFolder failed");
            throw;
        }
    }

    public async Task CleanupDownloadFiles(string tempFilePath, string filePath)
    {
        if (File.Exists(tempFilePath))
        {
            try
            {
                Logger.Info($"Deleting temporary file: {tempFilePath}");

                if (await IsFileDeleteableAsync(tempFilePath, 5))
                {
                    File.Delete(tempFilePath);
                }
                else
                {
                    Logger.Warn($"Unable to delete locked temporary file: {tempFilePath}");
                    try
                    {
                        var pendingDeleteFile = Path.Combine(
                            Path.GetDirectoryName(tempFilePath),
                            "pending_delete.txt");

                        await File.AppendAllLinesAsync(pendingDeleteFile, new[] { tempFilePath });
                    }
                    catch
                    {
                        Logger.Warn($"Failed to schedule {tempFilePath} for later deletion");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to delete temporary file: {tempFilePath}");
            }
        }

        if (File.Exists(filePath) && File.Exists(tempFilePath) && new FileInfo(tempFilePath).Length > 0)
        {
            try
            {
                Logger.Info($"Deleting target file: {filePath}");

                if (await IsFileDeleteableAsync(filePath, 5))
                {
                    File.Delete(filePath);
                }
                else
                {
                    Logger.Warn($"Unable to delete locked target file: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to delete target file: {filePath}");
            }
        }
    }
}
