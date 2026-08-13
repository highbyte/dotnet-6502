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
/// One read-modify-write operation's compute step: consumes the value read from memory,
/// returns the value to write back, updating registers/flags. The bus SEQUENCE around it
/// (NMOS read-write-write vs 65C02 read-read-write) is composed per model.
/// </summary>
internal delegate byte RmwOperation(CPU cpu, byte value);

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

    // --- Shifts/rotates: memory forms (RmwOperation) and accumulator forms (ImpliedOperation) ---

    public static byte Asl(CPU cpu, byte value) => BinaryArithmeticHelpers.PerformASLAndSetStatusRegisters(value, ref cpu.ProcessorStatus);
    public static byte Lsr(CPU cpu, byte value) => BinaryArithmeticHelpers.PerformLSRAndSetStatusRegisters(value, ref cpu.ProcessorStatus);
    public static byte Rol(CPU cpu, byte value) => BinaryArithmeticHelpers.PerformROLAndSetStatusRegisters(value, ref cpu.ProcessorStatus);
    public static byte Ror(CPU cpu, byte value) => BinaryArithmeticHelpers.PerformRORAndSetStatusRegisters(value, ref cpu.ProcessorStatus);

    public static void AslAccumulator(CPU cpu) => cpu.A = BinaryArithmeticHelpers.PerformASLAndSetStatusRegisters(cpu.A, ref cpu.ProcessorStatus);
    public static void LsrAccumulator(CPU cpu) => cpu.A = BinaryArithmeticHelpers.PerformLSRAndSetStatusRegisters(cpu.A, ref cpu.ProcessorStatus);
    public static void RolAccumulator(CPU cpu) => cpu.A = BinaryArithmeticHelpers.PerformROLAndSetStatusRegisters(cpu.A, ref cpu.ProcessorStatus);
    public static void RorAccumulator(CPU cpu) => cpu.A = BinaryArithmeticHelpers.PerformRORAndSetStatusRegisters(cpu.A, ref cpu.ProcessorStatus);

    // --- Memory increment/decrement (RmwOperation) ---

    public static byte Inc(CPU cpu, byte value)
    {
        value++;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(value, ref cpu.ProcessorStatus);
        return value;
    }

    public static byte Dec(CPU cpu, byte value)
    {
        value--;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(value, ref cpu.ProcessorStatus);
        return value;
    }

    // --- 65C02-only operations ---

    public static void IncAccumulator(CPU cpu)
    {
        cpu.A++;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, ref cpu.ProcessorStatus);
    }

    public static void DecAccumulator(CPU cpu)
    {
        cpu.A--;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, ref cpu.ProcessorStatus);
    }

    /// <summary>TSB: Z = (A AND M) == 0, then M |= A.</summary>
    public static byte Tsb(CPU cpu, byte value)
    {
        cpu.ProcessorStatus.Zero = (cpu.A & value) == 0;
        return (byte)(value | cpu.A);
    }

    /// <summary>TRB: Z = (A AND M) == 0, then M &amp;= ~A.</summary>
    public static byte Trb(CPU cpu, byte value)
    {
        cpu.ProcessorStatus.Zero = (cpu.A & value) == 0;
        return (byte)(value & ~cpu.A);
    }
}
