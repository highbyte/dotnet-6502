using System.Diagnostics;

namespace Highbyte.DotNet6502.Systems.Instrumentation.Stats;

// Credit to instrumentation/stat code to: https://github.com/davidwengier/Trains.NET
public class PerSecondTimedStat : IStat
{
    private const int SampleCount = 60;

    private readonly Stopwatch _sw = new();
    private double? _emaElapsedMs;
    private double? _fakeValue;

    // Compute FPS as 1 / E[T], not E[1/T]. The latter is upward-biased when intervals vary
    // (Jensen's inequality), which on browsers - where Task.Delay clamping causes 30%+ jitter
    // in frame intervals - makes a true 59.83 fps loop read as ~62 fps. Averaging the elapsed
    // time and inverting gives an unbiased estimate of the true average rate.
    public double? Value
    {
        get
        {
            if (_fakeValue.HasValue)
                return _fakeValue.Value;
            if (!_emaElapsedMs.HasValue || _emaElapsedMs.Value <= 0)
                return null;
            return 1000.0 / _emaElapsedMs.Value;
        }
    }

    public void Update()
    {
        if (_sw.IsRunning)
        {
            var elapsedMs = _sw.Elapsed.TotalMilliseconds;
#if DEBUG
            if (elapsedMs == 0)
                throw new NotImplementedException("Elapsed 0.0 milliseconds, cannot handle division by 0");
#endif
            if (_emaElapsedMs == null)
                _emaElapsedMs = elapsedMs;
            else
                _emaElapsedMs = (_emaElapsedMs.Value * (SampleCount - 1) + elapsedMs) / SampleCount;
        }
        _sw.Restart();
    }

    public string GetDescription()
    {
        var value = Value;
        if (value == null)
            return "null";
        if (value < 0.01)
            return "< 0.01";
        return Math.Round(value ?? 0, 2).ToString();
    }

    public bool ShouldShow() => Value.HasValue;

    // For unit testing
    public void SetFakeFPSValue(double fps)
    {
        _fakeValue = fps;
    }
}
