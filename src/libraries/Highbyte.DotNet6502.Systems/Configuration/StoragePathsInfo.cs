using System.Text;
using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Systems.Configuration;

public sealed class StoragePathsInfo
{
    [JsonPropertyName("hostName")]
    public string HostName { get; init; } = string.Empty;

    [JsonPropertyName("userContentRoot")]
    public string UserContentRoot { get; init; } = string.Empty;

    [JsonPropertyName("scriptsDirectory")]
    public string ScriptsDirectory { get; init; } = string.Empty;

    [JsonPropertyName("snapshotsDirectory")]
    public string SnapshotsDirectory { get; init; } = string.Empty;

    [JsonPropertyName("userSettingsFile")]
    public string UserSettingsFile { get; init; } = string.Empty;

    [JsonPropertyName("cacheRoot")]
    public string CacheRoot { get; init; } = string.Empty;

    [JsonPropertyName("downloadCacheDirectory")]
    public string DownloadCacheDirectory { get; init; } = string.Empty;

    [JsonPropertyName("systems")]
    public IReadOnlyList<SystemStoragePathsInfo> Systems { get; init; } = Array.Empty<SystemStoragePathsInfo>();
}

public sealed class SystemStoragePathsInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("romDirectory")]
    public string? RomDirectory { get; init; }
}

public static class StoragePathsInfoFactory
{
    public static async Task<StoragePathsInfo> CreateAsync(
        string hostName,
        string userSettingsFile,
        SystemList systemList,
        ScriptingConfig? scriptingConfig = null)
    {
        var systems = new List<SystemStoragePathsInfo>();
        foreach (var systemName in systemList.Systems.Order(StringComparer.OrdinalIgnoreCase))
        {
            var hostSystemConfig = await systemList.GetHostSystemConfig(systemName).ConfigureAwait(false);
            systems.Add(new SystemStoragePathsInfo
            {
                Name = systemName,
                RomDirectory = GetEffectiveRomDirectory(hostSystemConfig.SystemConfig)
            });
        }

        return new StoragePathsInfo
        {
            HostName = hostName,
            UserContentRoot = PathHelper.ExpandOSEnvironmentVariables(AppStoragePaths.GetUserContentRoot()),
            ScriptsDirectory = scriptingConfig?.ResolvedScriptDirectory()
                ?? PathHelper.ExpandOSEnvironmentVariables(AppStoragePaths.GetScriptsDirectory()),
            SnapshotsDirectory = PathHelper.ExpandOSEnvironmentVariables(AppStoragePaths.GetSnapshotsDirectory()),
            UserSettingsFile = PathHelper.ExpandOSEnvironmentVariables(userSettingsFile),
            CacheRoot = PathHelper.ExpandOSEnvironmentVariables(AppStoragePaths.GetCacheRoot()),
            DownloadCacheDirectory = PathHelper.ExpandOSEnvironmentVariables(AppStoragePaths.GetDownloadCacheDirectory()),
            Systems = systems,
        };
    }

    public static string FormatForConsole(StoragePathsInfo paths)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Host: {paths.HostName}");
        sb.AppendLine($"User content root: {paths.UserContentRoot}");
        sb.AppendLine($"Scripts directory: {paths.ScriptsDirectory}");
        sb.AppendLine($"Snapshots directory: {paths.SnapshotsDirectory}");
        sb.AppendLine($"User settings file: {paths.UserSettingsFile}");
        sb.AppendLine($"Cache root: {paths.CacheRoot}");
        sb.AppendLine($"Download cache directory: {paths.DownloadCacheDirectory}");
        if (paths.Systems.Count > 0)
        {
            sb.AppendLine("Systems:");
            foreach (var system in paths.Systems)
            {
                var romDirectory = string.IsNullOrWhiteSpace(system.RomDirectory)
                    ? "(none)"
                    : system.RomDirectory;
                sb.AppendLine($"  {system.Name} ROM directory: {romDirectory}");
            }
        }
        return sb.ToString();
    }

    private static string? GetEffectiveRomDirectory(ISystemConfig systemConfig)
    {
        var property = systemConfig.GetType().GetProperty("EffectiveROMDirectory");
        var value = property?.GetValue(systemConfig) as string;
        return string.IsNullOrWhiteSpace(value)
            ? null
            : PathHelper.ExpandOSEnvironmentVariables(value);
    }
}
