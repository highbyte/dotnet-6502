namespace Highbyte.DotNet6502;

/// <summary>
/// Builds a CPU model's 256-entry descriptor dispatch table from an instruction table.
///
/// Everything that was previously decided per executed instruction — the addressing-mode
/// switch and the IInstructionUses* interface probes — is decided ONCE here, at table
/// build time, and baked into each descriptor's <see cref="OpCodeDescriptor.Execute"/>
/// handler. The hot path becomes one array index + one delegate call. Where a model's
/// behavior diverges from this generic composition (e.g. how a JMP (addr) pointer is
/// dereferenced), the model binds its own handler for that byte instead — divergence
/// lives in handler binding, never in a per-instruction model branch.
///
/// Handlers intentionally reuse the existing Instruction objects: composition changes
/// WHERE dispatch decisions are made, not WHAT the instructions do. Only handlers that
/// genuinely diverge between models get model-specific implementations (in later steps).
/// </summary>
internal static class OpCodeDescriptorTableBuilder
{
    /// <param name="instructionList">Source instruction table (metadata + instruction objects).</param>
    /// <param name="handlerOverrides">
    /// Optional per-byte execute handlers replacing the generic composition where the
    /// model's behavior genuinely diverges (e.g. the NMOS JMP ($xxFF) page-wrap bug).
    /// Applied last; metadata stays from the instruction table. Overriding a byte the
    /// instruction table leaves undefined is a construction error.
    /// </param>
    public static OpCodeDescriptor?[] Build(InstructionList instructionList, IReadOnlyDictionary<byte, ExecuteHandler>? handlerOverrides = null)
    {
        var table = new OpCodeDescriptor?[256];
        for (var code = 0; code <= 0xff; code++)
        {
            var b = (byte)code;
            var opCode = instructionList.TryGetOpCode(b);
            if (opCode is null)
                continue;
            var instruction = instructionList.GetInstruction(opCode);
            table[b] = new OpCodeDescriptor
            {
                Code = b,
                Mnemonic = instruction.Name,
                Addressing = opCode.AddressingMode,
                Size = (byte)opCode.Size,
                BaseCycles = opCode.MinimumCycles,
                Documented = InstructionList.GetMinimumCompatibilityProfile(opCode.Code) == CpuCompatibilityProfile.OfficialOnly,
                Execute = ComposeExecuteHandler(opCode, instruction),
            };
        }

        // Model-divergent behavior: replace the generic composition where the model binds
        // its own handler. Metadata stays from the instruction table; only Execute changes.
        if (handlerOverrides is not null)
        {
            foreach (var (code, handler) in handlerOverrides)
            {
                var descriptor = table[code]
                    ?? throw new DotNet6502Exception($"CPU model table construction error: handler override for opcode {code:x2}, which is undefined in the instruction table.");
                table[code] = new OpCodeDescriptor
                {
                    Code = descriptor.Code,
                    Mnemonic = descriptor.Mnemonic,
                    Addressing = descriptor.Addressing,
                    Size = descriptor.Size,
                    BaseCycles = descriptor.BaseCycles,
                    Documented = descriptor.Documented,
                    Execute = handler,
                };
            }
        }

        return table;
    }

    /// <summary>
    /// Binds (addressing mode × instruction interface) to a single handler. The interface
    /// probe order mirrors the previous runtime executor exactly: IInstructionUsesByte,
    /// then IInstructionUsesAddress (only when the mode produces an address), then
    /// IInstructionUsesStack, then IInstructionUsesOnlyRegOrStatus. An unmappable
    /// combination is a table-construction error, caught here instead of at runtime.
    /// Internal so model table builders can compose additional (instruction, mode)
    /// pairs that the NMOS opcode tables don't contain (e.g. 65C02 "(zp)" modes).
    /// </summary>
    internal static ExecuteHandler ComposeExecuteHandler(OpCode opCode, Instruction instruction)
    {
        var oc = opCode;
        var baseCycles = opCode.MinimumCycles;

        switch (opCode.AddressingMode)
        {
            // Modes producing a byte value directly (immediate operand / branch offset).
            case AddrMode.I:
            case AddrMode.Relative:
                switch (instruction)
                {
                    case IInstructionUsesByte usesByte:
                        return (cpu, mem) =>
                        {
                            var value = cpu.FetchOperand(mem);
                            var calc = new AddrModeCalcResult { OpCode = oc, InsValue = value };
                            return baseCycles + usesByte.ExecuteWithByte(cpu, mem, value, calc);
                        };
                    case IInstructionUsesStack usesStack:
                        return (cpu, mem) =>
                        {
                            var calc = new AddrModeCalcResult { OpCode = oc, InsValue = cpu.FetchOperand(mem) };
                            return baseCycles + usesStack.ExecuteWithStack(cpu, mem, calc);
                        };
                    case IInstructionUsesOnlyRegOrStatus usesRegOrStatus:
                        return (cpu, mem) =>
                        {
                            var calc = new AddrModeCalcResult { OpCode = oc, InsValue = cpu.FetchOperand(mem) };
                            return baseCycles + usesRegOrStatus.Execute(cpu, calc);
                        };
                    default:
                        throw BuildError(opCode, instruction);
                }

            // Modes producing no operand at all.
            case AddrMode.Implied:
            case AddrMode.Accumulator:
                switch (instruction)
                {
                    case IInstructionUsesStack usesStack:
                        return (cpu, mem) => baseCycles + usesStack.ExecuteWithStack(cpu, mem, new AddrModeCalcResult { OpCode = oc });
                    case IInstructionUsesOnlyRegOrStatus usesRegOrStatus:
                        return (cpu, mem) => baseCycles + usesRegOrStatus.Execute(cpu, new AddrModeCalcResult { OpCode = oc });
                    default:
                        throw BuildError(opCode, instruction);
                }

            case AddrMode.ZP:
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) => ((ushort)cpu.FetchOperand(mem), false));

