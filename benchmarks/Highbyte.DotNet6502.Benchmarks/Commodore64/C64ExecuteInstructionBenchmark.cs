using BenchmarkDotNet.Attributes;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64;

namespace Highbyte.DotNet6502.Benchmarks.Commodore64;

[Config(typeof(C64HotPathConfig))]
public class C64ExecuteInstructionBenchmark
{
    private C64 _c64 = default!;

    [Params(
        C64HotPathScenario.CoreOnly,
        C64HotPathScenario.RenderOnly,
        C64HotPathScenario.AudioOnly,
        C64HotPathScenario.RenderAndAudio)]
    public C64HotPathScenario Scenario;

    [Params(1, 100, 1000)]
    public int NumberOfInstructionsToExecute;

    [Params(TimerMode.UpdateEachRasterLine, TimerMode.UpdateEachInstruction)]
    public TimerMode TimerMode;

    [GlobalSetup]
    public void Setup()
    {
        _c64 = C64HotPathBenchmarkSupport.CreateScenario(Scenario, timerMode: TimerMode);
    }

    [Benchmark]
    public void ExecuteInstructions()
    {
        _c64.CPU.PC = C64HotPathBenchmarkSupport.StartAddress;
        for (var i = 0; i < NumberOfInstructionsToExecute; i++)
        {
            _c64.ExecuteOneInstruction(out _);
        }
    }
}
