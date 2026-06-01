# Hot-path benchmark results

This file captures baselines for the `HotPathBenchmarks` micro-benchmark suite
(`benchmarks/Highbyte.DotNet6502.Benchmarks/6502/HotPathBenchmarks.cs`). Each
future PR that touches the per-instruction hot path should:

1. Re-run the suite (`dotnet run -c Release --project benchmarks/Highbyte.DotNet6502.Benchmarks`)
   with `HotPathBenchmarks` selected in `Program.cs` (the default).
2. Or run `tools/perf-compare.sh` to diff against `master` automatically and flag
   any regression ≥ 5% or any new allocation.
3. Update the relevant table below if the change is intentional (and explain
   why in the PR description).

## How to interpret the tables

- `Mean` — average per-invocation time as reported by BenchmarkDotNet.
- `Ratio` — relative to `ExecEvaluator_Check_NotTriggered` on the same run.
- `Allocated` — bytes allocated per invocation. Any value other than `-` on the
  hot-path benchmarks is a regression to investigate; the per-step path is
  expected to be allocation-free in steady state.

## Baseline — 2026-06-01 (feature/hotpath-optimize HEAD)

Environment:

- BenchmarkDotNet v0.15.6
- macOS 26.5 (25F71) / Darwin 25.5.0
- Apple M5, 1 CPU, 10 logical and 10 physical cores
- .NET SDK 10.0.201, Arm64 RyuJIT armv8.0-a, Concurrent Workstation GC

Re-run on the merge commit of this branch (or whatever the project standardises
as the perf-CI machine) and replace these numbers if they differ materially. The
Apple M5 numbers are recorded here as the first known-good snapshot so future
PRs have a concrete target until a stable perf machine is wired up.

| Method                                       |          Mean | Ratio | Allocated |
|----------------------------------------------|--------------:|------:|----------:|
| `ExecEvaluator_Check_NotTriggered`           |      1.823 ns |  1.00 |         - |
| `ExecEvaluator_Check_OneConditionConfigured` |      1.944 ns |  1.07 |         - |
| `ExecEvaluator_Check_AllConditionsConfigured`|      2.658 ns |  1.46 |         - |
| `InstructionExecutor_OneStep`                |     10.861 ns |  5.96 |         - |
| `CPU_Run_1000Instructions`                   | 12,089.872 ns |  6632 |         - |

Observations to carry into the Part 2 analysis pass:

- All five benchmarks are **allocation-free** in steady state — that's the
  property to defend.
- The `_NotTriggered` → `_AllConditionsConfigured` spread (1.82 → 2.66 ns) is
  ~+46% — that's the headroom the cached `_anyStopConditionConfigured` flag
  proposal in the feature spec is going after.
- `CPU_Run_1000Instructions` averages ~12.1 ns per instruction on M5 (~83 MHz
  effective). `InstructionExecutor_OneStep` ≈ 10.9 ns, so the per-step fixed
  cost dominates over the small program's per-iteration variance.

## Confirming `[AggressiveInlining]` folded the ExecEvaluator helpers

The `LegacyExecEvaluator.Check` refactor split the original method into three
`[MethodImpl(MethodImplOptions.AggressiveInlining)]` helpers
(`CheckInstructionStopConditions`, `CheckExecutionLimitConditions`,
`CheckProgramCounterConditions`). The benchmark class is annotated with
`[DisassemblyDiagnoser(maxDepth: 2)]`, which writes a `*-asm.md` file alongside
the result tables. To verify the JIT folds those helpers back into the caller,
open that file after a run and confirm none of the three helper names appear as
separate call sites in the disassembly of `Check`.

## History

Add a new section per merged PR that intentionally changes any number above by
≥ 5% or introduces/removes an allocation, in reverse chronological order:

```
### YYYY-MM-DD — short description (PR #NNN)

What changed and the new numbers. Example:

| Method                              | Before  | After   | Δ        |
|-------------------------------------|--------:|--------:|---------:|
| ExecEvaluator_Check_NotTriggered    | 12.3 ns | 4.5 ns  | -63%     |
```
