namespace Highbyte.DotNet6502.Instrumentation.Stats;

// Credit to instrumentation/stat code to: https://github.com/davidwengier/Trains.NET
public abstract class AveragedStat : IStat
{
    public double? Value { get; private set; }
    private readonly int _sampleCount;
    public AveragedStat(int sampleCount)
    {
        _sampleCount = sampleCount;
    }
    protected void SetValue(double value)
    {
        if (Value == null)
            Value = value;
        else
        {
            Value = (Value * (_sampleCount - 1) + value) / _sampleCount;
        }
    }
    public abstract string GetDescription();

    public bool ShouldShow() => Value.HasValue;
}
