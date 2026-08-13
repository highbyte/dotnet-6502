using Highbyte.DotNet6502.Tests.Helpers;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// Exhaustive guard for the per-model read-modify-write bus sequences: EVERY memory-RMW
/// opcode byte must perform read-write(original)-write on NMOS models and
/// read-read-write on the 65C02. The bytes are enumerated from each model's own
/// descriptor table (by mnemonic), so a table refactor that accidentally leaves an RMW
/// byte with the wrong model's sequence — e.g. a missed entry in the 65C02's CMOS
/// re-binding — fails here rather than going unnoticed: result-level tests cannot see
/// the difference, only the bus traffic can.
/// </summary>
public class RmwBusSequenceMatrixTests
{
    private const ushort StartPc = 0x1000;
    private const byte InitialTargetValue = 0x41;

    private static readonly string[] s_nmosRmwMnemonics =
        { "ASL", "LSR", "ROL", "ROR", "INC", "DEC", "SLO", "SRE", "RLA", "RRA", "DCP", "ISC" };
    private static readonly string[] s_cmosRmwMnemonics =
        { "ASL", "LSR", "ROL", "ROR", "INC", "DEC", "TSB", "TRB" };

    [Fact]
    public void Every_Nmos_Rmw_Byte_Does_Read_WriteBack_Write()
    {
        var checkedBytes = 0;
        var probe = new CPU(CpuCompatibilityProfile.FullUnofficial);
        for (var code = 0; code <= 0xff; code++)
        {
            var descriptor = probe.Descriptors[(byte)code];
            if (descriptor is null || !s_nmosRmwMnemonics.Contains(descriptor.Mnemonic)
                || descriptor.Addressing == AddrMode.Accumulator)
                continue;

            var cpu = new CPU(CpuCompatibilityProfile.FullUnofficial);
            var accesses = ExecuteAndRecordTargetAccesses(cpu, (byte)code, descriptor.Addressing);

            // Indexed modes always start with the NMOS dummy read at the un-carried
            // address (== the target here, since the setups never cross a page).
            var isIndexed = descriptor.Addressing is AddrMode.ABS_X or AddrMode.ABS_Y or AddrMode.IND_IX;
            var expectedCount = isIndexed ? 4 : 3;
            Assert.True(accesses.Count == expectedCount,
                $"{descriptor.Mnemonic} ({code:x2}, {descriptor.Addressing}): expected {expectedCount} target accesses, got {accesses.Count}");
            var i = 0;
            if (isIndexed)
                Assert.True(accesses[i++].IsRead, $"{code:x2}: dummy read must come first");
            Assert.True(accesses[i++].IsRead, $"{code:x2}: read must precede the writes");
            Assert.True(!accesses[i].IsRead && accesses[i].Value == InitialTargetValue,
                $"{descriptor.Mnemonic} ({code:x2}): must write the ORIGINAL value back before the result");
            Assert.False(accesses[i + 1].IsRead, $"{code:x2}: final access must write the result");
            checkedBytes++;
        }

        // 24 official (6 ops x zp/zp,X/abs/abs,X) + 42 illegal (6 combos x 7 modes).
        Assert.Equal(66, checkedBytes);
    }

    [Fact]
    public void Every_Cmos_Rmw_Byte_Does_Read_Read_Write()
    {
        var checkedBytes = 0;
        var probe = New65c02Cpu();
        for (var code = 0; code <= 0xff; code++)
        {
            var descriptor = probe.Descriptors[(byte)code];
            if (descriptor is null || !s_cmosRmwMnemonics.Contains(descriptor.Mnemonic)
                || descriptor.Addressing == AddrMode.Accumulator)
                continue;

            var cpu = New65c02Cpu();
            var accesses = ExecuteAndRecordTargetAccesses(cpu, (byte)code, descriptor.Addressing);

            Assert.True(accesses.Count == 3,
                $"{descriptor.Mnemonic} ({code:x2}, {descriptor.Addressing}): expected 3 target accesses, got {accesses.Count}");
            Assert.True(accesses[0].IsRead && accesses[1].IsRead,
                $"{descriptor.Mnemonic} ({code:x2}): first two accesses must be reads (65C02 replaces the NMOS write-back with a second read)");
            Assert.False(accesses[2].IsRead, $"{code:x2}: third access must write the result");
            checkedBytes++;
        }

        // 24 official RMW bytes + TSB zp/abs + TRB zp/abs.
        Assert.Equal(28, checkedBytes);
    }

    private static CPU New65c02Cpu()
        => new(new ExecState(), new NullLoggerFactory(), CpuModelIds.Ncr65c02, CpuCompatibilityProfile.OfficialOnly);

    /// <summary>
    /// Writes the instruction with operands arranged so its effective address is a single
    /// watched target byte (indirect pointers and index registers live outside the watch),
    /// executes it, and returns the recorded accesses at the target.
    /// </summary>
    private static IReadOnlyList<BusAccessRecorder.BusAccess> ExecuteAndRecordTargetAccesses(CPU cpu, byte opCode, AddrMode addressing)
    {
        var mem = new Memory();
        cpu.PC = StartPc;
        cpu.SP = 0xFF;
        mem[StartPc] = opCode;

        ushort target;
        switch (addressing)
        {
            case AddrMode.ZP:
                target = 0x0080;
                mem[StartPc + 1] = 0x80;
                break;
            case AddrMode.ZP_X:
                target = 0x0080;
                cpu.X = 0x10;
                mem[StartPc + 1] = 0x70;
                break;
            case AddrMode.ABS:
                target = 0x3080;
                mem.WriteWord((ushort)(StartPc + 1), 0x3080);
                break;
            case AddrMode.ABS_X:
                target = 0x3080;
                cpu.X = 0x10;
                mem.WriteWord((ushort)(StartPc + 1), 0x3070);
                break;
            case AddrMode.ABS_Y:
                target = 0x3080;
                cpu.Y = 0x10;
                mem.WriteWord((ushort)(StartPc + 1), 0x3070);
                break;
            case AddrMode.IX_IND:
                target = 0x3080;
                cpu.X = 0x04;
                mem[StartPc + 1] = 0x40;
                mem.WriteWord(0x0044, 0x3080);
                break;
            case AddrMode.IND_IX:
                target = 0x3080;
                cpu.Y = 0x10;
                mem[StartPc + 1] = 0x40;
                mem.WriteWord(0x0040, 0x3070);
                break;
            default:
                throw new DotNet6502Exception($"Unexpected RMW addressing mode {addressing} for opcode {opCode:x2}.");
        }

        mem[target] = InitialTargetValue;
        var recorder = new BusAccessRecorder();
        recorder.Watch(mem, target, 1);

        cpu.ExecuteOneInstructionMinimal(mem);
        return recorder.Accesses;
    }
}
