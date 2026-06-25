using BenchmarkDotNet.Attributes;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Benchmarks.Commodore64;

/// <summary>
/// Micro-benchmarks for the C64 CIA timer hot path.
///
/// The normal C64 frame benchmarks currently use <see cref="TimerMode.UpdateEachRasterLine"/>,
/// while timing-sensitive cartridges such as Expert need timers updated on each
/// executed instruction. These benchmarks isolate that cost without renderer/audio
/// noise.
/// </summary>
[Config(typeof(C64HotPathConfig))]
public class C64CiaTimerBenchmark
{
    private const int TimerProcessCallsPerOperation = 1024;

    private C64 _c64WithStoppedTimers = default!;
    private C64 _c64WithRunningTimers = default!;
    private C64 _c64WithUnderflowingTimers = default!;

    [GlobalSetup]
    public void Setup()
    {
        _c64WithStoppedTimers = CreateC64();

        _c64WithRunningTimers = CreateC64();
        ConfigureTimerB(_c64WithRunningTimers, latch: 0x7fff, enableInterrupt: true);

        _c64WithUnderflowingTimers = CreateC64();
        ConfigureTimerB(_c64WithUnderflowingTimers, latch: 1, enableInterrupt: true);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = TimerProcessCallsPerOperation)]
    public void ProcessAllCiaTimers_Stopped()
    {
        for (var i = 0; i < TimerProcessCallsPerOperation; i++)
            ProcessAllCiaTimers(_c64WithStoppedTimers, cyclesExecuted: 2);
    }

    [Benchmark(OperationsPerInvoke = TimerProcessCallsPerOperation)]
    public void ProcessAllCiaTimers_Running_NoUnderflow()
    {
        for (var i = 0; i < TimerProcessCallsPerOperation; i++)
            ProcessAllCiaTimers(_c64WithRunningTimers, cyclesExecuted: 2);
    }

    [Benchmark(OperationsPerInvoke = TimerProcessCallsPerOperation)]
    public void ProcessAllCiaTimers_Running_FrequentUnderflow()
    {
        for (var i = 0; i < TimerProcessCallsPerOperation; i++)
            ProcessAllCiaTimers(_c64WithUnderflowingTimers, cyclesExecuted: 2);
    }

    private static C64 CreateC64()
        => C64.BuildC64(new C64Config
        {
            C64Model = "C64NTSC",
            Vic2Model = "NTSC",
            LoadROMs = false,
            TimerMode = TimerMode.UpdateEachInstruction,
            AudioEnabled = false,
            RenderProviderType = null,
            InstrumentationEnabled = false,
        }, NullLoggerFactory.Instance);

    private static void ConfigureTimerB(C64 c64, ushort latch, bool enableInterrupt)
    {
        c64.Cia2.TimerBLOStore(0, (byte)(latch & 0xff));
        c64.Cia2.TimerBHIStore(0, (byte)(latch >> 8));
        if (enableInterrupt)
            c64.Cia2.InterruptControlStore(0, 0x82);
        c64.Cia2.TimerBControlStore(0, 0x11);
    }

    private static void ProcessAllCiaTimers(C64 c64, ulong cyclesExecuted)
    {
        c64.Cia1.ProcessTimers(cyclesExecuted);
        c64.Cia2.ProcessTimers(cyclesExecuted);
    }
}
