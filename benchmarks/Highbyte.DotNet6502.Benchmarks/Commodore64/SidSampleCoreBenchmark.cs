using BenchmarkDotNet.Attributes;
using Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;

namespace Highbyte.DotNet6502.Benchmarks.Commodore64;

/// <summary>
/// Compares <see cref="SidSampleCore.AdvanceCycles"/> cost across <see cref="SidEmulationMode"/>
/// for two workload profiles:
/// <list type="bullet">
///   <item><c>Simple</c>: one voice, plain sawtooth, no advanced features. Tests whether
///   <c>Auto</c> with its inner-loop fast paths matches <c>Fast</c> when no advanced features
///   are actually in use.</item>
///   <item><c>Complex</c>: three voices using combined waveforms, hard sync, ring modulation
///   and one voice routed through a resonant low-pass filter. Exercises every advanced path
///   that <c>Fast</c> disables.</item>
/// </list>
/// Workload size is one PAL frame (19,656 cycles) — the batch the audio coordinator actually
/// processes between vsyncs.
/// </summary>
[MemoryDiagnoser]
public class SidSampleCoreBenchmark
{
    private const int PalCyclesPerFrame = 19656;
    private const int PrimingFrames = 20;

    [Params(SidEmulationMode.Auto, SidEmulationMode.Fast)]
    public SidEmulationMode Mode;

    private SidSampleCore _simpleCore = default!;
    private SidSampleCore _complexCore = default!;
    private float[] _buffer = default!;

    [GlobalSetup]
    public void Setup()
    {
        _buffer = new float[2048];
        _simpleCore = BuildSimple(Mode);
        _complexCore = BuildComplex(Mode);

        // Prime: a few frames' worth of advance so the JIT has tier-1 compiled the hot methods
        // and the filter state has settled. Without this the first measured iteration would pay
        // tiered-JIT cost we'd otherwise attribute to one mode.
        for (int i = 0; i < PrimingFrames; i++)
        {
            _simpleCore.AdvanceCycles(PalCyclesPerFrame, _buffer);
            _complexCore.AdvanceCycles(PalCyclesPerFrame, _buffer);
        }
    }

    [Benchmark]
    public int Simple()
        => _simpleCore.AdvanceCycles(PalCyclesPerFrame, _buffer);

    [Benchmark]
    public int Complex()
        => _complexCore.AdvanceCycles(PalCyclesPerFrame, _buffer);

    private static SidSampleCore BuildSimple(SidEmulationMode mode)
    {
        // Voice 1: plain sawtooth, ~440 Hz, sustaining. Voices 2 and 3 silent. No filter.
        var core = new SidSampleCore(mode: mode);
        core.WriteRegister(0x00, 0x45);          // FRELO1
        core.WriteRegister(0x01, 0x1D);          // FREHI1
        core.WriteRegister(0x05, 0x00);          // ATDCY1 — fastest attack/decay
        core.WriteRegister(0x06, 0xF0);          // SUREL1 — sustain max
        core.WriteRegister(0x18, 0x0F);          // SIGVOL — master vol 15
        core.WriteRegister(0x04, 0x21);          // VCREG1 — sawtooth + gate
        return core;
    }

    private static SidSampleCore BuildComplex(SidEmulationMode mode)
    {
        // Voice 1: hard-sync source, sawtooth, high freq for frequent MSB rollovers.
        // Voice 2: combined saw+triangle, hard-synced from voice 1.
        // Voice 3: triangle + ring modulation (source = voice 2), routed through LP filter
        //          with mid cutoff and mid resonance.
        var core = new SidSampleCore(mode: mode);

        // Voice 1.
        core.WriteRegister(0x00, 0xFF); core.WriteRegister(0x01, 0x40);
        core.WriteRegister(0x05, 0x00); core.WriteRegister(0x06, 0xF0);
        core.WriteRegister(0x04, 0x21);

        // Voice 2 (saw + triangle, hard sync, gate).
        core.WriteRegister(0x07, 0x00); core.WriteRegister(0x08, 0x10);
        core.WriteRegister(0x0C, 0x00); core.WriteRegister(0x0D, 0xF0);
        core.WriteRegister(0x0B, 0x33);

        // Voice 3 (triangle, ring mod, gate).
        core.WriteRegister(0x0E, 0x00); core.WriteRegister(0x0F, 0x20);
        core.WriteRegister(0x13, 0x00); core.WriteRegister(0x14, 0xF0);
        core.WriteRegister(0x12, 0x15);

        // Filter: route V3 through filter, mid cutoff, resonance=7, low-pass enabled.
        core.WriteRegister(0x15, 0x00);          // FCLO
        core.WriteRegister(0x16, 0x80);          // FCHI (~6 kHz)
        core.WriteRegister(0x17, 0x74);          // RESON: res=7, V3 routed
        core.WriteRegister(0x18, 0x1F);          // SIGVOL: LP enable + master vol 15
        return core;
    }
}
