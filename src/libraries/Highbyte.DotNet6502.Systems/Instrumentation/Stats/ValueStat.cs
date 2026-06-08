namespace Highbyte.DotNet6502.Systems.Instrumentation.Stats;

/// <summary>
/// A stat whose value is read lazily from a delegate each time it is displayed — a live gauge
/// or counter (e.g. a buffer fill percentage or an error count) rather than a value accumulated
/// over samples. An optional reset delegate lets <see cref="ResetAverage"/> clear the underlying
/// state (e.g. zero a counter).
/// </summary>
public sealed class ValueStat : IStat
{
    private readonly Func<double?> _getValue;
    private readonly string _unit;
    private readonly int _decimals;
    private readonly Action? _resetAction;

    public ValueStat(Func<double?> getValue, string unit = "", int decimals = 0, Action? resetAction = null)
    {
        _getValue = getValue;
        _unit = unit;
        _decimals = decimals;
        _resetAction = resetAction;
    }

    public string GetDescription()
    {
        var value = _getValue();
        if (value == null)
            return "null";

        var rounded = Math.Round(value.Value, _decimals);
        var text = _decimals > 0 ? rounded.ToString("F" + _decimals) : rounded.ToString("0");
        return text + _unit;
    }

    public bool ShouldShow() => _getValue().HasValue;

    public void ResetAverage() => _resetAction?.Invoke();
}
