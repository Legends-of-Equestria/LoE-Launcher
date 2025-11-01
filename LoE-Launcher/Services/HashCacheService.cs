using System;
using System.Collections.Generic;
using System.IO;
using Models.Paths.Json;
using Newtonsoft.Json;
using NLog;

namespace LoE_Launcher.Services;

public class HashCacheService(string cacheDirectory)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public Dictionary<string, FileHashCache> LoadHashCache()
    {
        try
        {
            if (!Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            var cacheFile = Path.Combine(cacheDirectory, "hash_cache.json");
            if (File.Exists(cacheFile))
            {
                var json = File.ReadAllText(cacheFile);
                return JsonConvert.DeserializeObject<Dictionary<string, FileHashCache>>(json)
                    ?? new Dictionary<string, FileHashCache>();
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to load hash cache, creating new one");
        }

        return new Dictionary<string, FileHashCache>();
    }

    public void SaveHashCache(Dictionary<string, FileHashCache> cache)
    {
        try
        {
            if (!Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            var cacheFile = Path.Combine(cacheDirectory, "hash_cache.json");
            var json = JsonConvert.SerializeObject(cache);
            File.WriteAllText(cacheFile, json);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to save hash cache");
        }
    }

    public void ClearHashCache()
    {
        try
        {
            var cacheFile = Path.Combine(cacheDirectory, "hash_cache.json");
            if (File.Exists(cacheFile))
            {
                File.Delete(cacheFile);
                Logger.Info("Deleted hash cache");
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to clear hash cache");
        }
    }

    public bool TryGetCachedHash(string filePath, FileInfo fileInfo, Dictionary<string, FileHashCache> hashCache, out string hash)
    {
        hash = string.Empty;

        if (hashCache.TryGetValue(filePath, out var cachedInfo) &&
            fileInfo.LastWriteTimeUtc == cachedInfo.LastModifiedUtc &&
            fileInfo.Length == cachedInfo.FileSize)
        {
            hash = cachedInfo.Hash;
            return true;
        }

        return false;
    }

    public void UpdateCacheEntry(string filePath, string hash, FileInfo fileInfo, Dictionary<string, FileHashCache> cache)
    {
        cache[filePath] = new FileHashCache
        {
            FilePath = filePath,
            Hash = hash,
            LastModifiedUtc = fileInfo.LastWriteTimeUtc,
            FileSize = fileInfo.Length
        };
    }
}

public class FileHashCache
{
    public string FilePath { get; set; }
    public string Hash { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public long FileSize { get; set; }
}
