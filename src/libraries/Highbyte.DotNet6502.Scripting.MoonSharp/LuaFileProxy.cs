namespace Highbyte.DotNet6502.Scripting.MoonSharp;

/// <summary>
/// Provides sandboxed file I/O operations for Lua scripts via the <c>file</c> global.
/// All paths are resolved relative to a configured base directory; traversal outside it is blocked.
/// Write operations are further gated by <c>AllowFileWrite</c> in <see cref="Highbyte.DotNet6502.Systems.ScriptingConfig"/>.
/// </summary>
public class LuaFileProxy
{
    private readonly string _baseDirectory;
    private readonly bool _allowWrite;

    internal LuaFileProxy(string baseDirectory, bool allowWrite)
    {
        _baseDirectory = Path.GetFullPath(baseDirectory);
        _allowWrite = allowWrite;
    }

    /// <summary>
    /// Returns the fully-resolved path if <paramref name="filename"/> is safe (within base directory),
    /// or <c>null</c> if the filename is null/empty or would escape the base directory.
    /// </summary>
    internal string? GetSafePath(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return null;

        string resolved = Path.GetFullPath(Path.Combine(_baseDirectory, filename));

        // Trailing separator check prevents matching adjacent directories:
        // base="/scripts" must not match resolved="/scripts-evil/foo"
        string baseWithSep = _baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                           + Path.DirectorySeparatorChar;

        return resolved.StartsWith(baseWithSep, StringComparison.OrdinalIgnoreCase) ? resolved : null;
    }

    internal void ThrowIfWriteDisabled(string op)
    {
        if (!_allowWrite)
            throw new InvalidOperationException(
                $"file.{op}() requires AllowFileWrite: true in scripting config.");
    }

    /// <summary>Reads the entire text content of a file. Returns nil if the file does not exist or the path is unsafe.</summary>
    public string? Read(string filename)
    {
        var path = GetSafePath(filename);
        return path != null && File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>Reads a file as raw bytes. Returns nil if the file does not exist or the path is unsafe.</summary>
    public byte[]? ReadBytes(string filename)
    {
        var path = GetSafePath(filename);
        return path != null && File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    /// <summary>Writes (overwrites) a text file. Requires AllowFileWrite: true.</summary>
    public void Write(string filename, string content)
    {
        ThrowIfWriteDisabled("write");
        var path = GetSafePath(filename) ?? throw new ArgumentException($"Unsafe or invalid filename: {filename}");
        File.WriteAllText(path, content);
    }

    /// <summary>Appends text to a file (creates it if it does not exist). Requires AllowFileWrite: true.</summary>
    public void Append(string filename, string content)
    {
        ThrowIfWriteDisabled("append");
        var path = GetSafePath(filename) ?? throw new ArgumentException($"Unsafe or invalid filename: {filename}");
        File.AppendAllText(path, content);
    }

    /// <summary>Returns true if a file exists at the given path within the base directory.</summary>
    public bool Exists(string filename) => GetSafePath(filename) is { } p && File.Exists(p);

    /// <summary>
    /// Enumerates filenames in the base directory matching an optional glob pattern.
    /// Returns only the filename component (not the full path).
    /// </summary>
    public IEnumerable<string> List(string pattern = "*")
    {
        if (!Directory.Exists(_baseDirectory))
            return [];
        return Directory.EnumerateFiles(_baseDirectory, pattern, SearchOption.TopDirectoryOnly)
                        .Select(Path.GetFileName)
                        .OfType<string>();
    }

    /// <summary>Deletes a file. Requires AllowFileWrite: true. No-op if the file does not exist.</summary>
    public void Delete(string filename)
    {
        ThrowIfWriteDisabled("delete");
        var path = GetSafePath(filename) ?? throw new ArgumentException($"Unsafe or invalid filename: {filename}");
        if (File.Exists(path))
            File.Delete(path);
    }
}
