using System.Diagnostics;

namespace Highbyte.DotNet6502.App.SkiaNative.Instrumentation.Stats;

// Credit to instrumentation/stat code to: https://github.com/davidwengier/Trains.NET    
public class PerSecondTimedStat : AveragedStat
{
    private readonly Stopwatch _sw;
    public PerSecondTimedStat()
        : base(60) // Average over 60 samples
    {
        _sw = new Stopwatch();
    }

    public void Update()
    {
        if (_sw.IsRunning)
        {
            //var elapsedMs = _sw.ElapsedMilliseconds;
            var elapsedMs = _sw.Elapsed.TotalMilliseconds;
#if DEBUG
            if (elapsedMs == 0)
                throw new NotImplementedException("Elapsed 0.0 miliseconds, cannot handle division by 0");
#endif
            double perSecond = 1000.0 / elapsedMs;
            SetValue(perSecond);
        }
        _sw.Restart();
    }

    public override string GetDescription()
    {
        if (this.Value == null)
        {
            return "null";
        }
        if (this.Value < 0.01)
        {
            return "< 0.01";
        }
        return Math.Round(this.Value ?? 0, 2).ToString();
    }
}