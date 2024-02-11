using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Highbyte.DotNet6502.Benchmarks;

// If debugging, uncomment line below
//BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new DebugInProcessConfig());

// Or if just wanting a UI to select which benchmark to run, uncomment line below
//BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

// Run benchmarks
var summary = BenchmarkRunner.Run<ExecuteInstructions>();
