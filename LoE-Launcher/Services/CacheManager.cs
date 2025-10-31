using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace LoE_Launcher.Services;

public class CacheManager(string cacheDirectory)
{
    public async Task<Bitmap?> LoadCachedImageImmediately(string cacheFileName)
    {
        var cachePath = Path.Combine(cacheDirectory, cacheFileName);

        if (File.Exists(cachePath))
        {
            try
            {
                await using var fileReader = File.OpenRead(cachePath);
                return new Bitmap(fileReader);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    public async Task<Bitmap?> UpdateCachedImage(string url, string cacheFileName)
    {
        Directory.CreateDirectory(cacheDirectory);
        var cachePath = Path.Combine(cacheDirectory, cacheFileName);
        var tempPath = Path.Combine(cacheDirectory, $"temp_{cacheFileName}");

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            if (File.Exists(cachePath))
            {
                var lastModified = File.GetLastWriteTimeUtc(cachePath);
                client.DefaultRequestHeaders.IfModifiedSince = lastModified;
            }

            using var response = await client.GetAsync(url);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var memoryStream = new MemoryStream();

            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            await using (var fileStream = File.Create(tempPath))
            {
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(fileStream);
            }

            File.Move(tempPath, cachePath, true);

            memoryStream.Position = 0;
            return new Bitmap(memoryStream);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // ignore
                }
            }

            return null;
        }
    }

    public async Task<string?> LoadCachedTextImmediately(string cacheFileName)
    {
        var cachePath = Path.Combine(cacheDirectory, cacheFileName);

        if (File.Exists(cachePath))
        {
            try
            {
                return await File.ReadAllTextAsync(cachePath);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    public async Task<string?> UpdateCachedText(string url, string cacheFileName)
    {
        Directory.CreateDirectory(cacheDirectory);
        var cachePath = Path.Combine(cacheDirectory, cacheFileName);
        var tempPath = Path.Combine(cacheDirectory, $"temp_{cacheFileName}");

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            if (File.Exists(cachePath))
            {
                var lastModified = File.GetLastWriteTimeUtc(cachePath);
                client.DefaultRequestHeaders.IfModifiedSince = lastModified;
            }

            using var response = await client.GetAsync(url);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();

            await File.WriteAllTextAsync(tempPath, content);
            File.Move(tempPath, cachePath, true);

            return content;
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // ignore
                }
            }

            return null;
        }
    }
}
