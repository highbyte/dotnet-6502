using Highbyte.DotNet6502.Utils;

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
    /// <param name="indexedDummyReads">
    /// True for NMOS models (<see cref="CpuModelTraits.PerformsIndexedDummyReads"/>):
    /// abs,X / abs,Y / (zp),Y compose with the NMOS dummy read at the un-carried address
    /// — on page cross for plain reads; always for stores and read-modify-write.
    /// </param>
    public static OpCodeDescriptor?[] Build(InstructionList instructionList, IReadOnlyDictionary<byte, ExecuteHandler>? handlerOverrides = null, bool indexedDummyReads = false)
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
                Execute = ComposeExecuteHandler(opCode, instruction, indexedDummyReads),
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
    internal static ExecuteHandler ComposeExecuteHandler(OpCode opCode, Instruction instruction, bool indexedDummyReads = false)
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
                    static (cpu, mem) => { var address = (ushort)cpu.FetchOperand(mem); return (address, false, address); });

            case AddrMode.ZP_X:
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) => { var address = cpu.CalcZeroPageAddressX(cpu.FetchOperand(mem), wrapZeroPage: true); return (address, false, address); });

            case AddrMode.ZP_Y:
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) => { var address = cpu.CalcZeroPageAddressY(cpu.FetchOperand(mem), wrapZeroPage: true); return (address, false, address); });

            case AddrMode.ABS:
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) => { var address = cpu.FetchOperandWord(mem); return (address, false, address); });

            case AddrMode.ABS_X:
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) =>
                    {
                        var baseAddress = cpu.FetchOperandWord(mem);
                        var address = cpu.CalcFullAddressX(baseAddress, out var didCrossPageBoundary);
                        return (address, didCrossPageBoundary, UncarriedAddress(baseAddress, cpu.X));
                    },
                    DummyReadsFor(instruction, indexedDummyReads));

            case AddrMode.ABS_Y:
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) =>
                    {
                        var baseAddress = cpu.FetchOperandWord(mem);
                        var address = cpu.CalcFullAddressY(baseAddress, out var didCrossPageBoundary);
                        return (address, didCrossPageBoundary, UncarriedAddress(baseAddress, cpu.Y));
                    },
                    DummyReadsFor(instruction, indexedDummyReads));

            case AddrMode.IX_IND:
                // "(zp,X)": the pointer lives in zero page and wraps within it ($FF -> $00).
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) => { var address = ReadZeroPagePointer(cpu, mem, cpu.CalcZeroPageAddressX(cpu.FetchOperand(mem))); return (address, false, address); });

            case AddrMode.ZP_IND:
                // 65C02 "(zp)": pointer in zero page (wrapping), no index, no page-cross extra.
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) => { var address = ReadZeroPagePointer(cpu, mem, cpu.FetchOperand(mem)); return (address, false, address); });

            case AddrMode.IND_IX:
                // "(zp),Y": zero-page pointer (wrapping), then Y-indexed.
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) =>
                    {
                        var baseAddress = ReadZeroPagePointer(cpu, mem, cpu.FetchOperand(mem));
                        var address = cpu.CalcFullAddressY(baseAddress, out var didCrossPageBoundary);
                        return (address, didCrossPageBoundary, UncarriedAddress(baseAddress, cpu.Y));
                    },
                    DummyReadsFor(instruction, indexedDummyReads));

            case AddrMode.Indirect:
                // Default composition: linear pointer read. A model whose indirect JMP
                // dereference differs (the NMOS $xxFF page-wrap bug; the 65C02's 6-cycle
                // linear read) binds its own handler for that byte instead of using this
                // generic composition.
                return ComposeWithAddress(oc, baseCycles, instruction,
                    static (cpu, mem) => { var address = cpu.FetchWord(mem, cpu.FetchOperandWord(mem)); return (address, false, address); });

            default:
                throw BuildError(opCode, instruction);
        }
    }

    /// <summary>
    /// The address the NMOS 6502 touches on the cycle before the index-carry into the high
    /// byte is resolved: high byte from the base address, low byte already indexed.
    /// </summary>
    private static ushort UncarriedAddress(ushort baseAddress, byte index)
        => (ushort)((baseAddress & 0xFF00) | ((baseAddress + index) & 0x00FF));

    /// <summary>
    /// Reads a 16-bit pointer from zero page, wrapping within the page: a pointer at $FF
    /// takes its high byte from $00, on NMOS and CMOS alike.
    /// </summary>
    private static ushort ReadZeroPagePointer(CPU cpu, Memory mem, ushort zeroPageAddress)
    {
        var lowByte = cpu.FetchByte(mem, zeroPageAddress);
        var highByte = cpu.FetchByte(mem, (ushort)((zeroPageAddress + 1) & 0x00FF));
        return ByteHelpers.ToLittleEndianWord(lowByte, highByte);
    }

    /// <summary>
    /// NMOS dummy-read policy for an indexed-mode instruction: plain reads touch the
    /// un-carried address only when the page crosses; stores (IInstructionUsesAddress)
    /// and read-modify-write instructions ALWAYS touch it first — the reason indexed
    /// stores and RMW have no "+1 on page cross" and why e.g. STA $DC0D,X reads $DC0D
    /// before writing it.
    /// </summary>
    private static DummyReadBehavior DummyReadsFor(Instruction instruction, bool indexedDummyReads)
    {
        if (!indexedDummyReads)
            return DummyReadBehavior.None;
        return instruction is IInstructionUsesAddress or IReadModifyWriteInstruction
            ? DummyReadBehavior.Always
            : DummyReadBehavior.OnPageCross;
    }

    private enum DummyReadBehavior { None, OnPageCross, Always }

    /// <summary>
    /// Resolves the effective address for an address-producing mode. CrossedPageBoundary
    /// feeds extra-cycle calculation; UncarriedAddress is the dummy-read target for
    /// indexed modes (equals Address for non-indexed modes and non-crossing accesses).
    /// </summary>
    private delegate (ushort Address, bool CrossedPageBoundary, ushort UncarriedAddress) ResolveAddress(CPU cpu, Memory mem);

    private static ExecuteHandler ComposeWithAddress(OpCode oc, ulong baseCycles, Instruction instruction, ResolveAddress resolveAddress,
        DummyReadBehavior dummyReads = DummyReadBehavior.None)
    {
        switch (instruction)
        {
            case IInstructionUsesByte usesByte:
                return dummyReads switch
                {
                    DummyReadBehavior.Always => (cpu, mem) =>
                    {
                        var (address, crossedPageBoundary, uncarriedAddress) = resolveAddress(cpu, mem);
                        cpu.FetchByte(mem, uncarriedAddress);
                        var calc = new AddrModeCalcResult { OpCode = oc, InsAddress = address, AddressCalculationCrossedPageBoundary = crossedPageBoundary };
                        return baseCycles + usesByte.ExecuteWithByte(cpu, mem, cpu.FetchByte(mem, address), calc);
                    },
                    DummyReadBehavior.OnPageCross => (cpu, mem) =>
                    {
                        var (address, crossedPageBoundary, uncarriedAddress) = resolveAddress(cpu, mem);
                        if (crossedPageBoundary)
                            cpu.FetchByte(mem, uncarriedAddress);
                        var calc = new AddrModeCalcResult { OpCode = oc, InsAddress = address, AddressCalculationCrossedPageBoundary = crossedPageBoundary };
                        return baseCycles + usesByte.ExecuteWithByte(cpu, mem, cpu.FetchByte(mem, address), calc);
                    },
                    _ => (cpu, mem) =>
                    {
                        var (address, crossedPageBoundary, _) = resolveAddress(cpu, mem);
                        var calc = new AddrModeCalcResult { OpCode = oc, InsAddress = address, AddressCalculationCrossedPageBoundary = crossedPageBoundary };
                        return baseCycles + usesByte.ExecuteWithByte(cpu, mem, cpu.FetchByte(mem, address), calc);
                    },
                };
            case IInstructionUsesAddress usesAddress:
                return dummyReads switch
                {
                    // Stores and shift/rotate RMW: the dummy read always happens (its
                    // address equals the effective address when no page is crossed).
                    DummyReadBehavior.Always => (cpu, mem) =>
                    {
                        var (address, crossedPageBoundary, uncarriedAddress) = resolveAddress(cpu, mem);
                        cpu.FetchByte(mem, uncarriedAddress);
                        var calc = new AddrModeCalcResult { OpCode = oc, InsAddress = address, AddressCalculationCrossedPageBoundary = crossedPageBoundary };
                        return baseCycles + usesAddress.ExecuteWithWord(cpu, mem, address, calc);
                    },
                    _ => (cpu, mem) =>
                    {
                        var (address, crossedPageBoundary, _) = resolveAddress(cpu, mem);
                        var calc = new AddrModeCalcResult { OpCode = oc, InsAddress = address, AddressCalculationCrossedPageBoundary = crossedPageBoundary };
                        return baseCycles + usesAddress.ExecuteWithWord(cpu, mem, address, calc);
                    },
                };
            case IInstructionUsesStack usesStack:
                return (cpu, mem) =>
                {
                    var (address, crossedPageBoundary, _) = resolveAddress(cpu, mem);
                    var calc = new AddrModeCalcResult { OpCode = oc, InsAddress = address, AddressCalculationCrossedPageBoundary = crossedPageBoundary };
                    return baseCycles + usesStack.ExecuteWithStack(cpu, mem, calc);
                };
            case IInstructionUsesOnlyRegOrStatus usesRegOrStatus:
                return (cpu, mem) =>
                {
                    var (address, crossedPageBoundary, _) = resolveAddress(cpu, mem);
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
