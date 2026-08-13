namespace Highbyte.DotNet6502;

/// <summary>
/// Byte-indexed opcode metadata table keyed by <see cref="OpCodeId"/> — the public
/// <b>NMOS 6502 façade</b>. The enum names and the <see cref="CpuCompatibilityProfile"/>
/// filtering are NMOS concepts. On a CPU constructed with a non-NMOS model
/// (e.g. ncr65c02) this view contains only the officially documented instruction
/// subset shared with the NMOS 6502 — the model's full per-byte truth (redefined
/// bytes, new instructions, per-model cycles) lives in its internal descriptor
/// table (<see cref="OpCodeDescriptor"/>), which is also what execution runs on.
/// The metadata here is projected from the NMOS model's descriptor table, so the
/// two views cannot drift apart. Revisit this public type before any 1.0 release.
/// </summary>
public class InstructionList
{
    public Dictionary<byte, OpCode> OpCodeDictionary { get; private set; }

    // Byte-indexed lookup maintained in parallel with the dictionary above. Used on
    // metadata lookups (tooling, monitor, tests) -- avoids a Dictionary<byte, T> hash
    // per lookup in exchange for a fixed 2 KB of per-instance memory. The dictionary
    // is kept public for backward compat (tooling / monitor may enumerate it).
    private readonly OpCode?[] _opCodeArray = new OpCode?[256];

    public InstructionList(Dictionary<byte, OpCode> opCodeDictionary)
    {
        OpCodeDictionary = opCodeDictionary;
        foreach (var kvp in opCodeDictionary)
            _opCodeArray[kvp.Key] = kvp.Value;
    }

    public OpCode? TryGetOpCode(byte opCode) => _opCodeArray[opCode];

    public OpCode GetOpCode(byte opCode)
    {
        return _opCodeArray[opCode]!;
    }

    public InstructionList Clone()
    {
        return new InstructionList(this.OpCodeDictionary);
    }

    /// <summary>
    /// Builds the <b>NMOS 6502</b> instruction metadata table: the official instruction
    /// set plus the undocumented NMOS opcodes admitted by
    /// <paramref name="compatibilityProfile"/>. NMOS-specific by design — other CPU
    /// models compose their own descriptor tables and use this only for the shared
    /// official subset (with <see cref="CpuCompatibilityProfile.OfficialOnly"/>).
    /// Projected from the NMOS model's descriptor table — the single source of truth
    /// for which bytes each profile defines and their metadata.
    /// </summary>
    public static InstructionList GetAllInstructions(CpuCompatibilityProfile compatibilityProfile = CpuCompatibilityProfile.ExperimentalUnofficial)
    {
        var descriptors = Nmos6502Model.Definition.CreateDescriptors(compatibilityProfile);

        var opCodeDictionary = new Dictionary<byte, OpCode>();
        for (var code = 0; code <= 0xff; code++)
        {
            var descriptor = descriptors[code];
            if (descriptor is null)
                continue;
            opCodeDictionary.Add((byte)code, new OpCode
            {
                Code = (OpCodeId)code,
                AddressingMode = descriptor.Addressing,
                Size = descriptor.Size,
                MinimumCycles = descriptor.BaseCycles,
            });
        }

        return new InstructionList(opCodeDictionary);
    }
}
