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

### 2026-06-02 — `Memory_Read_TightLoop` / `Memory_Write_TightLoop` benchmarks added

Two new direct-memory benchmarks added as a permanent baseline for any future
work on `Memory.Read` / `Memory.Write` dispatch. They exercise a tight loop
of 1024 byte reads / writes through a default RAM mapping, summing into a
returned value so the JIT cannot fold the loop away.

| Method                                  |          Mean | Allocated |
|-----------------------------------------|--------------:|----------:|
| `Memory_Read_TightLoop` (1024 reads/op) |        901 ns |         - |
| `Memory_Write_TightLoop` (1024 writes/op)|       832 ns |         - |

Captured on Apple M5 / .NET 10.0.5 / Arm64. ~0.88 ns/read, ~0.81 ns/write.

These rows are the **delegate-path baseline** that any future Memory dispatch
optimization should compare against. A prototype "byte[]? fast path" was
explored on 2026-06-02 and reverted — it hit the perf targets but added
~50 MB of per-C64-instance allocation and significant `Memory` class
complexity for only a −3% win on the realistic-workload benchmark.

### 2026-06-01 — hot-path Part 2 round 1: candidates 1, 4, 2, 6

Four optimizations landed together on `feature/hotpath-optimize` after the Part 2 analysis pass:

1. Cached `_anyStopConditionConfigured` flag in `LegacyExecEvaluator`.
2. Cached `ExecuteUntilInstruction` byte and replaced
   `ExecuteUntilInstructions.Contains` with a `bool[256]` bitmap.
3. Replaced `InstructionList.Dictionary<byte, T>` lookups with parallel
   byte-indexed arrays (`OpCode?[256]`, `Instruction?[256]`).
4. Guarded EventArgs construction in `CPU.Execute` by handler-presence checks
   and eliminated the per-instruction `ExecState.ExecStateAfterInstruction()`
   allocation when no `InstructionExecuted` handler is attached.

Also added two new benchmarks for the full `Execute` path:
`CPU_Execute_NoSubscribers_1000Instructions` and
`CPU_Execute_WithSubscribers_1000Instructions`.

| Method                                       |       Baseline |          After |        Δ |
|----------------------------------------------|---------------:|---------------:|---------:|
| `ExecEvaluator_Check_NotTriggered`           |        1.82 ns |        0.41 ns | **−77%** |
| `ExecEvaluator_Check_OneConditionConfigured` |        1.94 ns |        1.89 ns |      −3% |
| `ExecEvaluator_Check_AllConditionsConfigured`|        2.66 ns |        1.97 ns | **−26%** |
| `InstructionExecutor_OneStep`                |       10.86 ns |        9.35 ns | **−14%** |
| `CPU_Run_1000Instructions`                   |       12.09 µs |        9.79 µs | **−19%** |
| `CPU_Execute_NoSubscribers_1000Instructions` |     (new)      |       11.84 µs |        — |
| `CPU_Execute_WithSubscribers_1000Instructions`|     (new)     |       24.56 µs |        — |

Allocations (per 1000 instructions):

| Method                                         | Baseline | After    | Δ           |
|------------------------------------------------|---------:|---------:|------------:|
| `CPU_Execute_NoSubscribers_1000Instructions`   |  136 KB* |    136 B | **−99.9%**  |
| `CPU_Execute_WithSubscribers_1000Instructions` |  136 KB* |   136 KB | unchanged   |

\* Pre-fix baseline for the new full-Execute benchmarks (measured against the
post-#1+#4+#2 code, i.e. after the first three candidates landed and before
candidate 6 was applied — both no-sub and with-sub paths allocated 136,136 B
per 1000-instruction run).

Notes:

- The `OneStep` / `Run_1000` cumulative improvement (−14% / −19%) is real but
  slightly smaller than the post-#4 intermediate (where `OneStep` measured
  8.79 ns). Candidate 6 introduces a ~0.6 ns regression on the minimal path,
  most likely because the larger `Execute` method body shifts the JIT's
  inlining decisions for sibling methods (`ProcessInterrupts`) that the
  minimal path also calls. Net effect is still a clear win across all
  benchmarks.
- `Check_OneCondition` improvement is small because the benchmark fixture only
  configures `MaxNumberOfInstructions`, which the cached-flag and bitmap
  changes don't directly touch; the value-add for that scenario shows up in
  the `_AllConditions` row instead.
- Confirmed allocation-free on all minimal-path benchmarks
  (`MemoryDiagnoser` reports 0 B/op).
- All 948 tests pass (715 in `Highbyte.DotNet6502.Tests` + 233 in
  `Highbyte.DotNet6502.Systems.Tests`).
