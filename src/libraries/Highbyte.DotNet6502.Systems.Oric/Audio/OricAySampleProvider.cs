using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Utils;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Oric.Audio;

[DisplayName("AY-3-8912 sample emulation")]
[HelpText("Reproduces the Oric's three-channel AY sound as mono PCM.")]
public sealed class OricAySampleProvider : IAudioProvider, IAudioSampleProvider
{
    private readonly OricMachine _oric;
    private readonly float[] _stagingBuffer = new float[32];
    private AudioSampleWriteCallback? _writeSamples;
    private ulong _lastCycles;
    private bool _firstInstructionSeen;

    public string Name => nameof(OricAySampleProvider);
    public int SampleRateHz => _oric.Ay.SampleRateHz;
    public int ChannelCount => 1;

    public OricAySampleProvider(OricMachine oric) => _oric = oric;

    public void Init(AudioSampleWriteCallback writeSamples) => _writeSamples = writeSamples;

    public void OnAfterInstruction()
    {
        if (_writeSamples is null)
            return;
        var now = _oric.CPU.ExecState.CyclesConsumed;
        if (!_firstInstructionSeen)
        {
            _firstInstructionSeen = true;
            _lastCycles = now;
            return;
        }

        var cycles = (int)(now - _lastCycles);
        _lastCycles = now;
        var written = _oric.Ay.AdvanceCycles(cycles, _stagingBuffer);
        if (written > 0)
            _writeSamples(_stagingBuffer.AsSpan(0, written));
    }

    public void OnEndFrame() { }
}
