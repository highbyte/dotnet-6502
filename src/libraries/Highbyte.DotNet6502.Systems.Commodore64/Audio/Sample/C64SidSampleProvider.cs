using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Utils;

namespace Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;

/// <summary>
/// C64 audio provider for the sample-based audio path. Wraps a <see cref="SidSampleCore"/>
/// (pure synchronous SID emulation) and bridges it to the per-instruction <see cref="C64"/>
/// hook: each instruction it forwards the instruction's cycle count to the core, pushes any
/// freshly generated PCM samples through the <see cref="AudioSampleWriteCallback"/> supplied by
/// <see cref="AudioSampleCoordinator"/>, and applies any SID register writes the instruction did.
///
/// The machine also uses this core for SID register readbacks with command-stream or disabled
/// audio. In that case register accesses advance chip state on demand and skip PCM generation.
/// </summary>
[DisplayName("SID sample emulation")]
[HelpText("Pure-managed sample-accurate SID emulation. Ticks the chip per CPU cycle and emits PCM.\nHigher CPU than Synth commands, but reproduces real SID waveform shapes and ADSR behaviour.")]
public sealed partial class C64SidSampleProvider : IAudioProvider, IAudioSampleProvider, ISidRegisterWriteSink
{
    private readonly C64 _c64;
    private readonly SidSampleCore _core;
    private readonly float[] _stagingBuffer;
    private readonly int _maxCyclesPerAdvance;

    private AudioSampleWriteCallback? _writeSamples;
    // The SID core's clock, in CPU bus cycles (CPU.BusCycles): every cycle up to and including
    // this one has been ticked into the core.
    private ulong _coreCycles;
    private bool _clockStarted;
    private bool _machineOwned;

    public string Name => "C64SidSampleProvider";

    public int SampleRateHz => _core.SampleRateHz;

    public int ChannelCount => 1;

    public SidSampleCore Core
    {
        get
        {
            if (_machineOwned)
                CatchUpTo(_c64.CPU.BusCycles);
            return _core;
        }
    }

    internal SidEmulationMode Mode => _core.Mode;

    public C64SidSampleProvider(
        C64 c64,
        int sampleRateHz = SidSampleCore.DefaultSampleRateHz,
        int sidClockHz = SidSampleCore.PalSidClockHz,
        SidEmulationMode mode = SidEmulationMode.Auto)
    {
        _c64 = c64;
        _core = new SidSampleCore(sampleRateHz, sidClockHz, mode);
        _stagingBuffer = new float[64];
        // Cycles that fit the staging buffer at this resample ratio; larger spans are advanced in
        // chunks so no sample is ever dropped (at 48 kHz / PAL that is ~1,270 cycles per chunk).
        _maxCyclesPerAdvance = Math.Max(1, (int)((long)(_stagingBuffer.Length - 2) * sidClockHz / sampleRateHz));
    }

    public void Init(AudioSampleWriteCallback writeSamples)
    {
        if (_machineOwned)
            CatchUpTo(_c64.CPU.BusCycles);
        _writeSamples = writeSamples;
        if (!_machineOwned)
            WireSidRegisters();

        // Warm a separate core: attaching an output device must not reset machine-visible
        // oscillator/envelope state, especially after restoring a recording snapshot.
        new SidSampleCore(_core.SampleRateHz, _core.SidClockHz, _core.Mode).WarmUp();
    }

    internal void InitMachine()
    {
        _machineOwned = true;
        _coreCycles = _c64.CPU.BusCycles;
        _clockStarted = true;
        WireSidRegisters();
    }

    private void WireSidRegisters()
    {
        var sidState = _c64.Sid.InternalSidState;

        // Register writes reach the core on the bus cycle they happen, not at instruction end.
        sidState.RegisterWriteSink = this;

        // Lazy OSC3/ENV3 (Auto mode only). The Sid memory-read mappings invoke these getters on
        // read, so the values are computed only when software polls them, for the state at the
        // cycle of the read. Fast mode skips wiring entirely — reads then return 0.
        if (_core.Mode == SidEmulationMode.Auto)
        {
            sidState.Osc3ReadbackProvider = ReadOsc3;
            sidState.Env3ReadbackProvider = () => { CatchUpToCurrentAccess(); return _core.Env3; };
        }
        else
        {
            sidState.Osc3ReadbackProvider = null;
            sidState.Env3ReadbackProvider = null;
        }
    }

