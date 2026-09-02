using BenchmarkDotNet.Running;
using Highbyte.DotNet6502.CycleEnginePrototype.Benchmarks;

// dotnet run -c Release --project labs/CycleEnginePrototype/Highbyte.DotNet6502.CycleEnginePrototype.Benchmarks
if (args.Length > 0)
    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
else
    BenchmarkRunner.Run<EngineBenchmarks>();
