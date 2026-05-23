namespace Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;

/// <summary>
/// Pure synchronous SID emulation core. Holds the chip state (registers, 3 voices, master
/// volume), advances it forward by SID clock cycles, and produces 32-bit float PCM samples at a
/// fixed output rate. No threading, no I/O, no C64 dependency — feed it cycles and register
/// writes, read samples back.
///
/// Implemented: all four waveforms (triangle, sawtooth, pulse, noise) individually and combined
/// via bitwise AND; full ADSR with the real 16 rate-counter periods and exponential decay
/// approximation; hard sync between voices; ring modulation; TEST-bit hold; 4-bit master volume;
/// voice 3 waveform / envelope readback ($D41B / $D41C); integer Bresenham downsampling from
/// SID rate to output rate (linear interpolation, no anti-alias filter).
///
/// Not implemented: SID filter (cutoff / resonance / filter routing).
///
/// <see cref="SidEmulationMode"/> chooses between full accuracy (<c>Auto</c>, with inner-loop
/// fast paths when the current SID state doesn't need the advanced features) and a lower-CPU
/// mode that disables the advanced features unconditionally (<c>Fast</c>).
/// </summary>
public sealed class SidSampleCore
{
    public const int VoiceCount = 3;
    public const int RegisterCount = 0x1D;          // $D400..$D41C — 29 registers
    public const int VolumeRegisterOffset = 0x18;   // $D418

    /// <summary>Default output sample rate.</summary>
    public const int DefaultSampleRateHz = 44100;

    /// <summary>PAL SID clock (~985 kHz).</summary>
    public const int PalSidClockHz = 985248;

    /// <summary>NTSC SID clock (~1.023 MHz).</summary>
    public const int NtscSidClockHz = 1022730;

    // Per-rate cycle period for the ADSR rate counter — the well-known SID 16-entry table.
    // Indexed by the 4-bit attack/decay/release rate selectors.
    private static readonly ushort[] s_adsrRatePeriods =
    {
        9, 32, 63, 95, 149, 220, 267, 313,
        392, 977, 1954, 3126, 3907, 11720, 19532, 31251,
    };

    private readonly int _sampleRateHz;
    private readonly int _sidClockHz;
    private readonly SidEmulationMode _mode;
    private readonly byte[] _registers = new byte[RegisterCount];
    private readonly Voice[] _voices = new Voice[VoiceCount];

    // Bresenham resampling: every SID cycle add _sampleRateHz; emit a sample whenever the
    // accumulator crosses _sidClockHz, then subtract. Exact integer math, no float drift.
    private int _sampleRateCounter;

    // Aggregate "any voice uses feature X" flags, refreshed on each VCREG decode.
    // Let TickAllVoices fuse into a single-pass loop when no voice currently needs hard sync.
    private bool _anyVoiceUsesSync;
    private bool _anyVoiceUsesTest;

    public SidSampleCore(
        int sampleRateHz = DefaultSampleRateHz,
        int sidClockHz = PalSidClockHz,
        SidEmulationMode mode = SidEmulationMode.Auto)
    {
        if (sampleRateHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRateHz), sampleRateHz, "Sample rate must be positive.");
        if (sidClockHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(sidClockHz), sidClockHz, "SID clock must be positive.");
        _sampleRateHz = sampleRateHz;
        _sidClockHz = sidClockHz;
        _mode = mode;

