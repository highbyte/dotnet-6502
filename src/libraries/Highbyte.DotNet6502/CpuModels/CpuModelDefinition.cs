namespace Highbyte.DotNet6502;

/// <summary>
/// Immutable definition of a CPU model (NMOS 6502, and later 6510 and 65C02): its stable
/// identity, which compatibility profiles it supports, and how its per-CPU instruction
/// table is built. One definition instance exists per model; all mutable per-CPU state
/// (registers, execution state, instruction table instances) lives on <see cref="CPU"/>.
///
/// The definition is selected once at CPU construction and never consulted per
/// instruction, keeping the hot path free of model branches.
/// </summary>
internal sealed class CpuModelDefinition
{
    public required string ModelId { get; init; }
    public required string DisplayName { get; init; }

    /// <summary>
    /// The compatibility profiles this model supports. Profiles only make sense for NMOS
    /// models (they control undocumented-opcode exposure); a CMOS model supports exactly
    /// one profile. CPU construction rejects unsupported combinations.
    /// </summary>
    public required IReadOnlyList<CpuCompatibilityProfile> SupportedProfiles { get; init; }

    /// <summary>
    /// Non-instruction behavior differences (interrupt/BRK/reset entry effects),
    /// consulted per event — never per instruction.
    /// </summary>
    public required CpuModelTraits Traits { get; init; }

    /// <summary>
    /// Builds this model's 256-entry descriptor dispatch table for the CPU's
    /// compatibility profile (which gates undocumented-opcode bindings; ignored by
    /// models with a single profile). This is THE mechanism for model-specific
    /// instruction behavior — divergence lives in handler binding at build time, never
    /// in definition flags or per-instruction model branches. Models compose the table
    /// from shared bindings (<see cref="InstructionBindings"/>) plus their own
    /// bespoke per-byte descriptors.
    /// </summary>
    public required Func<CpuCompatibilityProfile, OpCodeDescriptor?[]> CreateDescriptors { get; init; }

    /// <summary>
    /// Factory for per-CPU-instance model state (e.g. the 6510's I/O port), invoked
    /// once per CPU construction. Null for models without extra state. Definitions
    /// stay immutable and shareable precisely because mutable state lives on the CPU.
    /// </summary>
    public Func<CpuModelState>? StateFactory { get; init; }
}
