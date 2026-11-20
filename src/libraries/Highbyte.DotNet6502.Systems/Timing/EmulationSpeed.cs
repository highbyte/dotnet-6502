using System.Globalization;

namespace Highbyte.DotNet6502.Systems.Timing;

public sealed record EmulationSpeed
{
    public static EmulationSpeed Normal { get; } = new(1);
    public static EmulationSpeed Unthrottled { get; } = new(multiplier: null);
    public double? Multiplier { get; }
    private EmulationSpeed(double? multiplier) => Multiplier = multiplier;

    public static bool TryParse(string? text, out EmulationSpeed speed)
    {
        speed = Normal;
        if (string.Equals(text?.Trim(), "unthrottled", StringComparison.OrdinalIgnoreCase))
        {
            speed = Unthrottled;
            return true;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value) || value <= 0)
            return false;

        speed = value == 1 ? Normal : new(value);
        return true;
    }
}
