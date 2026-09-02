using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// Structural guard for the cycle contract: on a 6502 every clock cycle is a bus access, so the
/// number of accesses an instruction performs must equal the cycle count it reports, for every
/// opcode byte of every model and compatibility profile, from random register and memory state
/// (seeded, so a failure reproduces). The SingleStepTests vectors prove the exact addresses for
/// the bytes they cover; this test covers the rest (the 65C02's undefined-byte NOPs, the bytes
/// the corpus is skipped on) and every profile.
/// </summary>
public class OneBusAccessPerCycleTests
{
    private const int StatesPerOpcode = 8;

    public static IEnumerable<object[]> ModelsAndProfiles()
    {
        foreach (var profile in Enum.GetValues<CpuCompatibilityProfile>())
        {
            yield return [CpuModelIds.Nmos6502, profile];
            yield return [CpuModelIds.Mos6510, profile];
        }
        yield return [CpuModelIds.Ncr65c02, CpuCompatibilityProfile.OfficialOnly];
    }

    [Theory, MemberData(nameof(ModelsAndProfiles))]
    public void Every_Instruction_Performs_Exactly_One_Bus_Access_Per_Cycle(string cpuModelId, CpuCompatibilityProfile profile)
    {
        var cpu = new CPU(NullLoggerFactory.Instance, cpuModelId, profile);
        var mem = new Memory();
        var random = new Random(0x6502);
        var failures = new List<string>();

        for (var code = 0; code <= 0xFF; code++)
        {
            var descriptor = cpu.Descriptors[(byte)code];
            if (descriptor is null || descriptor.Mnemonic == "JAM")
                continue;

            for (var i = 0; i < StatesPerOpcode; i++)
            {
                for (var address = 0; address < 0x10000; address += 0x100)
                    random.NextBytes(new Span<byte>(new byte[0x100]));   // keep the sequence moving per state
                var pc = (ushort)random.Next(0x0200, 0xFF00);
                cpu.PC = pc;
                cpu.SP = (byte)random.Next(256);
                cpu.A = (byte)random.Next(256);
                cpu.X = (byte)random.Next(256);
                cpu.Y = (byte)random.Next(256);
                cpu.ProcessorStatus.Value = (byte)random.Next(256);
                mem[pc] = (byte)code;
                mem[(ushort)(pc + 1)] = (byte)random.Next(256);
                mem[(ushort)(pc + 2)] = (byte)random.Next(256);
                // Zero-page pointers and the stack page get random contents too.
                for (var zp = 0; zp < 0x200; zp++)
                    mem[(ushort)zp] = (byte)random.Next(256);

                var before = cpu.BusCycles;
                var result = cpu.ExecuteOneInstructionMinimal(mem);
                var accesses = cpu.BusCycles - before;
                if (accesses != result.CyclesConsumed)
                    failures.Add($"${code:x2} {descriptor.Mnemonic} {descriptor.Addressing} (A={cpu.A:x2} X={cpu.X:x2} Y={cpu.Y:x2} P={cpu.ProcessorStatus.Value:x2}): {accesses} accesses, {result.CyclesConsumed} cycles");
            }
        }

        Assert.True(failures.Count == 0, $"{cpuModelId}/{profile}: {failures.Count} mismatches\n{string.Join("\n", failures.Take(10))}");
    }
}
