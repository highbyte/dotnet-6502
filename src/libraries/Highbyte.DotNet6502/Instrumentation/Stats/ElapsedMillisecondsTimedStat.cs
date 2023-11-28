using System.Diagnostics;

namespace Highbyte.DotNet6502.Instrumentation.Stats;

// Credit to instrumentation/stat code to: https://github.com/davidwengier/Trains.NET
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Dispose is used to measure")]
public class ElapsedMillisecondsTimedStat : AveragedStat
{
    private readonly Stopwatch _sw;
    private readonly DisposableCallback _disposableCallback;
    public ElapsedMillisecondsTimedStat()
        : base(10) // Average over x samples
    {
        _sw = new Stopwatch();
        _disposableCallback = new DisposableCallback();
        _disposableCallback.Disposing += (o, e) => Stop();
    }

    public void Reset()
    {
        _sw.Reset();
    }

    public void Start(bool cont = false)
    {
        if (cont)
            _sw.Start();
        else
            _sw.Restart();
    }

    public void Stop()
    {
        _sw.Stop();
        //SetValue(_sw.ElapsedMilliseconds);
        SetValue(_sw.Elapsed.Ticks);
    }
    public IDisposable Measure(bool cont = false)
    {
        Start(cont);
        return _disposableCallback;
    }
    public override string GetDescription()
    {
        if (Value == null)
            return "null";

        //double ms = Value.Value / 10000.0d; // 10000 ticks per millisecond
        var ms = Value.Value / TimeSpan.TicksPerMillisecond; // 10000 ticks per millisecond

        if (ms < 0.01)
            return "< 0.01ms";
        return Math.Round(ms, 2).ToString("0.00") + "ms";
    }
}
