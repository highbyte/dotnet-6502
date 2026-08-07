using Highbyte.DotNet6502.Systems.Apple2.Audio.Sample;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// Runs the genuine ROM's bell through the audio path and measures what comes out.
///
/// This is the test that matters for audio. The resampler's own tests feed it a synthetic toggle
/// pattern, which only proves it is self-consistent; here the toggles come from real 6502 code
/// whose timing we do not control, through the speaker, the provider and the resampler. If the
/// cycle accounting, the toggle plumbing or the sample-rate conversion were wrong, a tone the ROM
/// intends to be about 1 kHz would come out at the wrong pitch — or not at all.
/// </summary>
[Trait("TestType", "Integration")]
public class Apple2RealRomBellAudioTests
{
    /// <summary>
    /// BELL1 in the monitor ROM: <c>LDY #$C0</c> around <c>LDA $C030</c> with a WAIT delay, i.e.
    /// 192 speaker toggles at roughly 1 kHz — the ~0.1 second beep.
    /// </summary>
    private const ushort Bell1Address = 0xFBDD;

    private const ushort ReturnSentinel = 0x9000;
    private const int SampleRate = 44100;

    private readonly ITestOutputHelper _output;

    public Apple2RealRomBellAudioTests(ITestOutputHelper output) => _output = output;

    private static Apple2System BootRealRom()
    {
        var romPath = Apple2TestRoms.ResolveSystemRomPath();
        Assert.NotNull(romPath);

        var romData = new Dictionary<string, byte[]>
        {
            { Apple2SystemConfig.SYSTEM_ROM_NAME, File.ReadAllBytes(romPath) },
        };
        var apple2 = new Apple2System(new Apple2Config(), NullLoggerFactory.Instance, romData);
        for (var frame = 0; frame < 180; frame++)
            apple2.ExecuteOneFrame();
        return apple2;
    }

    /// <summary>Runs the bell, returning every PCM sample the provider produced.</summary>
    private static List<float> RunBell(Apple2System apple2)
    {
        var samples = new List<float>();
        var provider = new Apple2SpeakerSampleProvider(apple2, SampleRate);
        provider.Init(written =>
        {
            foreach (var sample in written)
                samples.Add(sample);
            return written.Length;
        });

        var cpu = apple2.CPU;
        var mem = apple2.Mem;

        var returnMinusOne = ReturnSentinel - 1;
        mem[(ushort)(0x0100 + cpu.SP)] = (byte)(returnMinusOne >> 8);
        cpu.SP--;
        mem[(ushort)(0x0100 + cpu.SP)] = (byte)(returnMinusOne & 0xFF);
        cpu.SP--;

        cpu.PC = Bell1Address;

        // The bell is ~110,000 cycles; a generous instruction cap only guards against a hang.
        for (var step = 0; step < 200_000 && cpu.PC != ReturnSentinel; step++)
        {
            cpu.ExecuteOneInstruction(mem);
            provider.OnAfterInstruction();
        }

        Assert.Equal(ReturnSentinel, cpu.PC);
        return samples;
    }

    private static double MeasureFrequencyHz(List<float> samples)
    {
        // Count rising zero crossings over the part where the tone is established.
        var settled = samples.Skip(samples.Count / 10).ToList();
        var crossings = 0;
        for (var i = 1; i < settled.Count; i++)
        {
            if (settled[i - 1] <= 0f && settled[i] > 0f)
                crossings++;
        }
        return crossings / (settled.Count / (double)SampleRate);
    }

    [RequiresApple2RomFact]
    public void The_Rom_Bell_Produces_Audible_Samples()
    {
        var apple2 = BootRealRom();

        var samples = RunBell(apple2);
        var peak = samples.Count == 0 ? 0f : samples.Max(Math.Abs);

        _output.WriteLine($"{samples.Count} samples, peak {peak:F4}");

        // ~0.1 s at 44.1 kHz.
        Assert.InRange(samples.Count, 3_000, 8_000);
        Assert.True(peak > 0.05f, $"The bell produced no audible signal (peak {peak}).");
    }

    /// <summary>
    /// The pitch is the part a listener judges instantly, and it comes straight from cycle
    /// accounting — the same chain the backward-branch cycle bug would have detuned by ~9%.
    /// </summary>
    [RequiresApple2RomFact]
    public void The_Rom_Bell_Is_Around_One_Kilohertz()
    {
        var apple2 = BootRealRom();

        var measured = MeasureFrequencyHz(RunBell(apple2));
        _output.WriteLine($"measured {measured:F1} Hz");

        // BELL1's loop is a WAIT #$0C plus overhead, ~558 cycles per half period on a 1.02 MHz
        // machine, so a little under 1 kHz. The window is wide enough not to encode the exact
        // instruction timing, tight enough to catch a wrong sample rate or lost cycles.
        Assert.InRange(measured, 800.0, 1_100.0);
    }

    [RequiresApple2RomFact]
    public void Silence_Before_The_Bell_Is_Actually_Silent()
    {
        var apple2 = BootRealRom();

        var samples = new List<float>();
        var provider = new Apple2SpeakerSampleProvider(apple2, SampleRate);
        provider.Init(written =>
        {
            foreach (var sample in written)
                samples.Add(sample);
            return written.Length;
        });

        // Run the idle keyboard-polling loop; nothing touches $C030.
        for (var i = 0; i < 50_000; i++)
        {
            apple2.CPU.ExecuteOneInstruction(apple2.Mem);
            provider.OnAfterInstruction();
        }

        var peak = samples.Count == 0 ? 0f : samples.Max(Math.Abs);
        _output.WriteLine($"{samples.Count} samples, peak {peak:F6}");

        Assert.True(samples.Count > 0, "Expected samples to be produced even in silence.");
        Assert.True(peak < 0.001f, $"Idle machine should be silent, but peaked at {peak}.");
    }
}
