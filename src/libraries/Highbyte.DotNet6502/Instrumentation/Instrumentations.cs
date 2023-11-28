using Highbyte.DotNet6502.Instrumentation.Stats;

namespace Highbyte.DotNet6502.Instrumentation;

public class Instrumentations
{
    private readonly List<(string Name, IStat Stat)> _stats = new();
    public IEnumerable<(string Name, IStat Stat)> Stats => _stats.AsReadOnly();
    public T Add<T>(string name, T stat) where T : IStat
    {
        _stats.Add((name, stat));
        return stat;
    }
    public T Add<T>(string name) where T : IStat, new() => Add(name, new T());
    public void Remove(string name) => _stats.RemoveAll(s => s.Name == name);
    public void Clear() => _stats.Clear();
}
