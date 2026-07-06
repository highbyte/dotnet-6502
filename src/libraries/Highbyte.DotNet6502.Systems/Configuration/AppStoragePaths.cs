using System.Text.Json;
using System.Text.Json.Nodes;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Systems.Configuration;

public static class AppStoragePaths
{
    public const string CompanyFolderName = "Highbyte";
    public const string AppFolderName = "DotNet6502";
    public const string UserSettingsFileName = "appsettings.user.json";

    public static string GetUserSettingsDirectory(string hostName)
    {
        if (string.IsNullOrWhiteSpace(hostName))
            throw new ArgumentException("Host name must be specified.", nameof(hostName));

        return Path.Combine(GetSpecialFolderPath(Environment.SpecialFolder.LocalApplicationData), CompanyFolderName, AppFolderName, hostName);
    }

    public static string GetUserSettingsFilePath(string hostName)
        => Path.Combine(GetUserSettingsDirectory(hostName), UserSettingsFileName);

    public static string GetUserContentRoot()
        => Path.Combine(GetSpecialFolderPath(Environment.SpecialFolder.MyDocuments), CompanyFolderName, AppFolderName);

    public static IEnumerable<string> GetSharedUserContentDirectories()
    {
        yield return GetUserContentRoot();
        yield return Path.Combine(GetUserContentRoot(), "roms");
        yield return GetScriptsDirectory();
        yield return GetSnapshotsDirectory();
    }

    public static string GetRomDirectory(string systemName)
    {
        if (string.IsNullOrWhiteSpace(systemName))
            throw new ArgumentException("System name must be specified.", nameof(systemName));

        return Path.Combine(GetUserContentRoot(), "roms", systemName);
    }

    public static string GetScriptsDirectory()
        => Path.Combine(GetUserContentRoot(), "scripts");

    public static string GetSnapshotsDirectory()
        => Path.Combine(GetUserContentRoot(), "snapshots");

    public static string ResolveSnapshotFilePath(string snapshotPath)
    {
        if (string.IsNullOrWhiteSpace(snapshotPath))
            throw new ArgumentException("Snapshot path must be specified.", nameof(snapshotPath));

        var expandedPath = PathHelper.ExpandOSEnvironmentVariables(snapshotPath);
        if (Path.IsPathRooted(expandedPath))
            return Path.GetFullPath(expandedPath);

        var snapshotDirectory = GetSnapshotsDirectory();
        var resolvedPath = Path.GetFullPath(Path.Combine(snapshotDirectory, expandedPath));
        var snapshotDirectoryWithSeparator = Path.GetFullPath(snapshotDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!resolvedPath.StartsWith(snapshotDirectoryWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Relative snapshot path escapes the snapshot directory: {snapshotPath}", nameof(snapshotPath));

        return resolvedPath;
    }

    /// <summary>
    /// Root directory for regenerable, machine-local cache data. Anchored at
    /// <see cref="Environment.SpecialFolder.LocalApplicationData"/> (not <c>MyDocuments</c>) because
    /// cache is not user-edited content and should stay out of the user's documents / backup / sync.
    /// Host-agnostic (no per-host segment): cached content is equally valid for any desktop host.
    /// </summary>
    public static string GetCacheRoot()
        => Path.Combine(GetSpecialFolderPath(Environment.SpecialFolder.LocalApplicationData), CompanyFolderName, AppFolderName, "cache");

    /// <summary>
    /// Directory holding the read-through cache of auto-downloaded content (C64 <c>.d64</c>/<c>.prg</c>).
    /// See <c>Highbyte.DotNet6502.Systems.Caching.FileDownloadCache</c>.
    /// </summary>
    public static string GetDownloadCacheDirectory()
        => Path.Combine(GetCacheRoot(), "downloads");

    private static string GetSpecialFolderPath(Environment.SpecialFolder specialFolder)
    {
        var path = Environment.GetFolderPath(specialFolder);
        if (!string.IsNullOrWhiteSpace(path))
            return path;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            home = Environment.GetEnvironmentVariable("HOME") ?? AppContext.BaseDirectory;

        return specialFolder switch
        {
            Environment.SpecialFolder.LocalApplicationData => Path.Combine(home, ".local", "share"),
            Environment.SpecialFolder.MyDocuments => Path.Combine(home, "Documents"),
            _ => home
        };
    }
}

public static class AppSettingsUserFile
{
    private static readonly JsonSerializerOptions s_writeOptions = new()
    {
        WriteIndented = true
    };

    public static async Task MergeSectionAsync(string userSettingsFilePath, string sectionName, string sectionJson)
    {
        if (string.IsNullOrWhiteSpace(userSettingsFilePath))
            throw new ArgumentException("User settings file path must be specified.", nameof(userSettingsFilePath));
        if (string.IsNullOrWhiteSpace(sectionName))
            throw new ArgumentException("Section name must be specified.", nameof(sectionName));
        if (string.IsNullOrWhiteSpace(sectionJson))
            throw new ArgumentException("Section JSON must be specified.", nameof(sectionJson));

        var root = await LoadRootAsync(userSettingsFilePath);
        var section = JsonNode.Parse(sectionJson)
            ?? throw new JsonException($"Configuration section '{sectionName}' is empty.");

        if (root[sectionName] is JsonObject existingSection && section is JsonObject incomingSection)
            MergeObject(existingSection, incomingSection);
        else
            root[sectionName] = section;

        await WriteAtomicAsync(userSettingsFilePath, root.ToJsonString(s_writeOptions));
    }

    private static async Task<JsonObject> LoadRootAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return new JsonObject();

        var json = await File.ReadAllTextAsync(filePath);
        if (string.IsNullOrWhiteSpace(json))
            return new JsonObject();

        return JsonNode.Parse(json) as JsonObject
            ?? throw new JsonException($"User settings file must contain a JSON object: {filePath}");
    }

    private static void MergeObject(JsonObject target, JsonObject source)
    {
        foreach (var property in source)
        {
            if (target[property.Key] is JsonObject targetObject && property.Value is JsonObject sourceObject)
            {
                MergeObject(targetObject, sourceObject);
                continue;
            }

            target[property.Key] = property.Value?.DeepClone();
        }
    }

    private static async Task WriteAtomicAsync(string filePath, string json)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException($"Cannot determine directory for {filePath}.");
        Directory.CreateDirectory(directory);

        var tempFile = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(tempFile, json);
            File.Move(tempFile, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
