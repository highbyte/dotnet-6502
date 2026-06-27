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

## C64 hot-path baseline — 2026-06-02 (feature/hotpath-optimize HEAD)

These baselines cover the integrated C64 execution path with the four first-pass
provider permutations:

- `CoreOnly`
- `RenderOnly` (`Vic2Rasterizer`)
- `AudioOnly` (`C64SidSampleProvider`)
- `RenderAndAudio` (`Vic2Rasterizer` + `C64SidSampleProvider`)

Run them with:

1. `dotnet run -c Release --project benchmarks/Highbyte.DotNet6502.Benchmarks -- --filter '*C64ExecuteInstructionBenchmark*'`
2. `dotnet run -c Release --project benchmarks/Highbyte.DotNet6502.Benchmarks -- --filter '*C64ExecuteFrameBenchmark*'`

The benchmark executable now keeps `HotPathBenchmarks` as the no-args default,
but switches to `BenchmarkSwitcher` when args are supplied so filtered C64 runs
work without editing `Program.cs`.

### `C64ExecuteInstructionBenchmark`

ShortRun / Apple M5 / .NET 10.0.5 / Arm64:

| Scenario | 1 instruction | 100 instructions | 1000 instructions | Allocated |
|----------|--------------:|-----------------:|------------------:|----------:|
| `CoreOnly` | 13.64 ns | 1.596 us | 16.286 us | - |
| `RenderOnly` | 29.97 ns | 3.609 us | 35.609 us | - |
| `AudioOnly` | 23.88 ns | 2.850 us | 29.228 us | - |
| `RenderAndAudio` | 48.56 ns | 5.742 us | 59.975 us | - |

### `C64ExecuteFrameBenchmark`

ShortRun / Apple M5 / .NET 10.0.5 / Arm64:

`SpriteScenario = None`

| Scenario | 1 frame | Allocated |
|----------|--------:|----------:|
| `CoreOnly` | 132.4 us | - |
| `RenderOnly` | 301.9 us | - |
| `AudioOnly` | 240.2 us | - |
| `RenderAndAudio` | 420.0 us | - |

`SpriteScenario = MixedVisibleSprites`

| Scenario | 1 frame | Allocated |
|----------|--------:|----------:|
| `CoreOnly` | 171.4 us | - |
| `RenderOnly` | 335.3 us | - |
| `AudioOnly` | 260.7 us | - |
| `RenderAndAudio` | 527.4 us | - |

Observations to carry into the next optimization pass:

- The benchmark matrix is now **allocation-free per instruction** in all four
  scenarios.
- The frame benchmark is now **allocation-free in all four scenarios** after
  removing a per-frame LINQ `OrderByDescending(...)` call from the rasterizer's
  sprite pass.
- The frame benchmark now also has a visible-sprite workload, which exercises:
  - sprite collision work in `C64.ExecuteOneFrame()`
  - rasterizer sprite drawing in `Vic2RasterizerUintPixelGenerator.OnEndFrame()`
  - sprite priority / multicolor / width-height expansion branches
- On this machine, render cost is currently higher than sample-audio cost, and
  the combined scenario scales roughly additively, which makes the suite useful
  for validating future render/audio refactors independently.

## C64 sprite rendering: per-frame vs per-line — 2026-06-28 (feature/c64-sprite-multiplex)

`C64SpriteRenderBenchmark` compares `Vic2Rasterizer` sprite rendering cost for the
**same** 8 static sprites (mix of single/multi colour and X/Y expansion) between the
default end-of-frame path (`PerLineSprites=False`) and the opt-in per-raster-line
multiplex path (`PerLineSprites=True`, `C64Config.Vic2RasterizerPerLineSprites`). One
NTSC frame is simulated by advancing the raster in instruction-sized cycle chunks and
calling the rasterizer directly (no CPU), so only the rendering path is measured —
including the per-line latch/snapshot scan and, for the per-frame path, the
`StoreRasterLineIORegisters` snapshot cost.

