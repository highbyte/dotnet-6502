
using Microsoft.Extensions.Logging;
using System.IO.Compression;


namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64;

public class D64Downloader
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly string? _corsProxyUrl;

    public D64Downloader(
        ILoggerFactory loggerFactory,
        HttpClient httpClient,
        string? corsProxyUrl = null)
    {
        _logger = loggerFactory.CreateLogger(typeof(D64Downloader).Name);
        _httpClient = httpClient;
        _corsProxyUrl = corsProxyUrl;
    }


    /// <summary>
    /// Downloads and processes a disk image file (supports both .d64 and .zip files)
    /// </summary>
    /// <param name="diskInfo">Information about the disk to download</param>
    /// <returns>The .d64 file content as byte array</returns>
    public async Task<byte[]> DownloadAndProcessDiskImage(D64DownloadDiskInfo diskInfo)
    {
        _logger.LogInformation($"Downloading disk image: {diskInfo.DisplayName} from {diskInfo.DownloadUrl}");

        // Use CORS proxy to bypass browser CORS restrictions
        var downloadUrl = _corsProxyUrl != null ?
            _corsProxyUrl + Uri.EscapeDataString(diskInfo.DownloadUrl)
            : diskInfo.DownloadUrl;

        _logger.LogInformation($"Using download URL: {downloadUrl}");

        // Check the download type to determine download strategy
        if (diskInfo.DownloadType == DownloadType.ZIP)
        {
            _logger.LogInformation("Processing ZIP file to extract .d64");
            return await DownloadAndExtractZipD64(downloadUrl);
        }
        else
        {
            // Download direct .d64 file
            using var response = await _httpClient.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();
            var d64Bytes = await response.Content.ReadAsByteArrayAsync();
            _logger.LogInformation($"Downloaded .d64 file: {d64Bytes.Length} bytes");
            return d64Bytes;
        }
    }

    /// <summary>
    /// Downloads a ZIP file and extracts the first .d64 file in a memory-efficient way
    /// </summary>
    /// <param name="url">The URL to download the ZIP from</param>
    /// <returns>The .d64 file content as byte array</returns>
    private async Task<byte[]> DownloadAndExtractZipD64(string url)
    {
        using var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        // Use the response stream directly instead of loading everything into memory
        using var responseStream = await response.Content.ReadAsStreamAsync();
        using var archive = new ZipArchive(responseStream, ZipArchiveMode.Read);

        // Find the first .d64 file in the archive
        var d64Entry = archive.Entries.FirstOrDefault(entry => entry.Name.EndsWith(".d64", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("No .d64 file found in the ZIP archive");

        _logger.LogInformation($"Found .d64 file in ZIP: {d64Entry.Name}, size: {d64Entry.Length} bytes");

        // Pre-allocate array with the exact size
        var d64Bytes = new byte[d64Entry.Length];

        // Extract the .d64 file content directly
        using var entryStream = d64Entry.Open();
        int totalBytesRead = 0;
        int bytesRead;

        // Read in chunks to be memory-efficient
        while (totalBytesRead < d64Bytes.Length &&
               (bytesRead = await entryStream.ReadAsync(d64Bytes, totalBytesRead, d64Bytes.Length - totalBytesRead)) > 0)
        {
            totalBytesRead += bytesRead;
        }

        _logger.LogInformation($"Extracted .d64 file: {d64Bytes.Length} bytes");
        return d64Bytes;
    }
}

public class D64DownloadDiskInfo
{
    public string DisplayName { get; set; }
    public string DownloadUrl { get; set; }
    public List<string> RunCommands { get; set; }
    public DownloadType DownloadType { get; set; }
    public bool KeyboardJoystickEnabled { get; set; }
    public int KeyboardJoystickNumber { get; set; }
    public bool RequiresBitmap { get; set; }
    public bool AudioEnabled { get; set; }

    public D64DownloadDiskInfo(string displayName, string downloadUrl, List<string>? runCommands = null, DownloadType downloadType = DownloadType.D64, bool keyboardJoystickEnabled = false, int keyboardJoystickNumber = 2, bool requiresBitmap = false, bool audioEnabled = false)
    {
        DisplayName = displayName;
        DownloadUrl = downloadUrl;
        RunCommands = runCommands ?? new List<string> { "load\"*\",8,1", "run" };
        DownloadType = downloadType;
        KeyboardJoystickEnabled = keyboardJoystickEnabled;
        KeyboardJoystickNumber = keyboardJoystickNumber;
        RequiresBitmap = requiresBitmap;
        AudioEnabled = audioEnabled;
    }
}

public enum DownloadType
{
    /// <summary>
    /// A .d64 file.
    /// </summary>
    D64,

    /// <summary>
    /// A .zip file that contains a .d64 file.
    /// </summary>
    ZIP
}
