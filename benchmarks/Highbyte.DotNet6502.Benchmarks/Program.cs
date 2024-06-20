using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Highbyte.DotNet6502.Benchmarks.Commodore64;

//var lab = new C64SpriteManagerBenchmark();
//lab.NumberOfSprites = 8;
//lab.Setup();
//var collision = lab.Vic2SpriteManager.GetSpriteToBackgroundCollision();
//return;

#if DEBUG

// Debugging, run benchmarks in-process, UI for selection
BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args, new DebugInProcessConfig());

#else

// Running for real:
// dotnet run -c Release

// Use UI selection
//BenchmarkSwitcher
//    .FromAssembly(typeof(Program).Assembly)
//    .Run(args);

// OR run specific benchmarks:
//var summary = BenchmarkRunner
//    .Run<Execute6502InstructionBenchmark>();

//var summary = BenchmarkRunner
//    .Run<ExecuteAll6502InstructionsBenchmark>();

var summary = BenchmarkRunner
    .Run<C64ExecuteInstructionBenchmark>();

//var summary = BenchmarkRunner
//    .Run<C64ExecuteFrameBenchmark>();

//var summary = BenchmarkRunner
//    .Run<C64SpriteManagerBenchmark>();

#endif

