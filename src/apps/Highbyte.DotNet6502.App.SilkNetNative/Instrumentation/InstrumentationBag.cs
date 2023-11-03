using Highbyte.DotNet6502.App.SilkNetNative.Instrumentation.Stats;

namespace Highbyte.DotNet6502.App.SilkNetNative.Stats;

// Credit to instrumentation/stat code to: https://github.com/davidwengier/Trains.NET    
public static class InstrumentationBag
{
    private static readonly List<(string Name, IStat Stat)> s_stats = new();
    public static IEnumerable<(string Name, IStat Stat)> Stats => s_stats.AsReadOnly();
    public static T Add<T>(string name, T stat) where T : IStat
    {
        s_stats.Add((name, stat));
        return stat;
    }
    public static T Add<T>(string name) where T : IStat, new() => Add<T>(name, new T());

    public static void Remove(string name) => s_stats.RemoveAll(s => s.Name == name);
}