namespace Highbyte.DotNet6502;

/// <summary>
/// The MOS 6510's on-chip I/O port — chip truth only. Two 8-bit registers (the machine
/// maps them at $00/$01 through its normal memory handlers): the data-direction
/// register (1 = output) and the data register (output latch). Port lines P0–P5 exist
/// as pins; bits 6–7 have no pins.
///
/// Reading the port combines, per bit: output-configured bits read the output latch;
/// input-configured bits that the board drives read the externally supplied line levels;
/// input-configured bits that nothing drives — the unimplemented bits 6–7 always, and the
/// pins the board reports as floating — read the charge left on them: the value the latch
/// had when the bit was last an output. On the real chip that charge decays within a few
/// hundred milliseconds; here it holds, which is what the test programs and the programs
/// that rely on it (reading back a value through such a bit) observe within that time.
///
/// Board wiring stays in the machine (e.g. C64): pull-ups/pull-downs and other input
/// line levels are supplied via <see cref="ExternalInputLevels"/>, pins with nothing
/// attached via <see cref="FloatingLinesMask"/>, and derived board
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
    /// Implemented pins that nothing on the board drives (the C64's cassette write line with
    /// no datasette attached). Configured as inputs they read their held charge, not
    /// <see cref="ExternalInputLevels"/>. Set by the machine at wiring time.
    /// </summary>
    public byte FloatingLinesMask { get; set; }

    // The value held by the floating lines: the latch as it was while each bit was last an
    // output. Bits configured as outputs are refreshed from the latch whenever the direction
    // register is written, so the sample is taken at the moment a bit turns into an input.
    private byte _floatingCharge;

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
        _floatingCharge = (byte)((_floatingCharge & ~DataDirectionRegister) | (DataRegister & DataDirectionRegister));
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
        var floatingLines = (byte)(FloatingLinesMask | ~ImplementedLinesMask);
        var outputBits = (byte)(DataRegister & DataDirectionRegister);
        var drivenInputBits = (byte)(ExternalInputLevels & ~DataDirectionRegister & ~floatingLines);
        var floatingInputBits = (byte)(_floatingCharge & ~DataDirectionRegister & floatingLines);
        return (byte)(outputBits | drivenInputBits | floatingInputBits);
    }

    /// <summary>
    /// Sets both registers together (machine reset, snapshot restore), then raises
    /// <see cref="OutputsChanged"/> exactly once with the final state — subscribers
    /// never observe a half-applied combination. The floating lines take the data register's
    /// value as their charge (the state a snapshot carries; see <see cref="SerializeState"/>).
    /// </summary>
    public void SetState(byte dataDirectionRegister, byte dataRegister)
    {
        DataDirectionRegister = dataDirectionRegister;
        DataRegister = dataRegister;
        _floatingCharge = dataRegister;
        OutputsChanged?.Invoke();
    }

    /// <summary>Register values, held charge and board wiring copy; no event subscribers.</summary>
    public override CpuModelState Clone()
        => new Cpu6510Port
        {
            DataDirectionRegister = DataDirectionRegister,
            DataRegister = DataRegister,
            ExternalInputLevels = ExternalInputLevels,
            FloatingLinesMask = FloatingLinesMask,
            _floatingCharge = _floatingCharge,
        };

    /// <summary>
    /// The two raw registers; input levels are board wiring and stay out, and so does the
    /// floating lines' charge (it decays on the real chip anyway): a restore seeds it from
    /// the data register.
    /// </summary>
    public override byte[] SerializeState()
        => new[] { DataDirectionRegister, DataRegister };

    public override void RestoreState(byte[] data)
    {
        if (data.Length != 2)
            throw new DotNet6502Exception($"6510 port state payload must be 2 bytes (DDR, data register), was {data.Length}.");
        SetState(dataDirectionRegister: data[0], dataRegister: data[1]);
    }
}
