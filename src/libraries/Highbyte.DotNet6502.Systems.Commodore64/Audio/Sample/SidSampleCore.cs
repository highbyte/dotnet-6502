namespace Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;

/// <summary>
/// Pure synchronous SID emulation core for the Phase 1 sample-based audio path. Holds the chip
/// state (registers, 3 voices, master volume), advances it forward by SID clock cycles, and
/// produces 32-bit float PCM samples at a fixed output rate. No threading, no I/O, no C64
/// dependency — feed it cycles and register writes, read samples back.
///
/// Phase 1 scope (matches design-log idea
/// <c>c64-sid-sample-emulation.md</c>): individual waveforms only (saw / triangle / pulse / noise,
/// no combined waveforms), full ADSR with the real 16 rate-counter periods and exponential decay
/// approximation, 4-bit master volume, simple Bresenham downsampling SID-rate → output-rate
/// (linear, no anti-alias filter). No filter, no ring modulation, no hard sync, no OSC3/ENV3
/// readback. These land in later phases.
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
    private readonly byte[] _registers = new byte[RegisterCount];
    private readonly Voice[] _voices = new Voice[VoiceCount];

    // Bresenham resampling: every SID cycle add _sampleRateHz; emit a sample whenever the
    // accumulator crosses _sidClockHz, then subtract. Exact integer math, no float drift.
    private int _sampleRateCounter;

    public SidSampleCore(int sampleRateHz = DefaultSampleRateHz, int sidClockHz = PalSidClockHz)
    {
        if (sampleRateHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRateHz), sampleRateHz, "Sample rate must be positive.");
        if (sidClockHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(sidClockHz), sidClockHz, "SID clock must be positive.");
        _sampleRateHz = sampleRateHz;
        _sidClockHz = sidClockHz;

        for (int i = 0; i < VoiceCount; i++)
            _voices[i] = new Voice();
    }

    public int SampleRateHz => _sampleRateHz;
    public int SidClockHz => _sidClockHz;

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
        }

        // Reset all chip state so warmup leaves no audible trace.
        Array.Clear(_registers, 0, _registers.Length);
        for (int i = 0; i < VoiceCount; i++)
            _voices[i] = new Voice();
        _sampleRateCounter = 0;
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

        // Waveform: bits 4-7. Phase 1 = single waveform only. If multiple are selected, pick the
        // lowest-numbered one (combined waveforms come in Phase 2).
        int waveBits = (value >> 4) & 0x0F;
        v.Waveform = waveBits switch
        {
            0 => Waveform.None,
            var w when (w & 1) != 0 => Waveform.Triangle,
            var w when (w & 2) != 0 => Waveform.Sawtooth,
            var w when (w & 4) != 0 => Waveform.Pulse,
            var w when (w & 8) != 0 => Waveform.Noise,
            _ => Waveform.None,
        };

        // TEST bit (bit 3): resets the phase accumulator and noise LFSR while held.
        if ((value & 0x08) != 0)
        {
            v.Accumulator = 0;
            v.NoiseLfsr = 0x7FFFF8; // reSID-canonical reset value
        }
    }

    private void TickAllVoices()
    {
        for (int i = 0; i < VoiceCount; i++)
        {
            ref var v = ref _voices[i];
            TickPhaseAccumulator(ref v);
            TickEnvelope(ref v);
        }
    }

    private static void TickPhaseAccumulator(ref Voice v)
    {
        uint prevAcc = v.Accumulator;
        v.Accumulator = (prevAcc + v.Frequency) & 0x00FFFFFF; // 24-bit wrap

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
            if (v.Waveform == Waveform.None)
                continue;                                            // silent voice contributes 0
            int wave = GetWaveformOutput(ref v);                    // 0..4095
            int centered = wave - 0x800;                            // remove DC bias: -2048..+2047
            int enveloped = (centered * v.Envelope) >> 8;           // envelope=0 ⇒ silent
            mix += enveloped;
        }

        // Normalise to roughly [-1, 1] then apply master volume. The denominator is
        // VoiceCount × 2048 (max signed magnitude per voice after DC removal and envelope).
        return mix * (masterVol / 15f) / (VoiceCount * 2048f);
    }

    private static int GetWaveformOutput(ref Voice v)
    {
        switch (v.Waveform)
        {
            case Waveform.None:
                return 0;

            case Waveform.Triangle:
            {
                uint a = v.Accumulator;
                uint folded = (a & 0x800000) != 0 ? ~a : a;
                return (int)((folded >> 11) & 0xFFE); // 12-bit, LSB always 0 (real SID drops LSB)
            }

            case Waveform.Sawtooth:
                return (int)((v.Accumulator >> 12) & 0xFFF);

            case Waveform.Pulse:
            {
                // Output high (0xFFF) when the top 12 bits of the accumulator are >= pulse width,
                // low (0) otherwise. PW=0 ⇒ always low; PW=0xFFF ⇒ always high (real SID quirk).
                uint accTop = (v.Accumulator >> 12) & 0xFFF;
                return accTop >= v.PulseWidth ? 0xFFF : 0;
            }

            case Waveform.Noise:
            {
                // Standard reSID 8-bit noise tap, shifted left 4 to occupy the 12-bit waveform range.
                uint l = v.NoiseLfsr;
                int n =
                    (int)(((l >> 22) & 1) << 11) |
                    (int)(((l >> 20) & 1) << 10) |
                    (int)(((l >> 16) & 1) << 9) |
                    (int)(((l >> 13) & 1) << 8) |
                    (int)(((l >> 11) & 1) << 7) |
                    (int)(((l >> 7) & 1) << 6) |
                    (int)(((l >> 4) & 1) << 5) |
                    (int)(((l >> 2) & 1) << 4);
                return n;
            }

            default:
                return 0;
        }
    }

    private enum Waveform : byte { None, Triangle, Sawtooth, Pulse, Noise }
    private enum AdsrPhase : byte { Off, Attack, Decay, Sustain, Release }

    // Mutable struct held in an array; accessed via ref. Performance matters here because
    // TickAllVoices runs at the SID clock rate (~1 MHz).
    private struct Voice
    {
        public uint Accumulator;
        public uint NoiseLfsr;
        public ushort Frequency;
        public ushort PulseWidth;
        public Waveform Waveform;
        public bool Gate;
        public byte AttackRate;
        public byte DecayRate;
        public byte SustainLevel;
        public byte ReleaseRate;
        public byte Envelope;
        public ushort RateCounter;
        public byte ExpCounter;
        public AdsrPhase AdsrPhase;

        public Voice()
        {
            NoiseLfsr = 0x7FFFF8;
            Waveform = Waveform.None;
            AdsrPhase = AdsrPhase.Off;
        }
    }
}
