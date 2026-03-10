namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Cross-platform key/value store accessible from Lua scripts via the <c>store</c> global.
/// On desktop: backed by files in a subdirectory of <see cref="ScriptingConfig.ScriptDirectory"/>.
/// In browser: backed by browser localStorage.
/// </summary>
public interface IScriptStore
{
    /// <summary>Returns the stored string for <paramref name="key"/>, or <c>null</c> if not found.</summary>
    string? Get(string key);

    /// <summary>Stores <paramref name="value"/> under <paramref name="key"/>, overwriting any existing entry.</summary>
    void Set(string key, string value);

    /// <summary>Deletes the entry for <paramref name="key"/>. No-op if the key does not exist.</summary>
    void Delete(string key);

    /// <summary>Returns all stored keys.</summary>
    IReadOnlyList<string> List();

    /// <summary>Returns <c>true</c> if an entry exists for <paramref name="key"/>.</summary>
    bool Exists(string key);
}
