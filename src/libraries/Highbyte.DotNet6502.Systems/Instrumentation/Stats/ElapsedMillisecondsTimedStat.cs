using System.Diagnostics;

namespace Highbyte.DotNet6502.Systems.Instrumentation.Stats;

// Credit to instrumentation/stat code to: https://github.com/davidwengier/Trains.NET
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Dispose is used to measure")]
public class ElapsedMillisecondsTimedStat : AveragedStat
{
    private readonly Stopwatch _sw;

    public ElapsedMillisecondsTimedStat() : this(10) { }

    public ElapsedMillisecondsTimedStat(int samples)
        : base(samples) // Average over x samples   
    {
        _sw = new Stopwatch();
    }

    public void Reset()
    {
        _sw.Reset();
    }

    public virtual void Start(bool cont = false)
    {
        if (cont)
            _sw.Start();
        else
            _sw.Restart();
    }

    public virtual void Stop(bool cont = false)
    {
        _sw.Stop();
        if (!cont)
            //SetValue(_sw.ElapsedMilliseconds);
            SetValue(_sw.Elapsed.Ticks);
    }

    public double? GetStatMilliseconds()
    {
        if (Value == null)
            return null;
        return Value.Value / TimeSpan.TicksPerMillisecond; // 10000 ticks per millisecond
    }

    public override string GetDescription()
    {
        var ms = GetStatMilliseconds();
        if (ms == null)
            return "null";

        if (ms < 0.01)
            return "< 0.01ms";
        return Math.Round(ms.Value, 2).ToString("0.00") + "ms";
    }

    public void SetFakeMSValue(double ms)
    {
        SetValue(ms);
    }
}
