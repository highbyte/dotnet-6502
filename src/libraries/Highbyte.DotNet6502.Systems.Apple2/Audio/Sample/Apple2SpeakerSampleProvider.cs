using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Utils;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Apple2.Audio.Sample;

/// <summary>
/// Apple II audio provider for the sample-based path. Bridges <see cref="Apple2SpeakerSampleCore"/>
/// to the per-instruction hook: each instruction it advances the core by that instruction's cycles
/// at the level the speaker held for them, pushes any completed PCM through the
/// <see cref="AudioSampleWriteCallback"/>, and then applies whatever the instruction did to the
/// speaker.
///
/// The Apple II has only this style. There is no register set describing notes or voices to feed a
/// synth-command stream — the machine's entire audio output is a one-bit cone position over time,
/// so reproducing it means reproducing the waveform.
/// </summary>
[DisplayName("Speaker sample emulation")]
[HelpText("Reproduces the one-bit speaker as PCM by averaging its level over each output sample.\nThe only faithful option: Apple II sound, including sampled speech, is timed bit-toggling.")]
public sealed class Apple2SpeakerSampleProvider : IAudioProvider, IAudioSampleProvider
{
    private readonly Apple2System _apple2;
    private readonly Apple2SpeakerSampleCore _core;
    private readonly float[] _stagingBuffer;

    private AudioSampleWriteCallback? _writeSamples;
    private ulong _lastCycles;
    private ulong _lastToggleCount;
    private bool _firstInstructionSeen;

    public string Name => "Apple2SpeakerSampleProvider";

    public int SampleRateHz => _core.SampleRateHz;

    /// <summary>The speaker is a single cone.</summary>
    public int ChannelCount => 1;

    /// <summary>The wrapped resampler (exposed for diagnostics and tests).</summary>
    public Apple2SpeakerSampleCore Core => _core;

    public Apple2SpeakerSampleProvider(
        Apple2System apple2,
        int sampleRateHz = Apple2SpeakerSampleCore.DefaultSampleRateHz)
    {
        _apple2 = apple2;
        _core = new Apple2SpeakerSampleCore(apple2.CpuFrequencyHz, sampleRateHz);

        // At ~23 cycles per sample and at most 7 cycles per instruction, one instruction can never
        // complete more than one sample. 16 is generous headroom against that reasoning being off.
        _stagingBuffer = new float[16];
    }

    public void Init(AudioSampleWriteCallback writeSamples) => _writeSamples = writeSamples;

    public void OnAfterInstruction()
    {
        if (_writeSamples is null)
            return;

        var nowCycles = _apple2.CPU.ExecState.CyclesConsumed;

        if (!_firstInstructionSeen)
        {
            // Don't backfill audio for cycles that ran before the provider was wired up: that would
            // dump a burst of stale samples into the buffer the moment sound is switched on.
            _firstInstructionSeen = true;
            _lastCycles = nowCycles;
            _lastToggleCount = _apple2.Speaker.ToggleCount;
            return;
        }

        var delta = (int)(nowCycles - _lastCycles);
        _lastCycles = nowCycles;

        if (delta > 0)
        {
            var written = _core.AdvanceCycles(delta, _stagingBuffer);
            if (written > 0)
                _writeSamples(_stagingBuffer.AsSpan(0, Math.Min(written, _stagingBuffer.Length)));
        }

        ApplySpeakerToggles();
    }

    public void OnEndFrame()
    {
        // Samples are emitted per instruction; nothing to flush at frame end.
    }

    /// <summary>
    /// Picks up any toggling the instruction did. The level is applied <em>after</em> the cycles
    /// that ran at the old level, so a toggle takes effect from the instruction boundary rather
    /// than from its exact cycle within the instruction — the same concession the C64 sample path
    /// makes for register writes, and below one output sample at 44.1 kHz.
    ///
    /// An odd number of toggles within one instruction flips the level; an even number leaves it
    /// where it was, which is what the hardware would do too.
    /// </summary>
    private void ApplySpeakerToggles()
    {
        var toggleCount = _apple2.Speaker.ToggleCount;
        if (toggleCount == _lastToggleCount)
            return;

        _lastToggleCount = toggleCount;
        _core.SetLevel(_apple2.Speaker.Level);
    }
}
