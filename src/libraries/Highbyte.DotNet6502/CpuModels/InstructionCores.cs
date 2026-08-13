namespace Highbyte.DotNet6502;

/// <summary>
/// One read-style operation's compute step: consumes the fetched value, updates
/// registers/flags. Returns extra cycles beyond the addressing-determined ones
/// (0 for almost everything; e.g. 65C02 decimal ADC/SBC return 1).
/// </summary>
internal delegate ulong ReadOperation(CPU cpu, byte value);

/// <summary>One store-style operation's compute step: produces the value to write.</summary>
internal delegate byte StoreOperation(CPU cpu);

/// <summary>One implied/accumulator operation: registers and flags only.</summary>
internal delegate void ImpliedOperation(CPU cpu);

/// <summary>
/// Operation cores for instructions whose semantics are identical on every CPU model:
/// tiny static methods composed with per-model addressing at table build time
/// (see OpCodeDescriptorTableBuilder). Named after the instructions they implement —
/// methods carry the instruction identity, not classes.
/// </summary>
internal static class InstructionCores
{
    // --- Loads ---

    public static ulong Lda(CPU cpu, byte value)
    {
        cpu.A = value;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(value, ref cpu.ProcessorStatus);
        return 0;
    }

    public static ulong Ldx(CPU cpu, byte value)
    {
        cpu.X = value;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(value, ref cpu.ProcessorStatus);
        return 0;
    }

    public static ulong Ldy(CPU cpu, byte value)
    {
        cpu.Y = value;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(value, ref cpu.ProcessorStatus);
        return 0;
    }

    // --- Stores ---

    public static byte Sta(CPU cpu) => cpu.A;
    public static byte Stx(CPU cpu) => cpu.X;
    public static byte Sty(CPU cpu) => cpu.Y;

    // --- Register transfers ---

    public static void Tax(CPU cpu)
    {
        cpu.X = cpu.A;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.X, ref cpu.ProcessorStatus);
    }

    public static void Tay(CPU cpu)
    {
        cpu.Y = cpu.A;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.Y, ref cpu.ProcessorStatus);
    }

    public static void Tsx(CPU cpu)
    {
        cpu.X = cpu.SP;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.X, ref cpu.ProcessorStatus);
    }

    public static void Txa(CPU cpu)
    {
        cpu.A = cpu.X;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, ref cpu.ProcessorStatus);
    }

    public static void Txs(CPU cpu)
    {
        // TXS sets no flags — the only transfer that doesn't.
        cpu.SP = cpu.X;
    }

    public static void Tya(CPU cpu)
    {
        cpu.A = cpu.Y;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, ref cpu.ProcessorStatus);
    }

    // --- Logic ---

    public static ulong And(CPU cpu, byte value)
    {
        cpu.A &= value;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, ref cpu.ProcessorStatus);
        return 0;
    }

    public static ulong Ora(CPU cpu, byte value)
    {
        cpu.A |= value;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, ref cpu.ProcessorStatus);
        return 0;
    }

    public static ulong Eor(CPU cpu, byte value)
    {
        cpu.A ^= value;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, ref cpu.ProcessorStatus);
        return 0;
    }

    public static ulong Bit(CPU cpu, byte value)
    {
        BinaryArithmeticHelpers.PerformBITAndSetStatusRegisters(cpu.A, value, ref cpu.ProcessorStatus);
        return 0;
    }

    // --- Compares ---

    public static ulong Cmp(CPU cpu, byte value)
    {
        BinaryArithmeticHelpers.SetFlagsAfterCompare(cpu.A, value, ref cpu.ProcessorStatus);
        return 0;
    }

    public static ulong Cpx(CPU cpu, byte value)
    {
        BinaryArithmeticHelpers.SetFlagsAfterCompare(cpu.X, value, ref cpu.ProcessorStatus);
        return 0;
    }

    public static ulong Cpy(CPU cpu, byte value)
    {
        BinaryArithmeticHelpers.SetFlagsAfterCompare(cpu.Y, value, ref cpu.ProcessorStatus);
        return 0;
    }

    // --- Add/subtract with carry: the decimal-mode behavior is per model ---

    public static ulong AdcNmos(CPU cpu, byte value)
    {
        cpu.A = cpu.ProcessorStatus.Decimal
            ? DecimalArithmeticHelpers.AddWithCarryAndOverFlowDecimalMode(cpu.A, value, ref cpu.ProcessorStatus)
            : BinaryArithmeticHelpers.AddWithCarryAndOverflow(cpu.A, value, ref cpu.ProcessorStatus);
        return 0;
    }

    public static ulong SbcNmos(CPU cpu, byte value)
    {
        cpu.A = cpu.ProcessorStatus.Decimal
            ? DecimalArithmeticHelpers.SubtractWithCarryAndOverflowDecimalMode(cpu.A, value, ref cpu.ProcessorStatus)
            : BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(cpu.A, value, ref cpu.ProcessorStatus);
        return 0;
    }

    public static ulong AdcCmos(CPU cpu, byte value)
    {
        if (cpu.ProcessorStatus.Decimal)
        {
            cpu.A = DecimalArithmeticHelpers.AddWithCarryAndOverFlowDecimalModeCmos(cpu.A, value, ref cpu.ProcessorStatus);
            return 1; // 65C02: decimal mode costs one extra cycle
        }
        cpu.A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(cpu.A, value, ref cpu.ProcessorStatus);
        return 0;
    }

    public static ulong SbcCmos(CPU cpu, byte value)
    {
        if (cpu.ProcessorStatus.Decimal)
        {
            cpu.A = DecimalArithmeticHelpers.SubtractWithCarryAndOverflowDecimalModeCmos(cpu.A, value, ref cpu.ProcessorStatus);
            return 1; // 65C02: decimal mode costs one extra cycle
        }
        cpu.A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(cpu.A, value, ref cpu.ProcessorStatus);
        return 0;
    }

    // --- Register increment/decrement ---

    public static void Inx(CPU cpu)
    {
        cpu.X++;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.X, ref cpu.ProcessorStatus);
    }

    public static void Iny(CPU cpu)
    {
        cpu.Y++;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.Y, ref cpu.ProcessorStatus);
    }

    public static void Dex(CPU cpu)
    {
        cpu.X--;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.X, ref cpu.ProcessorStatus);
    }

    public static void Dey(CPU cpu)
    {
        cpu.Y--;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.Y, ref cpu.ProcessorStatus);
    }
}
