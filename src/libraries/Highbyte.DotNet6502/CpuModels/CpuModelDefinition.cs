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
    /// Builds the per-CPU instruction table for a supported profile. The table is still
    /// the mutable per-instance <see cref="InstructionList"/> graph that today's executor
    /// consumes unchanged; a frozen descriptor/handler table replaces it in a later step
    /// of the CPU-model work.
    /// </summary>
    public required Func<CpuCompatibilityProfile, InstructionList> CreateInstructionList { get; init; }
}
