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
/// Counterpart of <see cref="C64SidCommandStream"/> for the sample-style pipeline. Both can
/// coexist as compiled types but only one is wired up per emulator session (the C64 audio config
/// selects which provider to register).
/// </summary>
[DisplayName("SID sample emulation")]
[HelpText("Pure-managed sample-accurate SID emulation. Ticks the chip per CPU cycle and emits PCM.\nHigher CPU than Synth commands, but reproduces real SID waveform shapes and ADSR behaviour.")]
public sealed class C64SidSampleProvider : IAudioProvider, IAudioSampleProvider, ISidRegisterWriteSink
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

    public string Name => "C64SidSampleProvider";

    public int SampleRateHz => _core.SampleRateHz;

    public int ChannelCount => 1;

    public SidSampleCore Core => _core;

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
        _writeSamples = writeSamples;

        var sidState = _c64.Sid.InternalSidState;

        // Register writes reach the core on the bus cycle they happen, not at instruction end.
        sidState.RegisterWriteSink = this;

        // Lazy OSC3/ENV3 (Auto mode only). The Sid memory-read mappings invoke these getters on
        // read, so the values are computed only when software polls them, for the state at the
        // cycle of the read. Fast mode skips wiring entirely — reads then return 0.
        if (_core.Mode == SidEmulationMode.Auto)
        {
            sidState.Osc3ReadbackProvider = () => { CatchUpToCurrentAccess(); return _core.Osc3; };
            sidState.Env3ReadbackProvider = () => { CatchUpToCurrentAccess(); return _core.Env3; };
        }

        // Force JIT to compile the inner mixing / waveform paths now, so the first BASIC POKE
        // that turns audio on doesn't pay a multi-millisecond JIT cost mid-frame and crackle.
        _core.WarmUp();
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

        CatchUpTo(busCycle - 1);
        _core.WriteRegister(offset, value);
        return true;
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
            CatchUpTo(_c64.CPU.BusCycles - 1);
    }

    private void CatchUpTo(ulong busCycles)
    {
        if (busCycles <= _coreCycles || _writeSamples is null)
            return;

        var remaining = busCycles - _coreCycles;
        _coreCycles = busCycles;
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
