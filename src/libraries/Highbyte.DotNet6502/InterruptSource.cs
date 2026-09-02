namespace Highbyte.DotNet6502;

/// <summary>
/// Handle for one named interrupt source registered with a <see cref="CPUInterrupts"/> instance.
/// Devices that raise or clear interrupt lines frequently should obtain the handle once (via
/// <see cref="CPUInterrupts.GetSource(string)"/>) and use the handle-based overloads, which are a
/// single mask operation with no dictionary lookup. The string-based overloads remain for wiring
/// code that runs on device events and for diagnostics.
/// </summary>
public readonly struct InterruptSource : IEquatable<InterruptSource>
{
    /// <summary>Bit position of this source in the owning <see cref="CPUInterrupts"/> line masks.</summary>
    public int Index { get; }

    /// <summary>The name the source was registered under.</summary>
    public string Name { get; }

    /// <summary>Single-bit mask for this source.</summary>
    public ulong Mask => 1UL << Index;

    internal InterruptSource(int index, string name)
    {
        Index = index;
        Name = name;
    }

    public bool Equals(InterruptSource other) => Index == other.Index && string.Equals(Name, other.Name, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is InterruptSource other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Index, Name);
    public override string ToString() => $"{Name} (#{Index})";

    public static bool operator ==(InterruptSource left, InterruptSource right) => left.Equals(right);
    public static bool operator !=(InterruptSource left, InterruptSource right) => !left.Equals(right);
}
