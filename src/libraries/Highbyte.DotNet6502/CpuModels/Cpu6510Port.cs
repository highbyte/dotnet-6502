namespace Highbyte.DotNet6502;

/// <summary>
/// The MOS 6510's on-chip I/O port — chip truth only. Two 8-bit registers (the machine
/// maps them at $00/$01 through its normal memory handlers): the data-direction
/// register (1 = output) and the data register (output latch). Port lines P0–P5 exist
/// as pins; bits 6–7 have no pins.
///
/// Reading the port combines, per bit: output-configured implemented bits read the
/// output latch; input-configured implemented bits read the externally supplied line
/// levels; the unimplemented bits 6–7 read the latch (a simplification — the real chip
/// reads floating/decaying values there, which nothing emulated depends on).
///
/// Board wiring stays in the machine (e.g. C64): pull-ups/pull-downs and other input
/// line levels are supplied via <see cref="ExternalInputLevels"/>, and derived board
/// state (memory banking, cassette motor) reacts to the synchronous
/// <see cref="OutputsChanged"/> notification — raised before the triggering CPU write
/// returns, so banking changes take effect for the very next memory access.
/// </summary>
public sealed class Cpu6510Port : CpuModelState
{
    /// <summary>P0–P5 exist as pins on the 6510; bits 6–7 do not.</summary>
    public const byte ImplementedLinesMask = 0b0011_1111;

    /// <summary>Raw data-direction register ($00): 1 = output, 0 = input.</summary>
    public byte DataDirectionRegister { get; private set; }

    /// <summary>Raw data register / output latch ($01).</summary>
    public byte DataRegister { get; private set; }

    /// <summary>
    /// Line levels the board presents to input-configured pins (pull-ups, cassette
    /// sense, ...). Set by the machine at wiring time; observable through
    /// <see cref="ReadPort"/> only on bits configured as inputs.
    /// </summary>
    public byte ExternalInputLevels { get; set; }

    /// <summary>
    /// Raised synchronously after every register write (<see cref="WriteDataDirectionRegister"/>,
    /// <see cref="WriteDataRegister"/>) and once after <see cref="SetState"/> — including
    /// writes that leave the values unchanged; the subscriber owns change detection.
    /// Handlers may read the port's properties. A handler that writes back into the port
    /// re-enters this notification synchronously — subscribers must not do that unless
    /// they can terminate the recursion. Reads never notify.
    /// </summary>
    public event Action? OutputsChanged;

    /// <summary>Store to $00.</summary>
    public void WriteDataDirectionRegister(byte value)
    {
        DataDirectionRegister = value;
        OutputsChanged?.Invoke();
    }

    /// <summary>Store to $01.</summary>
    public void WriteDataRegister(byte value)
    {
        DataRegister = value;
        OutputsChanged?.Invoke();
    }

    /// <summary>Load from $00: the raw data-direction register.</summary>
    public byte ReadDataDirectionRegister() => DataDirectionRegister;

    /// <summary>Load from $01: the per-bit combination described on the class.</summary>
    public byte ReadPort()
    {
        var outputBits = (byte)(DataRegister & DataDirectionRegister & ImplementedLinesMask);
        var inputBits = (byte)(ExternalInputLevels & ~DataDirectionRegister & ImplementedLinesMask);
        var unimplementedBits = (byte)(DataRegister & ~ImplementedLinesMask);
        return (byte)(unimplementedBits | outputBits | inputBits);
    }

    /// <summary>
    /// Sets both registers together (machine reset, snapshot restore), then raises
    /// <see cref="OutputsChanged"/> exactly once with the final state — subscribers
    /// never observe a half-applied combination.
    /// </summary>
    public void SetState(byte dataDirectionRegister, byte dataRegister)
    {
        DataDirectionRegister = dataDirectionRegister;
        DataRegister = dataRegister;
        OutputsChanged?.Invoke();
    }

    /// <summary>Register values and input levels copy; no event subscribers.</summary>
    public override CpuModelState Clone()
        => new Cpu6510Port
        {
            DataDirectionRegister = DataDirectionRegister,
            DataRegister = DataRegister,
            ExternalInputLevels = ExternalInputLevels,
        };
}