Run:

    dotnet run -c Release --project benchmarks/Highbyte.DotNet6502.Benchmarks -- --filter '*C64SpriteRenderBenchmark*'

Apple M5 / .NET 10.0.5 / Arm64 / DefaultJob. The whole-frame time is dominated by the
full-screen text background (identical in both modes); the **per-line vs per-frame delta**
is the signal. `NumberOfSprites=0` isolates the per-line fixed scan overhead; `Sparse`
toggles full-opaque vs realistic (~half rows empty) sprite shapes.

| PerLineSprites | NumberOfSprites | Sparse | Mean | Allocated |
|---------------:|----------------:|-------:|-----:|----------:|
| False | 0 | False  |  97.2 us | - |
| False | 0 | True   |  97.7 us | - |
| False | 8 | False  | 114.8 us | - |
| False | 8 | True   | 103.1 us | - |
| True  | 0 | False  |  98.9 us | - |
| True  | 0 | True   |  98.5 us | - |
| True  | 8 | False  | 116.1 us | - |
| True  | 8 | True   | 109.0 us | - |

Within-run deltas (per-line minus per-frame), both paths allocation-free:

- **No sprites enabled (idle scan):** ~+1.2 us/frame.
- **8 full opaque sprites:** ~+1.3 us (+1.1%) — on par; the heavy pixel-draw work
  dominates and roughly cancels the per-line overhead.
- **8 sparse / realistic sprites:** ~+5.9 us (+5.7%) — the end-of-frame path skips empty
  sprite rows very cheaply, while the per-line path pays a fixed per-line scan + per-sprite
  Y reads + a 63-byte band copy per displayed band regardless of fill. Marginal sprite draw
  cost (8 − 0) is ~5.4 us (per-frame) vs ~10.5 us (per-line) for the sparse shape.

Takeaway: for the same sprite count the per-line path is roughly even for pixel-dense
sprites and ~5–6% slower per frame for realistic sparse sprites; the cost is per-line
overhead, not the drawing. The end-of-frame path is unchanged and used whenever the flag
is off.

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

### 2026-06-28 — per-line sprite scan reads `$D015` once

The per-line sprite path (`Vic2RasterizerUintPixelGenerator`) sampled each sprite's
enable+Y via up to 16 `ReadIOStorage` calls on **every** visible raster line. It now reads
the sprite-enable register (`$D015`) once per line, bit-tests it for the trigger, and only
samples Y for enabled sprites — skipping all per-sprite reads on lines where no sprite is
enabled.

Measured via `C64SpriteRenderBenchmark` on Apple M5 / .NET 10.0.5, the per-line **fixed
overhead with no sprites enabled** (per-line minus end-of-frame at `NumberOfSprites=0`)
dropped from ~+5.2 us to ~+1.2 us per frame. The enabled-sprite cases improve modestly (the
16→9 reads/line saving); the remaining per-line overhead for enabled sparse sprites is the
per-sprite Y reads, the active-run gate loop, and the 63-byte band copy. Still
allocation-free; all 407 `Highbyte.DotNet6502.Systems.Tests` pass.

### 2026-06-02 — standard-text background is prefilled once per line

The rasterizer's standard-text path used to write the same background-color span
once per character cell. It now fills the standard-text background once when a
new line starts, then only writes per-cell foreground pixels. The same change
also stops mutating the cached line-level character mode while handling
multicolor-text fallback characters.

Measured on Apple M5 / .NET 10.0.5 / ShortRun against the previous local
rasterizer/audio baseline:

| Scenario | `None` before | `None` after | Δ | `MixedVisibleSprites` before | `MixedVisibleSprites` after | Δ | Allocated |
|----------|--------------:|-------------:|--:|-----------------------------:|----------------------------:|--:|----------:|
| `RenderOnly` | 248.6 us | 228.2 us | -8% | 258.0 us | 232.2 us | -10% | - / - |
| `RenderAndAudio` | 372.6 us | 340.7 us | -9% | 370.7 us | 348.3 us | -6% | - / - |

