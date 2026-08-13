using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502;

/// <summary>
/// Composition building blocks for a CPU model's 256-entry descriptor dispatch table:
/// per-mode effective-address resolution and the Compose* methods that bind an operation
/// core (<see cref="InstructionCores"/>) to an addressing mode as a single pre-composed
/// <see cref="OpCodeDescriptor.Execute"/> handler.
///
/// Everything decided per instruction shape — addressing mode, bus sequence, dummy-read
/// policy, page-cross cycles — is decided ONCE here, at table build time. The hot path
/// becomes one array index + one delegate call. Where a model's behavior diverges from
/// this generic composition (e.g. how a JMP (addr) pointer is dereferenced), the model
/// binds its own bespoke handler for that byte instead — divergence lives in handler
/// binding, never in a per-instruction model branch.
/// </summary>
internal static class OpCodeDescriptorTableBuilder
{
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
    /// Resolves the effective address for an address-producing mode. CrossedPageBoundary
    /// feeds extra-cycle calculation; UncarriedAddress is the dummy-read target for
    /// indexed modes (equals Address for non-indexed modes and non-crossing accesses).
    /// </summary>
    internal delegate (ushort Address, bool CrossedPageBoundary, ushort UncarriedAddress) ResolveAddress(CPU cpu, Memory mem);

    /// <summary>Effective-address resolution for an address-producing mode (see ResolveAddress).</summary>
    internal static ResolveAddress GetAddressResolver(AddrMode addressingMode)
        => addressingMode switch
        {
            AddrMode.ZP => static (cpu, mem) => { var address = (ushort)cpu.FetchOperand(mem); return (address, false, address); },
            AddrMode.ZP_X => static (cpu, mem) => { var address = cpu.CalcZeroPageAddressX(cpu.FetchOperand(mem), wrapZeroPage: true); return (address, false, address); },
            AddrMode.ZP_Y => static (cpu, mem) => { var address = cpu.CalcZeroPageAddressY(cpu.FetchOperand(mem), wrapZeroPage: true); return (address, false, address); },
            AddrMode.ABS => static (cpu, mem) => { var address = cpu.FetchOperandWord(mem); return (address, false, address); },
            AddrMode.ABS_X => static (cpu, mem) =>
            {
                var baseAddress = cpu.FetchOperandWord(mem);
                var address = cpu.CalcFullAddressX(baseAddress, out var didCrossPageBoundary);
                return (address, didCrossPageBoundary, UncarriedAddress(baseAddress, cpu.X));
            },
            AddrMode.ABS_Y => static (cpu, mem) =>
            {
                var baseAddress = cpu.FetchOperandWord(mem);
                var address = cpu.CalcFullAddressY(baseAddress, out var didCrossPageBoundary);
                return (address, didCrossPageBoundary, UncarriedAddress(baseAddress, cpu.Y));
            },
            AddrMode.IX_IND => static (cpu, mem) => { var address = ReadZeroPagePointer(cpu, mem, cpu.CalcZeroPageAddressX(cpu.FetchOperand(mem))); return (address, false, address); },
            AddrMode.ZP_IND => static (cpu, mem) => { var address = ReadZeroPagePointer(cpu, mem, cpu.FetchOperand(mem)); return (address, false, address); },
            AddrMode.IND_IX => static (cpu, mem) =>
            {
                var baseAddress = ReadZeroPagePointer(cpu, mem, cpu.FetchOperand(mem));
                var address = cpu.CalcFullAddressY(baseAddress, out var didCrossPageBoundary);
                return (address, didCrossPageBoundary, UncarriedAddress(baseAddress, cpu.Y));
            },
            AddrMode.Indirect => static (cpu, mem) => { var address = cpu.FetchWord(mem, cpu.FetchOperandWord(mem)); return (address, false, address); },
            _ => throw new DotNet6502Exception($"CPU model table construction error: addressing mode {addressingMode} does not produce an address."),
        };

    private static bool IsIndexedMode(AddrMode addressingMode)
        => addressingMode is AddrMode.ABS_X or AddrMode.ABS_Y or AddrMode.IND_IX;

