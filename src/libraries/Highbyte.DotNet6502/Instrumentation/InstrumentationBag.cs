using Highbyte.DotNet6502.Instrumentation.Stats;

namespace Highbyte.DotNet6502.Instrumentation;

// Credit for basis of instrumentation/stat code to: https://github.com/davidwengier/Trains.NET    
public static class InstrumentationBag
{
    private static readonly Instrumentations s_instrumentations = new();
    public static IEnumerable<(string Name, IStat Stat)> Stats => s_instrumentations.Stats;
    public static T Add<T>(string name, T stat) where T : IStat
    {
        s_instrumentations.Add(name, stat);
        return stat;
    }
    public static T Add<T>(string name) where T : IStat, new() => Add(name, new T());
    public static void Remove(string name) => s_instrumentations.Remove(name);
    public static void Clear() => s_instrumentations.Clear();
}