### 2026-06-02 — SID quiescent voices skip per-cycle ticking

`SidSampleCore` now skips fully quiescent voices on the existing no-sync fast
path instead of ticking all three voices every SID cycle. Voices still tick
normally as soon as they have active ADSR state, TEST bit state, or a
non-zero frequency.

Measured on Apple M5 / .NET 10.0.5:

`SidSampleCoreBenchmark` against a clean `HEAD` baseline:

| Mode | `Simple` before | `Simple` after | Δ | `Complex` before | `Complex` after | Δ | Allocated |
|------|----------------:|---------------:|--:|-----------------:|----------------:|--:|----------:|
| `Auto` | 55.88 us | 53.30 us | -5% | 123.21 us | 115.79 us | -6% | - / - |
| `Fast` | 56.11 us | 52.16 us | -7% | 88.77 us | 83.21 us | -6% | - / - |

Integrated `C64ExecuteFrameBenchmark` audio-only scenarios against the previous
local rasterizer baseline:

| Scenario | `None` before | `None` after | Δ | `MixedVisibleSprites` before | `MixedVisibleSprites` after | Δ | Allocated |
|----------|--------------:|-------------:|--:|-----------------------------:|----------------------------:|--:|----------:|
| `AudioOnly` | 240.2 us | 215.2 us | -10% | 233.6 us | 222.8 us | -5% | - / - |

### 2026-06-02 — rasterizer lookup tables use indexed caches

The per-instruction text/bitmap rasterizer path used dictionary lookups for the
precomputed border and 8-pixel pattern caches. Replacing those hot-path
lookups with packed-index tables removes the hashing overhead while keeping the
same cached pixel payloads and allocation-free behavior.

Measured on Apple M5 / .NET 10.0.5 / ShortRun against the previous
`sparse sprite row and byte skips` baseline:

| Scenario | `None` before | `None` after | Δ | `MixedVisibleSprites` before | `MixedVisibleSprites` after | Δ | Allocated |
|----------|--------------:|-------------:|--:|-----------------------------:|----------------------------:|--:|----------:|
| `RenderOnly` | 289.1 us | 243.6 us | -16% | 278.9 us | 246.7 us | -12% | - / - |
| `RenderAndAudio` | 456.6 us | 378.2 us | -17% | 461.5 us | 380.6 us | -18% | - / - |

### 2026-06-02 — sparse sprite row and byte skips

The sprite-heavy frame path now caches which sprite rows actually contain
pixels, then uses that information to skip empty rows in both collision
detection and rasterizer sprite drawing. The rasterizer sprite loop also skips
fully transparent sprite bytes before entering the per-pixel decode work.

Measured on Apple M5 / .NET 10.0.5 / ShortRun against the last committed
sprite-collision baseline:

| Scenario | `MixedVisibleSprites` before | `MixedVisibleSprites` after | Δ | Allocated |
|----------|-----------------------------:|----------------------------:|--:|----------:|
| `CoreOnly` | 141.1 us | 131.4 us | -7% | - / - |
| `RenderOnly` | 303.8 us | 278.9 us | -8% | - / - |
| `AudioOnly` | 234.4 us | 225.1 us | -4% | - / - |
| `RenderAndAudio` | 491.1 us | 461.5 us | -6% | - / - |

### 2026-06-02 — sprite collision prefilter and early exit

`Vic2SpriteManager.GetSpriteToSpriteCollision()` now rejects sprite pairs whose
screen-space bounds do not overlap before entering the per-line collision work,
and stops scanning additional lines once a colliding pair has already been
found.

This avoids unnecessary calls into the expensive row normalization and byte
alignment helpers for the staggered visible-sprite benchmark workload, while
keeping the path allocation-free.

