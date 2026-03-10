using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.Scripting.MoonSharp;

/// <summary>
/// File-system-backed implementation of <see cref="IScriptStore"/>.
/// Each key maps to a file inside <paramref name="directory"/>.
/// Keys must be valid, non-path-traversing filenames.
/// </summary>
public sealed class FileSystemScriptStore : IScriptStore
{
    private readonly string _directory;

    public FileSystemScriptStore(string directory)
    {
        _directory = Path.GetFullPath(directory);
    }

    private string? GetSafePath(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        if (key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
        // Block path-traversal attempts even if not caught by GetInvalidFileNameChars
        if (key.Contains('/') || key.Contains('\\') || key == ".." || key == ".") return null;
        return Path.Combine(_directory, key);
    }

    public string? Get(string key)
    {
        var path = GetSafePath(key);
        return path != null && File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public void Set(string key, string value)
    {
        var path = GetSafePath(key) ?? throw new ArgumentException($"Invalid store key: {key}");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, value);
    }

    public void Delete(string key)
    {
        var path = GetSafePath(key) ?? throw new ArgumentException($"Invalid store key: {key}");
        if (File.Exists(path)) File.Delete(path);
    }

    public IReadOnlyList<string> List()
    {
        if (!Directory.Exists(_directory)) return [];
        return Directory.EnumerateFiles(_directory, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToList();
    }

    public bool Exists(string key)
    {
        var path = GetSafePath(key);
        return path != null && File.Exists(path);
    }
}
