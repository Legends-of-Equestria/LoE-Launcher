using Avalonia.Media.Imaging;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace LoE_Launcher.Utils;

public static class ImageHelper
{
    private static readonly HttpClient httpClient = new HttpClient();

    public static async Task<Bitmap> LoadFromWeb(string url)
    {
        try
        {
            using (var response = await httpClient.GetAsync(url))
            {
                response.EnsureSuccessStatusCode();
                    
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var memoryStream = new MemoryStream())
                {
                    await stream.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                        
                    return new Bitmap(memoryStream);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load image from {url}: {ex.Message}");
            return null;
        }
    }
}