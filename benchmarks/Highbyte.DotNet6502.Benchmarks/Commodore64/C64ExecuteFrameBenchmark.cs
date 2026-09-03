using BenchmarkDotNet.Attributes;
using Highbyte.DotNet6502.Systems.Commodore64;

namespace Highbyte.DotNet6502.Benchmarks.Commodore64;

[Config(typeof(C64HotPathConfig))]
public class C64ExecuteFrameBenchmark
{
    private C64 _c64 = default!;

    [Params(
        C64HotPathScenario.CoreOnly,
        C64HotPathScenario.RenderOnly,
        C64HotPathScenario.AudioOnly,
        C64HotPathScenario.RenderAndAudio)]
    public C64HotPathScenario Scenario;

    [Params(
        C64SpriteScenario.None,
        C64SpriteScenario.MixedVisibleSprites)]
    public C64SpriteScenario SpriteScenario;

    [Params(1)]
    public int NumberOfFramesToExecute;

    [GlobalSetup]
    public void Setup()
    {
        _c64 = C64HotPathBenchmarkSupport.CreateScenario(Scenario, SpriteScenario);
    }

    [Benchmark]
    public void ExecuteFrames()
    {
        _c64.CPU.PC = C64HotPathBenchmarkSupport.StartAddress;
        for (var i = 0; i < NumberOfFramesToExecute; i++)
        {
            _c64.ExecuteOneFrame();
        }
    }
}
