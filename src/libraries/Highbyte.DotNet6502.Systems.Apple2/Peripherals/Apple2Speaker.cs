namespace Highbyte.DotNet6502.Systems.Apple2.Peripherals;

/// <summary>
/// The Apple II speaker: one bit, and nothing else.
///
/// There is no sound chip. Any access to $C030-$C03F flips the cone between its two positions, and
/// every sound the machine makes — beeps, game effects, digitised speech — is software toggling
/// that bit at carefully chosen moments. Pitch is the toggle rate; timbre is the pattern. Software
/// that toggles faster than any sane sample rate is doing pulse-width modulation to fake
/// intermediate levels, which is how the Apple II plays sampled audio at all.
///
/// That is why there is nothing here to describe as notes or voices, and why the audio path is a
/// waveform one: the only signal this peripheral has to offer is its level over time.
///
/// <para>
/// <b>Timing resolution.</b> The cycle recorded for a toggle is whatever the CPU's cycle counter
/// reads at the time, which advances at instruction boundaries — so a toggle is placed at its
/// instruction rather than at its exact cycle within it. At 44.1 kHz one output sample spans about
/// 23 cycles and an instruction is 2-7, so the error stays below a sample. It is the same
/// concession the C64 sample path makes for register writes.
/// </para>
/// </summary>
public class Apple2Speaker
{
    /// <summary>$C030-$C03F: any access flips the cone.</summary>
    public const ushort ToggleAddress = 0xC030;

    private readonly Func<ulong> _cpuCycleProvider;

    /// <summary>
    /// Cone position. The absolute polarity is arbitrary — only the transitions are audible, and
    /// the DC blocker downstream removes any resting offset.
    /// </summary>
    public bool Level { get; private set; }

    /// <summary>
    /// Number of $C030 accesses. Kept because it answers "is this program making sound at all?",
    /// which silence alone cannot — a game with the volume of its effects set to nothing looks
    /// identical to one that never touches the speaker.
    /// </summary>
    public ulong ToggleCount { get; private set; }

    /// <summary>CPU cycle at the most recent toggle.</summary>
    public ulong LastToggleCycle { get; private set; }

    public Apple2Speaker(Func<ulong> cpuCycleProvider)
    {
        _cpuCycleProvider = cpuCycleProvider;
    }

    /// <summary>Flips the cone. Called for reads and writes alike, as on the hardware.</summary>
    public void Toggle()
    {
        Level = !Level;
        ToggleCount++;
        LastToggleCycle = _cpuCycleProvider();
    }

    public void Reset()
    {
        Level = false;
        ToggleCount = 0;
        LastToggleCycle = 0;
    }
}