        for (int i = 0; i < VoiceCount; i++)
            _voices[i] = new Voice();
    }

    public int SampleRateHz => _sampleRateHz;
    public int SidClockHz => _sidClockHz;
    public SidEmulationMode Mode => _mode;

    /// <summary>
    /// Voice 3 waveform-output high byte — the value real SID returns when software reads $D41B.
    /// Tunes use this for waveform-driven effects (e.g. accumulator-modulated vibrato).
    /// </summary>
    public byte Osc3 => (byte)(GetWaveformOutput(2) >> 4);

    /// <summary>
    /// Voice 3 envelope counter — the value real SID returns when software reads $D41C.
    /// </summary>
    public byte Env3 => _voices[2].Envelope;

    /// <summary>
    /// Apply a SID register write. <paramref name="offset"/> is the register index 0..<see cref="RegisterCount"/>-1
    /// (i.e. address minus $D400). Out-of-range offsets are ignored.
    /// </summary>
    public void WriteRegister(int offset, byte value)
    {
        if ((uint)offset >= RegisterCount)
            return;
        _registers[offset] = value;

        // Per-voice registers are at offsets 0..6 for voice 1, 7..13 for voice 2, 14..20 for voice 3.
        for (int v = 0; v < VoiceCount; v++)
        {
            int baseOffset = v * 7;
            if (offset >= baseOffset && offset <= baseOffset + 6)
            {
                DecodeVoiceRegister(v, offset - baseOffset, value);
                return;
            }
        }
        // Global registers (cutoff/resonance/filter/volume) are read directly from _registers
        // during sample mixing — no decode needed here.
    }

    /// <summary>
    /// Runs the inner sample-generation loop with every waveform branch + ADSR phase exercised
    /// once, then resets all chip state. Purpose: force the .NET JIT to compile the hot mixing
    /// paths (<c>MixOutput</c>, <c>GetWaveformOutput</c>, <c>TickEnvelope</c>) at host startup
    /// rather than mid-note. Without this, the first time a program raises master volume above
    /// zero pays a one-time JIT cost large enough to drain the audio ring buffer → first-note
    /// crackle. After warmup the chip is in the same state as a freshly-constructed core.
    /// </summary>
    public void WarmUp()
    {
        Span<float> scratch = stackalloc float[256];

        // Repeat the full warmup several times — .NET's tiered JIT only promotes a method to
        // Tier 1 after roughly 30 invocations on its bytecode-counter trigger, so a single
        // pass through every branch isn't enough; we need many invocations of each hot path.
        const int Passes = 4;
        for (int pass = 0; pass < Passes; pass++)
        {
            // Tick each of the four waveform branches so every arm of GetWaveformOutput is
            // compiled. Also drive ADSR through all four phases (Attack → Decay → Sustain →
            // Release) by running long enough on each phase.
            for (byte waveform = 0; waveform < 4; waveform++)
            {
                byte waveformBit = (byte)(0x10 << waveform); // bit 4..7
                byte voiceCtrlOn = (byte)(waveformBit | 0x01);
                byte voiceCtrlOff = waveformBit;

                WriteRegister(0x00, 0x45);          // FRELO1
                WriteRegister(0x01, 0x1D);          // FREHI1 (~440 Hz)
                WriteRegister(0x02, 0x00);          // PWLO1
                WriteRegister(0x03, 0x08);          // PWHI1 (50% pulse)
                WriteRegister(0x05, 0x00);          // attack=0, decay=0 (fastest)
                WriteRegister(0x06, 0x80);          // sustain=8, release=0
                WriteRegister(0x18, 0x0F);          // master volume = 15
                WriteRegister(0x04, voiceCtrlOn);   // gate on → Attack

                // Attack rate 0 = 9 cycles/step × 255 steps ≈ 2295 cycles for full attack.
                // Then Decay rate 0 down to sustain ≈ another ~5000 cycles with exp-divider slowing.
                // 10k cycles is plenty to exercise Attack → Decay → Sustain.
                AdvanceCycles(10_000, scratch);

                WriteRegister(0x04, voiceCtrlOff);  // gate off → Release
                AdvanceCycles(3000, scratch);
            }

            // Also exercise combined waveforms (saw+triangle), ring modulation (triangle with
            // ring bit), and hard sync (sync bit on voice 2 driven by voice 1) so their first
            // use in a real tune doesn't pay a mid-frame JIT cost.
            // Voice 1: a sync source.
            WriteRegister(0x00, 0xFF); WriteRegister(0x01, 0x40); // higher freq, drives MSB transitions
            WriteRegister(0x04, 0x21);                            // sawtooth + gate
            // Voice 2: target of hard sync + uses ring mod with triangle on the same pass.
            WriteRegister(0x07, 0x00); WriteRegister(0x08, 0x20); // freq
            WriteRegister(0x0C, 0x00); WriteRegister(0x0D, 0xF0); // adsr
            WriteRegister(0x0B, 0x17);                            // triangle + ring + sync + gate
            // Voice 3: combined sawtooth + triangle for combined-waveform path.
            WriteRegister(0x0E, 0x00); WriteRegister(0x0F, 0x10); // freq
            WriteRegister(0x13, 0x00); WriteRegister(0x14, 0xF0); // adsr
            WriteRegister(0x12, 0x31);                            // sawtooth + triangle + gate
            AdvanceCycles(8000, scratch);
            // Drop gates to also re-exercise the release paths with these features active.
            WriteRegister(0x04, 0x20);
            WriteRegister(0x0B, 0x16);
            WriteRegister(0x12, 0x30);
            AdvanceCycles(2000, scratch);
        }

        // Reset all chip state so warmup leaves no audible trace.
        Array.Clear(_registers, 0, _registers.Length);
        for (int i = 0; i < VoiceCount; i++)
            _voices[i] = new Voice();
        _sampleRateCounter = 0;
        _anyVoiceUsesSync = false;
        _anyVoiceUsesTest = false;
    }

    /// <summary>
    /// Advance the SID by <paramref name="cycles"/> SID clock cycles, writing any emitted samples
    /// into <paramref name="output"/>. Returns the number of samples written. If
    /// <paramref name="output"/> fills before all samples for the requested cycle window are
    /// generated, the excess is silently dropped (the caller's ring buffer overran).
    /// </summary>
    public int AdvanceCycles(int cycles, Span<float> output)
    {
        if (cycles <= 0)
            return 0;

        int written = 0;
        for (int c = 0; c < cycles; c++)
        {
            TickAllVoices();

            _sampleRateCounter += _sampleRateHz;
            if (_sampleRateCounter >= _sidClockHz)
            {
                _sampleRateCounter -= _sidClockHz;
                if ((uint)written < (uint)output.Length)
                    output[written] = MixOutput();
                written++;
            }
        }
        return Math.Min(written, output.Length);
    }

    private void DecodeVoiceRegister(int voice, int regOffset, byte value)
    {
        ref var v = ref _voices[voice];
        switch (regOffset)
        {
            case 0: // FRELO
                v.Frequency = (ushort)((v.Frequency & 0xFF00) | value);
                break;
            case 1: // FREHI
                v.Frequency = (ushort)((v.Frequency & 0x00FF) | (value << 8));
                break;
            case 2: // PWLO
                v.PulseWidth = (ushort)((v.PulseWidth & 0x0F00) | value);
                break;
            case 3: // PWHI — only low 4 bits of value matter
                v.PulseWidth = (ushort)((v.PulseWidth & 0x00FF) | ((value & 0x0F) << 8));
                break;
            case 4: // VCREG — gate, waveform, test/ring/sync bits
                DecodeControlReg(ref v, value);
                if (_mode == SidEmulationMode.Fast)
                    ApplyFastModeOverrides(ref v);
                RefreshAggregateFlags();
                break;
            case 5: // ATDCY — attack hi-nibble, decay lo-nibble
                v.AttackRate = (byte)((value >> 4) & 0x0F);
                v.DecayRate = (byte)(value & 0x0F);
                break;
            case 6: // SUREL — sustain hi-nibble, release lo-nibble
                v.SustainLevel = (byte)((value >> 4) & 0x0F);
                v.ReleaseRate = (byte)(value & 0x0F);
                break;
        }
    }

    private static void DecodeControlReg(ref Voice v, byte value)
    {
        bool gate = (value & 0x01) != 0;
        if (gate && !v.Gate)
        {
            // Rising edge of gate — restart attack from current envelope value (SID behaviour).
            v.AdsrPhase = AdsrPhase.Attack;
            v.RateCounter = 0;
            v.ExpCounter = 0;
        }
        else if (!gate && v.Gate)
        {
            // Falling edge of gate — jump to release phase.
            v.AdsrPhase = AdsrPhase.Release;
            v.RateCounter = 0;
            v.ExpCounter = 0;
        }
        v.Gate = gate;

        v.SyncEnabled = (value & 0x02) != 0;       // bit 1 — hard sync from source voice
        v.RingModEnabled = (value & 0x04) != 0;    // bit 2 — ring-modulate triangle with source voice MSB
        v.TestBit = (value & 0x08) != 0;           // bit 3 — held: accumulator + LFSR reset
        v.WaveformBits = (byte)((value >> 4) & 0x0F); // bits 4-7: tri/saw/pul/noi (any combination)

        // TEST bit transition to high also resets accumulator/LFSR immediately on the write
        // (TickPhaseAccumulator will then keep them reset every cycle until the bit clears).
        if (v.TestBit)
        {
            v.Accumulator = 0;
            v.NoiseLfsr = 0x7FFFF8; // reSID-canonical reset value
        }
    }

    private void TickAllVoices()
    {
        // Fast path: no voice is using hard sync this cycle, so phase-accumulator and envelope
        // ticking are independent and can be fused into one pass. Ring modulation still works in
        // this path (it's resolved at sample-emit time in TriangleOutput, not during ticking).
        if (!_anyVoiceUsesSync)
        {
            for (int i = 0; i < VoiceCount; i++)
            {
                TickPhaseAccumulator(i);
                TickEnvelope(ref _voices[i]);
            }
            return;
        }

        // Sync path. Pass 1: advance every voice's phase accumulator and compute MsbJustRose.
        // Two passes are required so the sync source's transition for *this* cycle is visible to
        // every sync'd voice regardless of voice tick order.
        for (int i = 0; i < VoiceCount; i++)
            TickPhaseAccumulator(i);

        // Pass 2: apply hard sync. Source routing: voice 1 ← voice 3, 2 ← 1, 3 ← 2 (i.e.
        // source = (i+2) mod 3).
        for (int i = 0; i < VoiceCount; i++)
        {
            ref var v = ref _voices[i];
            if (v.SyncEnabled && _voices[(i + 2) % VoiceCount].MsbJustRose)
            {
                v.Accumulator = 0;
                v.MsbJustRose = false;
            }
        }

        // Pass 3: envelopes.
        for (int i = 0; i < VoiceCount; i++)
            TickEnvelope(ref _voices[i]);
    }

    private void RefreshAggregateFlags()
    {
        bool anySync = false;
        bool anyTest = false;
        for (int i = 0; i < VoiceCount; i++)
        {
            if (_voices[i].SyncEnabled) anySync = true;
            if (_voices[i].TestBit) anyTest = true;
        }
        _anyVoiceUsesSync = anySync;
        _anyVoiceUsesTest = anyTest;
    }

    /// <summary>
    /// Fast-mode override applied after the standard VCREG decode: strip the advanced features
    /// (hard sync, ring modulation, TEST-bit hold, combined waveforms) so the inner loop hits
    /// fewer paths. The accumulator/LFSR reset that happens on a TEST-bit write was already
    /// applied by DecodeControlReg and is preserved.
    /// </summary>
    private static void ApplyFastModeOverrides(ref Voice v)
    {
        v.SyncEnabled = false;
        v.RingModEnabled = false;
        v.TestBit = false; // disable per-cycle accumulator hold; on-write reset already happened

        // Collapse multiple waveform bits to a single one (lowest-numbered wins).
        int wf = v.WaveformBits;
        if      ((wf & 0x1) != 0) v.WaveformBits = 0x1;
        else if ((wf & 0x2) != 0) v.WaveformBits = 0x2;
        else if ((wf & 0x4) != 0) v.WaveformBits = 0x4;
        else if ((wf & 0x8) != 0) v.WaveformBits = 0x8;
    }

    private void TickPhaseAccumulator(int voiceIdx)
    {
        ref var v = ref _voices[voiceIdx];

        if (v.TestBit)
        {
            // While TEST is held high, the oscillator is forced to zero. No MSB transitions, no LFSR clocks.
            v.Accumulator = 0;
            v.MsbJustRose = false;
            return;
        }

        uint prevAcc = v.Accumulator;
        v.Accumulator = (prevAcc + v.Frequency) & 0x00FFFFFF; // 24-bit wrap

        v.MsbJustRose = (prevAcc & 0x800000) == 0 && (v.Accumulator & 0x800000) != 0;

        // Noise LFSR clocks on a 0→1 transition of accumulator bit 19.
        if ((prevAcc & 0x080000) == 0 && (v.Accumulator & 0x080000) != 0)
        {
            uint feedback = ((v.NoiseLfsr >> 17) ^ (v.NoiseLfsr >> 22)) & 1;
            v.NoiseLfsr = ((v.NoiseLfsr << 1) | feedback) & 0x7FFFFF;
        }
    }

    private static void TickEnvelope(ref Voice v)
    {
        // Sustain and Off do not tick the rate counter — envelope is held.
        if (v.AdsrPhase == AdsrPhase.Sustain)
        {
            int sustainTarget = (v.SustainLevel << 4) | v.SustainLevel; // 4-bit nibble in both halves of byte
            v.Envelope = (byte)sustainTarget;
            return;
        }
        if (v.AdsrPhase == AdsrPhase.Off)
        {
            v.Envelope = 0;
            return;
        }

        byte rateIdx = v.AdsrPhase switch
        {
            AdsrPhase.Attack => v.AttackRate,
            AdsrPhase.Decay => v.DecayRate,
            AdsrPhase.Release => v.ReleaseRate,
            _ => (byte)0,
        };

        v.RateCounter++;
        if (v.RateCounter < s_adsrRatePeriods[rateIdx])
            return;
        v.RateCounter = 0;

        if (v.AdsrPhase == AdsrPhase.Attack)
        {
            // Attack is linear — increment envelope each rate-period until 0xFF.
            if (v.Envelope < 0xFF)
                v.Envelope++;
            if (v.Envelope == 0xFF)
                v.AdsrPhase = AdsrPhase.Decay;
            return;
        }

        // Decay and Release use exponential approximation: the divider table slows the
        // envelope decrement as it approaches zero.
        v.ExpCounter++;
        byte divider = GetExpDivider(v.Envelope);
        if (v.ExpCounter < divider)
            return;
        v.ExpCounter = 0;

        if (v.AdsrPhase == AdsrPhase.Decay)
        {
            int sustainTarget = (v.SustainLevel << 4) | v.SustainLevel;
            if (v.Envelope > sustainTarget)
            {
                v.Envelope--;
            }
            else
            {
                v.AdsrPhase = AdsrPhase.Sustain;
            }
        }
        else // Release
        {
            if (v.Envelope > 0)
                v.Envelope--;
            else
                v.AdsrPhase = AdsrPhase.Off;
        }
    }

    private static byte GetExpDivider(byte envelope)
    {
        // Per reSID: the envelope counter step is divided to approximate the exponential curve.
        if (envelope >= 0xFF) return 1;
        if (envelope >= 0x5D) return 2;
        if (envelope >= 0x36) return 4;
        if (envelope >= 0x1A) return 8;
        if (envelope >= 0x0E) return 16;
        if (envelope >= 0x06) return 30;
        return 30;
    }

    private float MixOutput()
    {
        int masterVol = _registers[VolumeRegisterOffset] & 0x0F;
        if (masterVol == 0)
            return 0f;

        int mix = 0;
        for (int i = 0; i < VoiceCount; i++)
        {
            ref var v = ref _voices[i];
            if (v.WaveformBits == 0)
                continue;                                            // silent voice contributes 0
            int wave = GetWaveformOutput(i);                         // 0..4095
            int centered = wave - 0x800;                             // remove DC bias: -2048..+2047
            int enveloped = (centered * v.Envelope) >> 8;            // envelope=0 ⇒ silent
            mix += enveloped;
        }

        // Normalise to roughly [-1, 1] then apply master volume. The denominator is
        // VoiceCount × 2048 (max signed magnitude per voice after DC removal and envelope).
        return mix * (masterVol / 15f) / (VoiceCount * 2048f);
    }

    /// <summary>
    /// Waveform output for the given voice. Common-case fast path: when exactly one waveform bit
    /// is set (the overwhelming majority of cycles in real tunes), dispatch directly to the
    /// matching generator. Multi-bit combinations fall through to the bitwise-AND mix (a coarse
    /// approximation of real SID analog mixing — chip-measured lookup tables would be more
    /// accurate but are deferred).
    /// </summary>
    private int GetWaveformOutput(int voiceIdx)
    {
        int bits = _voices[voiceIdx].WaveformBits;
        switch (bits)
        {
            case 0x0: return 0;
            case 0x1: return TriangleOutput(voiceIdx);
            case 0x2: return SawtoothOutput(voiceIdx);
            case 0x4: return PulseOutput(voiceIdx);
            case 0x8: return NoiseOutput(voiceIdx);
        }

        // Combined waveforms (multiple bits set): bitwise AND of the active generators.
        int combined = 0xFFF;
        if ((bits & 0x1) != 0) combined &= TriangleOutput(voiceIdx);
        if ((bits & 0x2) != 0) combined &= SawtoothOutput(voiceIdx);
        if ((bits & 0x4) != 0) combined &= PulseOutput(voiceIdx);
        if ((bits & 0x8) != 0) combined &= NoiseOutput(voiceIdx);
        return combined;
    }

    private int TriangleOutput(int voiceIdx)
    {
        ref var v = ref _voices[voiceIdx];
        uint a = v.Accumulator;
        uint msbBit;
        if (v.RingModEnabled)
        {
            // Ring modulation: XOR our accumulator MSB with the sync source's accumulator MSB.
            // Source routing matches hard sync (voice 1 ← 3, 2 ← 1, 3 ← 2).
            uint srcAcc = _voices[(voiceIdx + 2) % VoiceCount].Accumulator;
            msbBit = (a ^ srcAcc) & 0x800000;
        }
        else
        {
            msbBit = a & 0x800000;
        }
        uint folded = (msbBit != 0) ? ~a : a;
        return (int)((folded >> 11) & 0xFFE); // 12-bit, LSB always 0 (real SID drops LSB)
    }

    private int SawtoothOutput(int voiceIdx)
        => (int)((_voices[voiceIdx].Accumulator >> 12) & 0xFFF);

    private int PulseOutput(int voiceIdx)
    {
        ref var v = ref _voices[voiceIdx];
        // Output high (0xFFF) when the top 12 bits of the accumulator are >= pulse width,
        // low (0) otherwise. PW=0 ⇒ always low; PW=0xFFF ⇒ always high (real SID quirk).
        uint accTop = (v.Accumulator >> 12) & 0xFFF;
        return accTop >= v.PulseWidth ? 0xFFF : 0;
    }

    private int NoiseOutput(int voiceIdx)
    {
        // Standard reSID 8-bit noise tap, shifted left 4 to occupy the 12-bit waveform range.
        uint l = _voices[voiceIdx].NoiseLfsr;
        return
            (int)(((l >> 22) & 1) << 11) |
            (int)(((l >> 20) & 1) << 10) |
            (int)(((l >> 16) & 1) << 9) |
            (int)(((l >> 13) & 1) << 8) |
            (int)(((l >> 11) & 1) << 7) |
            (int)(((l >> 7) & 1) << 6) |
            (int)(((l >> 4) & 1) << 5) |
            (int)(((l >> 2) & 1) << 4);
    }

    private enum AdsrPhase : byte { Off, Attack, Decay, Sustain, Release }

    // Mutable struct held in an array; accessed via ref. Performance matters here because
    // TickAllVoices runs at the SID clock rate (~1 MHz).
    private struct Voice
    {
        public uint Accumulator;
        public uint NoiseLfsr;
        public ushort Frequency;
        public ushort PulseWidth;
        /// <summary>VCREG waveform-select bits 4..7, shifted down to bits 0..3
        /// (0x1=triangle, 0x2=saw, 0x4=pulse, 0x8=noise; any combination allowed).</summary>
        public byte WaveformBits;
        public bool SyncEnabled;
        public bool RingModEnabled;
        public bool TestBit;
        public bool Gate;
        public byte AttackRate;
        public byte DecayRate;
        public byte SustainLevel;
        public byte ReleaseRate;
        public byte Envelope;
        public ushort RateCounter;
        public byte ExpCounter;
        public AdsrPhase AdsrPhase;
        /// <summary>Set in TickPhaseAccumulator when this voice's accumulator MSB just went 0→1.
        /// Consumed by sync'd voices in the same cycle; cleared on next tick.</summary>
        public bool MsbJustRose;

        public Voice()
        {
            NoiseLfsr = 0x7FFFF8;
            AdsrPhase = AdsrPhase.Off;
        }
    }
}