    private byte ReadOsc3()
    {
        ulong completed = CyclesBeforeAccess(_c64.CPU.BusCycles);
        if (_writeSamples is null && completed > _coreCycles
            && _core.TryPeekOsc3(completed - _coreCycles, out byte value))
            return value;
        if (_clockStarted)
            CatchUpTo(completed);
        return _core.Osc3;
    }

    /// <summary>
    /// A register write on <paramref name="busCycle"/>: the core is advanced through every cycle
    /// before it, then the write is applied, so it takes effect from that cycle on. Before the
    /// clock has started (audio wired up mid-run) the write falls back to the batched path.
    /// </summary>
    public bool OnRegisterWrite(ushort address, byte value, ulong busCycle)
    {
        if (!_clockStarted)
            return false;

        int offset = address - SidAddr.FRELO1;
        if ((uint)offset >= SidSampleCore.RegisterCount)
            return false;

        CatchUpTo(CyclesBeforeAccess(busCycle));
        _core.WriteRegister(offset, value);
        // The command-stream output still needs its changed-register notifications. The
        // emulated SID is clocked independently even when no audio output is attached.
        return !_machineOwned || ReferenceEquals(_c64.AudioProvider, this);
    }

    public void OnAfterInstruction()
    {
        if (_writeSamples is null)
            return;
        ulong nowCycles = _c64.CPU.BusCycles;

        if (!_clockStarted)
        {
            // Don't backfill samples for cycles that ran before audio was wired up.
            _clockStarted = true;
            _coreCycles = nowCycles;
            ApplyChangedRegisters();
            return;
        }

        CatchUpTo(nowCycles);

        // Only writes the sink did not take at their exact cycle remain here: writes made before
        // the clock started, and the re-evaluation a snapshot restore requests.
        ApplyChangedRegisters();
    }

    public void OnEndFrame()
    {
        // Samples are emitted per instruction; nothing to flush at frame end.
    }

    /// <summary>State as of the cycle of the access the CPU is performing right now.</summary>
    private void CatchUpToCurrentAccess()
    {
        if (_clockStarted)
            CatchUpTo(CyclesBeforeAccess(_c64.CPU.BusCycles));
    }

    // During a CPU access the bus counter already includes the current cycle.
    // Host reads/writes between instructions see the fully completed cycle.
    private ulong CyclesBeforeAccess(ulong busCycle)
        => busCycle > _c64.Cia1.AdvancedToBusCycle ? busCycle - 1 : busCycle;

    private void CatchUpTo(ulong busCycles)
    {
        if (busCycles <= _coreCycles)
            return;

        var remaining = busCycles - _coreCycles;
        _coreCycles = busCycles;
        if (_writeSamples is null)
        {
            _core.AdvanceWithoutOutput(remaining);
            return;
        }
        while (remaining > 0)
        {
            int chunk = (int)Math.Min(remaining, (ulong)_maxCyclesPerAdvance);
            int written = _core.AdvanceCycles(chunk, _stagingBuffer);
            if (written > 0)
                _writeSamples(_stagingBuffer.AsSpan(0, Math.Min(written, _stagingBuffer.Length)));
            remaining -= (ulong)chunk;
        }
    }

    private void ApplyChangedRegisters()
    {
        var sidState = _c64.Sid.InternalSidState;
        if (!sidState.IsAudioChanged)
            return;

        for (int offset = 0; offset < SidSampleCore.RegisterCount; offset++)
        {
            ushort addr = (ushort)(SidAddr.FRELO1 + offset);
            if (sidState.IsRawSidRegChanged(addr))
                _core.WriteRegister(offset, sidState.GetRawSidRegValue(addr));
        }
        sidState.ClearAudioChanged();
    }
}
