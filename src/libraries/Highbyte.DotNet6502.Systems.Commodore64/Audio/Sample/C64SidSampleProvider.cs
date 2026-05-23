using Highbyte.DotNet6502.Systems.Audio;

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
public sealed class C64SidSampleProvider : IAudioProvider, IAudioSampleProvider
{
    private readonly C64 _c64;
    private readonly SidSampleCore _core;
    private readonly float[] _stagingBuffer;

    private AudioSampleWriteCallback? _writeSamples;
    private ulong _lastCycles;
    private bool _firstInstructionSeen;

    public string Name => "C64SidSampleProvider";

    public int SampleRateHz => _core.SampleRateHz;

    /// <summary>The SID is mono.</summary>
    public int ChannelCount => 1;

    /// <summary>The wrapped SID core (exposed for diagnostics/tests).</summary>
    public SidSampleCore Core => _core;

    public C64SidSampleProvider(
        C64 c64,
        int sampleRateHz = SidSampleCore.DefaultSampleRateHz,
        int sidClockHz = SidSampleCore.PalSidClockHz)
    {
        _c64 = c64;
        _core = new SidSampleCore(sampleRateHz, sidClockHz);
        // Max samples produced per instruction at 44.1 kHz / PAL is ≤ 2 (ceil(7 cycles × ratio)+1).
        // 64 leaves room for unexpected larger deltas without ever dropping samples.
        _stagingBuffer = new float[64];
    }

    public void Init(AudioSampleWriteCallback writeSamples)
    {
        _writeSamples = writeSamples;

        // Force JIT to compile the inner mixing / waveform paths now, so the first BASIC POKE
        // that turns audio on doesn't pay a multi-millisecond JIT cost mid-frame and crackle.
        _core.WarmUp();
    }

    public void OnAfterInstruction()
    {
        if (_writeSamples is null)
            return;

        ulong nowCycles = _c64.CPU.ExecState.CyclesConsumed;

        if (!_firstInstructionSeen)
        {
            // Don't backfill samples for cycles that ran before audio was wired up.
            _firstInstructionSeen = true;
            _lastCycles = nowCycles;
            ApplyChangedRegisters();
            return;
        }

        int delta = (int)(nowCycles - _lastCycles);
        _lastCycles = nowCycles;

        if (delta > 0)
        {
            int written = _core.AdvanceCycles(delta, _stagingBuffer);
            if (written > 0)
                _writeSamples(_stagingBuffer.AsSpan(0, Math.Min(written, _stagingBuffer.Length)));
        }

        // Register writes during this instruction land here, after the cycles using the prior
        // state are accounted for. See `c64-sid-sample-emulation.md` → per-instruction stepping.
        ApplyChangedRegisters();
    }

    public void OnEndFrame()
    {
        // Samples are emitted per instruction; nothing to flush at frame end.
    }

    private void ApplyChangedRegisters()
    {
        var sidState = _c64.Sid.InternalSidState;
        if (!sidState.IsAudioChanged)
            return;

        const ushort BaseAddr = 0xD400;
        for (int offset = 0; offset < SidSampleCore.RegisterCount; offset++)
        {
            ushort addr = (ushort)(BaseAddr + offset);
            if (sidState.IsRawSidRegChanged(addr))
                _core.WriteRegister(offset, sidState.GetRawSidRegValue(addr));
        }
        sidState.ClearAudioChanged();
    }
}