    /// <summary>
    /// Composes a read-style instruction (value in → registers/flags): operand fetch per
    /// mode, the NMOS page-cross dummy read when enabled, then the core. Total cycles =
    /// base + optional page-cross cycle + whatever the core reports (e.g. CMOS decimal +1).
    /// </summary>
    internal static ExecuteHandler ComposeRead(AddrMode addressingMode, ulong baseCycles, ReadOperation core,
        bool addPageCrossCycle, bool indexedDummyReads)
    {
        if (addressingMode == AddrMode.I)
            return (cpu, mem) => baseCycles + core(cpu, cpu.FetchOperand(mem));

        var resolveAddress = GetAddressResolver(addressingMode);
        if (indexedDummyReads && IsIndexedMode(addressingMode))
        {
            return (cpu, mem) =>
            {
                var (address, crossedPageBoundary, uncarriedAddress) = resolveAddress(cpu, mem);
                if (crossedPageBoundary)
                    cpu.FetchByte(mem, uncarriedAddress);
                var extra = core(cpu, cpu.FetchByte(mem, address));
                return baseCycles + (addPageCrossCycle && crossedPageBoundary ? 1ul : 0ul) + extra;
            };
        }
        return (cpu, mem) =>
        {
            var (address, crossedPageBoundary, _) = resolveAddress(cpu, mem);
            var extra = core(cpu, cpu.FetchByte(mem, address));
            return baseCycles + (addPageCrossCycle && crossedPageBoundary ? 1ul : 0ul) + extra;
        };
    }

    /// <summary>
    /// Composes a store-style instruction: address per mode, the NMOS always-dummy-read
    /// when enabled (why indexed stores have no conditional cycle), then the write.
    /// </summary>
    internal static ExecuteHandler ComposeStore(AddrMode addressingMode, ulong baseCycles, StoreOperation core,
        bool indexedDummyReads)
    {
        var resolveAddress = GetAddressResolver(addressingMode);
        if (indexedDummyReads && IsIndexedMode(addressingMode))
        {
            return (cpu, mem) =>
            {
                var (address, _, uncarriedAddress) = resolveAddress(cpu, mem);
                cpu.FetchByte(mem, uncarriedAddress);
                cpu.StoreByte(core(cpu), mem, address);
                return baseCycles;
            };
        }
        return (cpu, mem) =>
        {
            var (address, _, _) = resolveAddress(cpu, mem);
            cpu.StoreByte(core(cpu), mem, address);
            return baseCycles;
        };
    }

    /// <summary>Composes an implied/accumulator instruction: registers and flags only.</summary>
    internal static ExecuteHandler ComposeImplied(ulong baseCycles, ImpliedOperation core)
        => (cpu, mem) =>
        {
            core(cpu);
            return baseCycles;
        };

    /// <summary>
    /// Composes a branch instruction: 2 cycles not taken, 3 taken, 4 taken across a
    /// page boundary.
    /// </summary>
    internal static ExecuteHandler ComposeBranch(BranchCondition condition)
        => (cpu, mem) =>
        {
            var offset = cpu.FetchOperand(mem);
            if (!condition(cpu))
                return 2;
            cpu.PC = BranchHelper.CalculateNewAbsoluteBranchAddress(cpu.PC, (sbyte)offset, out _, out var crossedPageBoundary);
            return crossedPageBoundary ? 4ul : 3ul;
        };

    /// <summary>
    /// Composes a read-modify-write instruction with the model's bus sequence:
    /// 65C02 (<paramref name="cmosSequence"/>) reads twice then writes the result
    /// (the value from the final read feeds the modify); NMOS reads once, writes the
    /// unmodified value back, then writes the result — with the always-dummy-read at
    /// the un-carried address first on indexed modes when enabled.
    /// </summary>
    internal static ExecuteHandler ComposeRmw(AddrMode addressingMode, ulong baseCycles, RmwOperation core,
        bool cmosSequence, bool indexedDummyReads, bool addPageCrossCycle = false)
    {
        var resolveAddress = GetAddressResolver(addressingMode);
        if (cmosSequence)
        {
            return (cpu, mem) =>
            {
                var (address, crossedPageBoundary, _) = resolveAddress(cpu, mem);
                cpu.FetchByte(mem, address);
                var value = cpu.FetchByte(mem, address);
                cpu.StoreByte(core(cpu, value), mem, address);
                return baseCycles + (addPageCrossCycle && crossedPageBoundary ? 1ul : 0ul);
            };
        }
        if (indexedDummyReads && IsIndexedMode(addressingMode))
        {
            return (cpu, mem) =>
            {
                var (address, _, uncarriedAddress) = resolveAddress(cpu, mem);
                cpu.FetchByte(mem, uncarriedAddress);
                var value = cpu.FetchByte(mem, address);
                cpu.StoreByte(value, mem, address);
                cpu.StoreByte(core(cpu, value), mem, address);
                return baseCycles;
            };
        }
        return (cpu, mem) =>
        {
            var (address, _, _) = resolveAddress(cpu, mem);
            var value = cpu.FetchByte(mem, address);
            cpu.StoreByte(value, mem, address);
            cpu.StoreByte(core(cpu, value), mem, address);
            return baseCycles;
        };
    }
}
