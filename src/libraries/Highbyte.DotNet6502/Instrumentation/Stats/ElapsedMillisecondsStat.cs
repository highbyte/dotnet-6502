namespace Highbyte.DotNet6502.Instrumentation.Stats;

public class ElapsedMillisecondsStat : AveragedStat
{
    private double _currentMs;
    public ElapsedMillisecondsStat()
        : base(10) // Average over 10 samples
    {
        _currentMs = 0;
    }
    public void Add(double ms) => _currentMs += ms;
    public void Set(double ms) => _currentMs = ms;
    public void UpdateStat()
    {
        SetValue(_currentMs);
        _currentMs = 0;
    }

    public override string GetDescription()
    {
        if (Value == null)
            return "null";
        //double ms = Value.Value / TimeSpan.TicksPerMillisecond; // 10000 ticks per millisecond
        var ms = Value.Value;

        if (ms < 0.01)
            return "< 0.01ms";
        return Math.Round(ms, 2).ToString("0.00") + "ms";

    }
}
