using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.Scripting.MoonSharp;

/// <summary>
/// Delegate-backed implementation of <see cref="IScriptStore"/>.
/// Used in browser/WASM environments where the store is backed by localStorage via JSInterop callbacks.
/// </summary>
public sealed class DelegateScriptStore : IScriptStore
{
    private readonly Func<string, string?> _get;
    private readonly Action<string, string> _set;
    private readonly Action<string> _delete;
    private readonly Func<IReadOnlyList<string>> _list;

    public DelegateScriptStore(
        Func<string, string?> get,
        Action<string, string> set,
        Action<string> delete,
        Func<IReadOnlyList<string>> list)
    {
        _get = get;
        _set = set;
        _delete = delete;
        _list = list;
    }

    public string? Get(string key) => _get(key);
    public void Set(string key, string value) => _set(key, value);
    public void Delete(string key) => _delete(key);
    public IReadOnlyList<string> List() => _list();
    public bool Exists(string key) => _get(key) != null;
}
