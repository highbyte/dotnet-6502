using System.Text.Json;
using Highbyte.DotNet6502.Systems.Configuration;

namespace Highbyte.DotNet6502.Updates;

/// <summary>Persisted state from the last update check: timestamp (for cadence), ETag, and the last seen release.</summary>
public sealed record UpdateCheckCacheData
{
    public DateTimeOffset LastCheckUtc { get; init; }
    public string? ETag { get; init; }
    public string? LatestTag { get; init; }
    public string? LatestReleaseUrl { get; init; }
}

/// <summary>
/// Reads/writes the update-check cache as a small JSON file under the machine-local cache root
/// (<see cref="AppStoragePaths.GetCacheRoot"/>), host-agnostic. Best-effort: any IO/parse failure
/// is swallowed so a corrupt or unwritable cache never breaks the app or the check.
/// </summary>
public sealed class UpdateCheckCache
{
    private readonly string _filePath;

    public UpdateCheckCache(string? filePath = null)
    {
        _filePath = filePath ?? DefaultFilePath();
    }

    public static string DefaultFilePath()
        => Path.Combine(AppStoragePaths.GetCacheRoot(), "updates", "update-check.json");

    public UpdateCheckCacheData? Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return null;
            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
                return null;
            return JsonSerializer.Deserialize(json, UpdatesJsonContext.Default.UpdateCheckCacheData);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void Save(UpdateCheckCacheData data)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(data, UpdatesJsonContext.Default.UpdateCheckCacheData);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a failed cache write must not surface to the user.
        }
    }
}
