# Cycle engine prototype

A throwaway lab that compared candidate CPU execution engines for cycle-level timing before any
of them was allowed to replace the production executor. It is deliberately isolated from the
libraries: nothing under `src/` references it. The choice has been made: the cycle-stamped atomic
design with lazy device synchronization. The two resumable candidates that were also built and
measured (a micro-operation table and a flattened state machine) have been removed; their code is
in the repository history and their numbers are kept below. What remains is the reference for
migrating the design into `Highbyte.DotNet6502`, after which the lab is deleted.

## Question it answers

The production executor runs each instruction atomically through one pre-composed handler and
returns the total cycle count; systems advance their devices afterwards by that delta. Cycle-level
timing needs every bus access on its real cycle, read cycles that can stall while the VIC-II holds
BA low, and devices interleaved with the CPU. Three designs can provide that, and they differ a lot
in dispatch cost and in how much of the CPU has to be rewritten:

| Engine | Class | Shape |
| --- | --- | --- |
| Atomic, cycle-stamped (chosen) | `AtomicStampedEngine` | Instruction stays atomic; every cycle is a bus access that carries the cycle number; the bus ticks devices and inserts stall cycles. Two device policies: **per-cycle** (tick per access; the simplest correct form, kept as the oracle) and **lazy** (catch-up in closed form at instruction boundaries and at the predicted next BA-low cycle; the production policy). |
| Micro-operation table (removed) | – | Resumable: each opcode an array of micro-operation codes, one per cycle, run by one central switch. |
| Flattened state machine (removed) | – | Resumable: nested switches on (opcode, cycle index), in the shape a source generator would emit. |
| Legacy | `LegacyEngine` | The production executor driven the way the C64 drives it today. Reference for cost and final state. |

All engines share the same register file (`CPU`), memory, and device stub (`SystemStub`: a
PAL VIC-II that fetches one byte per cycle and pulls BA low on bad-line cycles 12–53, and a CIA
timer that raises an IRQ on underflow).

## The slice

Every engine implements the same representative instructions, with one bus access per cycle
and the documented dummy reads: `LDA #`, `LDA abs`, `LDA abs,X` (with and without page cross),
`STA abs,X`, `INC abs` (NMOS read/write/write and CMOS read/read/write), `BNE` (not taken, taken,
taken across a page), `NOP`, `PHA`, `JSR`, `RTS`, `RTI`, and hardware IRQ/NMI entry.

## Running

```sh
dotnet test labs/CycleEnginePrototype/Highbyte.DotNet6502.CycleEnginePrototype.Tests
dotnet run -c Release --project labs/CycleEnginePrototype/Highbyte.DotNet6502.CycleEnginePrototype.Benchmarks
```

The tests check both device policies against hand-derived bus traces and cycle counts, against the
production executor's final state, against the stall rules (reads stall while BA is low, writes do
not), and against each other over a long program with bad lines and timer interrupts active,
including identical device state. The benchmarks run the same slice loop under three device modes
(absent, ticking, ticking with bad lines) and report each policy relative to the legacy
executor, with allocations.

## Results (2026-09-02, Apple M1, .NET 10.0.7, Release, DefaultJob)

1,400 instructions of the slice loop per invocation. Ratio is against the legacy executor in the
same device mode. All rows allocate nothing. The two resumable rows were measured before those
engines were removed and are kept as the evidence for the choice.

| Engine | No devices | Devices ticking | Ticking with bad lines |
| --- | ---: | ---: | ---: |
| Legacy (reference) | 19.5 µs · 1.00 | 19.9 µs · 1.00 | 19.9 µs · 1.00 |
| Atomic, cycle-stamped, per-cycle device sync | 20.4 µs · 1.05 | 28.6 µs · 1.44 | 29.8 µs · 1.50 |
| Atomic, cycle-stamped, lazy device sync | 20.2 µs · 1.03 | 24.2 µs · 1.22 | 25.2 µs · 1.27 |
| Micro-operation table (resumable) | 24.3 µs · 1.24 | 32.3 µs · 1.63 | 32.8 µs · 1.65 |
| Flattened state machine (resumable) | 22.4 µs · 1.15 | 30.8 µs · 1.55 | 32.3 µs · 1.63 |

Reading the table:

- The candidates do strictly more work than the legacy executor: every cycle is a real bus access
  (the legacy executor skips the implied-mode, branch and stack dummy reads), and in the bad-line
  mode they also execute the stalled read cycles the legacy executor does not model (about 5% more
  cycles). The ratios therefore overstate their cost per cycle.
- With no devices, keeping the instruction atomic costs 3–5%, and making it resumable costs
  15–24% on top of that. Per-cycle dispatch is the price of resumability, not of cycle accuracy.
- With devices, the cost is dominated by how often the scheduler runs, not by the CPU: ticking the
  devices on every cycle costs 40–65% regardless of engine, while advancing them in closed form at
  instruction boundaries and only when a read can stall (lazy sync) keeps the whole thing at
  22–27% over legacy with identical stalls, interrupts and device state (the equivalence tests
  assert that).
- In absolute terms even the slowest row is about 23 ns per instruction against 14 ns, which on a
  C64 frame of ~6,500 instructions is under 60 µs of a 20 ms budget. Performance does not decide
  between the designs; what does is how much of the CPU must be rewritten and what each design
  makes possible.
- The two resumable forms are within 8% of each other; the flattened switch is the faster one.
  Measured with per-cycle device sync only; with lazy sync they would sit near their "no devices"
  column, so the real gap to the chosen design is roughly 12% of CPU time.

## Decision

The cycle-stamped atomic design was chosen, with lazy device synchronization as the production
policy and per-cycle synchronization kept as the test oracle. Cycle accuracy turned out to be
nearly free; what a resumable engine buys (cycle-granular debugger stepping, mid-instruction save
states, trivial lockstep with a second CPU) is not needed by the current goals, and its cost is a
rewrite of every opcode. The migration path is: make every cycle a real bus access in the
production handlers, thread the cycle number through the bus, and give each device a closed-form
`Advance(n)` that the per-cycle oracle validates.