            case AddrMode.ZP_X:
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) => (cpu.CalcZeroPageAddressX(cpu.FetchOperand(mem), wrapZeroPage: true), false));

            case AddrMode.ZP_Y:
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) => (cpu.CalcZeroPageAddressY(cpu.FetchOperand(mem), wrapZeroPage: true), false));

            case AddrMode.ABS:
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) => (cpu.FetchOperandWord(mem), false));

            case AddrMode.ABS_X:
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) =>
                    {
                        var address = cpu.CalcFullAddressX(cpu.FetchOperandWord(mem), out var didCrossPageBoundary);
                        return (address, didCrossPageBoundary);
                    });

            case AddrMode.ABS_Y:
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) =>
                    {
                        var address = cpu.CalcFullAddressY(cpu.FetchOperandWord(mem), out var didCrossPageBoundary);
                        return (address, didCrossPageBoundary);
                    });

            case AddrMode.IX_IND:
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) => (cpu.FetchWord(mem, cpu.CalcZeroPageAddressX(cpu.FetchOperand(mem))), false));

            case AddrMode.ZP_IND:
                // 65C02 "(zp)": pointer in zero page, no index, no page-cross extra.
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) => (cpu.FetchWord(mem, cpu.FetchOperand(mem)), false));

            case AddrMode.IND_IX:
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) =>
                    {
                        var address = cpu.CalcFullAddressY(cpu.FetchWord(mem, cpu.FetchOperand(mem)), out var didCrossPageBoundary);
                        return (address, didCrossPageBoundary);
                    });

            case AddrMode.Indirect:
                // Default composition: linear pointer read. A model whose indirect JMP
                // dereference differs (the NMOS $xxFF page-wrap bug; the 65C02's 6-cycle
                // linear read) binds its own handler for that byte instead of using this
                // generic composition.
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) => (cpu.FetchWord(mem, cpu.FetchOperandWord(mem)), false));

            default:
                throw BuildError(opCode, instruction);
        }
    }

    /// <summary>
    /// Resolves the effective address for an address-producing mode; the bool reports a
    /// page-boundary crossing (feeds extra-cycle calculation in the instruction logic).
    /// </summary>
    private delegate (ushort Address, bool CrossedPageBoundary) ResolveAddress(CPU cpu, Memory mem);

    private static ExecuteHandler ComposeWithAddress(OpCode oc, ulong baseCycles, Instruction instruction, ResolveAddress resolveAddress)
    {
        switch (instruction)
        {
            case IInstructionUsesByte usesByte:
                return (cpu, mem) =>
                {
                    var (address, crossedPageBoundary) = resolveAddress(cpu, mem);
                    var calc = new AddrModeCalcResult { OpCode = oc, InsAddress = address, AddressCalculationCrossedPageBoundary = crossedPageBoundary };
                    return baseCycles + usesByte.ExecuteWithByte(cpu, mem, cpu.FetchByte(mem, address), calc);
                };
            case IInstructionUsesAddress usesAddress:
                return (cpu, mem) =>
                {
                    var (address, crossedPageBoundary) = resolveAddress(cpu, mem);
                    var calc = new AddrModeCalcResult { OpCode = oc, InsAddress = address, AddressCalculationCrossedPageBoundary = crossedPageBoundary };
                    return baseCycles + usesAddress.ExecuteWithWord(cpu, mem, address, calc);
                };
            case IInstructionUsesStack usesStack:
                return (cpu, mem) =>
                {
                    var (address, crossedPageBoundary) = resolveAddress(cpu, mem);
                    var calc = new AddrModeCalcResult { OpCode = oc, InsAddress = address, AddressCalculationCrossedPageBoundary = crossedPageBoundary };
                    return baseCycles + usesStack.ExecuteWithStack(cpu, mem, calc);
                };
            case IInstructionUsesOnlyRegOrStatus usesRegOrStatus:
                return (cpu, mem) =>
                {
                    var (address, crossedPageBoundary) = resolveAddress(cpu, mem);
                    var calc = new AddrModeCalcResult { OpCode = oc, InsAddress = address, AddressCalculationCrossedPageBoundary = crossedPageBoundary };
                    return baseCycles + usesRegOrStatus.Execute(cpu, calc);
                };
            default:
                throw BuildError(oc, instruction);
        }
    }

    private static DotNet6502Exception BuildError(OpCode opCode, Instruction instruction)
        => new($"CPU model table construction error: no way to execute instruction {instruction.Name} (opcode {opCode.CodeRaw:x2}, addressing mode {opCode.AddressingMode}).");
}
