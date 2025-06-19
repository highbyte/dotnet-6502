
namespace Highbyte.DotNet6502.App.SadConsole;
public static class LabROMDownloader
{
    public static readonly string ArtifactPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "artifacts");

    static LabROMDownloader()
    {
        if (!Directory.Exists(ArtifactPath))
        {
            Directory.CreateDirectory(ArtifactPath);
        }
    }

    public static async Task DownloadC64RomsAsync(string[] romUrls)
    {
        using var httpClient = new HttpClient();
        //httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");

        foreach (var url in romUrls)
        {
            var filename = Path.GetFileName(new Uri(url).LocalPath);
            var dest = Path.Combine(ArtifactPath, filename);
            try
            {
                using var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Failed to get '{url}' ({(int)response.StatusCode})");
                await using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
                System.Console.WriteLine($"Downloaded {filename} to {dest}");
            }
            catch (Exception ex)
            {
                if (File.Exists(dest))
                    File.Delete(dest);
                throw new Exception($"Error downloading {url}: {ex.Message}", ex);
            }
        }
    }
}
