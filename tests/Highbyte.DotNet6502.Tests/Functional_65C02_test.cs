using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// Klaus Dormann test binaries run against the ncr65c02 model — the external arbiter
/// for the 65C02 implementation (feature cpu-models-65c02, M1 test gates).
///
/// Both tests assemble from the pinned source revision with settings changed from the
/// source DEFAULTS, which target a Rockwell/WDC part: the base/NCR 65C02 must execute
/// the Rockwell/WDC opcode bytes as NOPs, so the extended-opcodes test is configured
/// with rkwl_wdc_op=0 and wdc_op=0 (test those bytes AS NOPs — the pre-built binary in
/// the Klaus repo uses the defaults and would rightly fail on this model).
/// Assembly requires Windows (AS65); on other platforms these tests skip. CI runs them
/// (windows-latest).
/// </summary>
[Trait("TestType", "Integration")]
public class Functional_65C02_test
{
    private readonly ITestOutputHelper _output;
    public Functional_65C02_test(ITestOutputHelper testOutputHelper)
    {
        _output = testOutputHelper;
    }

    private static CPU New65c02Cpu()
        => new(new ExecState(), new NullLoggerFactory(), CpuModelIds.Ncr65c02, CpuCompatibilityProfile.OfficialOnly);

    [WindowsOnlyFact]
    public void Can_Run_65C02_Extended_Opcodes_Test_With_Rockwell_And_WDC_Bytes_As_NOPs()
    {
        // Arrange
        var compiler = new FunctionalTestCompiler(NullLogger<FunctionalTestCompiler>.Instance);
        var build = compiler.GetKlausTestBuild(
            "65C02_extended_opcodes_test.a65c",
            new Dictionary<string, string>
            {
                ["rkwl_wdc_op"] = "0", // Rockwell RMB/SMB/BBR/BBS bytes: test as NOPs (base/NCR part)
                ["wdc_op"] = "0",      // WDC WAI/STP bytes: test as NOPs
            });

        // Same memory layout convention as the NMOS functional test: zero_page = $a,
        // so the assembled image is loaded at $000A. Execution starts at $0400.
        ushort loadAddress = 0x000A;
        ushort startAddress = 0x0400;
        var successAddress = FunctionalTestCompiler.FindLabelAddressInListFile(build.ListFilePath, "success");
        _output.WriteLine($"Success label address (from .lst): {successAddress.ToHex()}");

        var mem = BinaryLoader.Load(build.BinaryFilePath, out _, out _, forceLoadAddress: loadAddress);
        var cpu = New65c02Cpu();
        cpu.PC = startAddress;

        // Act: run until the program traps in a self-loop (both success and failure end
        // that way) or the instruction budget is exhausted.
        var maxInstructions = 100_000_000;
        var insCount = 0;
        while (insCount < maxInstructions)
        {
            insCount++;
            ushort pcBefore = cpu.PC;
            cpu.ExecuteOneInstruction(mem);
            if (cpu.PC == pcBefore)
                break; // self-trap loop
        }

        _output.WriteLine($"Instructions executed: {insCount}");
        _output.WriteLine($"CPU last PC: {cpu.PC.ToHex()} (success = {successAddress.ToHex()})");

        // Assert: trapped at the success label. On failure the trap PC identifies the
        // failing test in the .lst file.
        Assert.Equal(successAddress, cpu.PC);
    }

    [WindowsOnlyFact]
    public void Can_Run_Decimal_Test_With_65C02_Flag_Expectations()
    {
        // Arrange: cputype=1 selects the 65C02 prediction routines; check all flags the
        // 65C02 defines validly in decimal mode (N, Z) plus V, A and C. Invalid BCD
        // operands stay enabled (vld_bcd=0 default) — the implementation follows the
        // 65C02 sequence exactly, so it must match for invalid operands too.
        var compiler = new FunctionalTestCompiler(NullLogger<FunctionalTestCompiler>.Instance);
        var build = compiler.GetKlausTestBuild(
            "6502_decimal_test.a65",
            new Dictionary<string, string>
            {
                ["cputype"] = "1",
                ["chk_n"] = "1",
                ["chk_v"] = "1",
                ["chk_z"] = "1",
            });

        // The decimal test's code origin is $0200; ERROR result byte lives at $0B.
        ushort loadAddress = 0x0200;
        ushort errorAddress = 0x000B;
        var doneAddress = FunctionalTestCompiler.FindLabelAddressInListFile(build.ListFilePath, "done");
        _output.WriteLine($"Done label address (from .lst): {doneAddress.ToHex()}");

        var mem = BinaryLoader.Load(build.BinaryFilePath, out _, out _, forceLoadAddress: loadAddress);
        var cpu = New65c02Cpu();
        cpu.PC = loadAddress;

        // Act: run until PC reaches the done label (its $DB byte is a NOP on the base
        // 65C02, so PC — not a halt — is the termination signal) or budget exhausted.
        var maxInstructions = 100_000_000;
        var insCount = 0;
        while (insCount < maxInstructions && cpu.PC != doneAddress)
        {
            insCount++;
            cpu.ExecuteOneInstruction(mem);
        }

        _output.WriteLine($"Instructions executed: {insCount}");
        _output.WriteLine($"CPU last PC: {cpu.PC.ToHex()} (done = {doneAddress.ToHex()})");
        _output.WriteLine($"ERROR byte at {errorAddress.ToHex()}: {mem[errorAddress]}");

        // Assert: reached done with ERROR == 0.
        Assert.Equal(doneAddress, cpu.PC);
        Assert.Equal(0, mem[errorAddress]);
    }
}
