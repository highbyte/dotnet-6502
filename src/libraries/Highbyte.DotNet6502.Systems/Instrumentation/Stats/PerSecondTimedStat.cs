namespace Highbyte.DotNet6502.Systems.Instrumentation.Stats;

// Credit to instrumentation/stat code to: https://github.com/davidwengier/Trains.NET
public class PerSecondTimedStat : IStat
{
    private const int SampleCount = 60;

    private readonly TimeProvider _timeProvider;
    private long? _previousTimestamp;
    private double? _emaElapsedMs;
    private double? _fakeValue;

    public PerSecondTimedStat() : this(TimeProvider.System)
    {
    }

    public PerSecondTimedStat(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

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
        var timestamp = _timeProvider.GetTimestamp();
        if (_previousTimestamp.HasValue)
        {
            var elapsedMs = _timeProvider.GetElapsedTime(_previousTimestamp.Value, timestamp).TotalMilliseconds;
            // Browser timer precision can give successive callbacks the same timestamp.
            // An unavailable timing sample must not interrupt rendering or audio playback.
            if (elapsedMs > 0)
            {
                if (_emaElapsedMs == null)
                    _emaElapsedMs = elapsedMs;
                else
                    _emaElapsedMs = (_emaElapsedMs.Value * (SampleCount - 1) + elapsedMs) / SampleCount;
            }
        }
        _previousTimestamp = timestamp;
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

    public void ResetAverage()
    {
        _emaElapsedMs = null;
        _previousTimestamp = null;
    }

    // For unit testing
    public void SetFakeFPSValue(double fps)
    {
        _fakeValue = fps;
    }
}