Measured on Apple M5 / .NET 10.0.5 / ShortRun:

| Scenario | `MixedVisibleSprites` before | `MixedVisibleSprites` after | Δ | Allocated |
|----------|-----------------------------:|----------------------------:|--:|----------:|
| `CoreOnly` | 171.4 us | 141.1 us | -18% | - / - |
| `RenderOnly` | 335.3 us | 303.8 us | -9% | - / - |
| `AudioOnly` | 260.7 us | 234.4 us | -10% | - / - |
| `RenderAndAudio` | 527.4 us | 491.1 us | -7% | - / - |

The focused sprite-manager micro-benchmark also remains allocation-free after
the change:

| Method | Mean | Allocated |
|--------|-----:|----------:|
| `GetSpriteToSpriteCollissions` | 17.08 us | - |
| `GetSpriteToBackgroundCollissions` | 13.36 us | - |

### 2026-06-02 — C64 rasterizer frame path: remove per-frame LINQ allocation

`Vic2RasterizerUintPixelGenerator.DrawSpritesToBitmapBackedByPixelArray()` used
`vic2.SpriteManager.Sprites.OrderByDescending(s => s.SpriteNumber)` on every
frame to draw sprites back-to-front. That LINQ sort allocated 408 B per frame in
the `C64ExecuteFrameBenchmark` rasterizer-enabled scenarios.

Replacing it with a simple reverse index loop preserves draw order and removes
the allocation entirely:

| Scenario | Before | After | Allocated before | Allocated after |
|----------|-------:|------:|-----------------:|----------------:|
| `RenderOnly` | 302.6 us | 298.4 us | 408 B | - |
| `RenderAndAudio` | 458.1 us | 454.4 us | 408 B | - |

### 2026-06-02 — C64 frame benchmark: add visible-sprite workload axis

`C64ExecuteFrameBenchmark` now includes `SpriteScenario = MixedVisibleSprites`
in addition to the previous no-sprite baseline. The mixed sprite setup enables 8
visible sprites spanning:

- standard + multicolor sprite modes
- double-width and double-height expansion
- priority-over-foreground and behind-foreground cases

This extends the frame benchmark from “text/bitmap/background/border rasterizer
cost” to also covering sprite drawing and sprite-collision work.

| Scenario | None | MixedVisibleSprites | Δ | Allocated |
|----------|-----:|--------------------:|--:|----------:|
| `CoreOnly` | 133.4 us | 268.1 us | +101% | - / - |
| `RenderOnly` | 306.9 us | 440.8 us | +44% | - / - |
| `AudioOnly` | 234.7 us | 365.1 us | +56% | - / - |
| `RenderAndAudio` | 469.4 us | 627.2 us | +34% | - / - |

### 2026-06-02 — sprite data cache behind dirty flag

`Vic2Sprite.Data` previously rebuilt sprite row bytes on every access, even
though the class already tracked sprite dirtiness. The sprite-heavy frame path
hits `sprite.Data` from both collision detection and rasterizer sprite drawing,
so this caused repeated redundant rebuilds.

The fix adds a dedicated sprite-data cache flag: sprite bytes are rebuilt only
when the pointer/data content changes, while the broader `IsDirty` flag keeps
its existing meaning for other render providers.

Measured on the Apple M5 / .NET 10.0.5 ShortRun benchmark:

| Scenario | MixedVisibleSprites before | MixedVisibleSprites after | Δ | Allocated |
|----------|---------------------------:|--------------------------:|--:|----------:|
| `CoreOnly` | 268.1 us | 171.4 us | -36% | - / - |
| `RenderOnly` | 440.8 us | 335.3 us | -24% | - / - |
| `AudioOnly` | 365.1 us | 260.7 us | -29% | - / - |
| `RenderAndAudio` | 627.2 us | 527.4 us | -16% | - / - |

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
