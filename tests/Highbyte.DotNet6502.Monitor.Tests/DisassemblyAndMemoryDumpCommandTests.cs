using Highbyte.DotNet6502.Monitor.Tests.Helpers;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.Monitor.Tests;

/// <summary>
/// Behavior of the monitor 'd' (disassemble) and 'm' (memory dump) commands, with focus on how the
/// argument-less forms track their position and how <see cref="MonitorBase.Reset"/> (called when the
/// monitor is (re)entered) re-anchors disassembly to PC while leaving the memory dump position intact.
/// </summary>
public class DisassemblyAndMemoryDumpCommandTests
{
    private const byte OpCodeNop = 0xEA; // 1-byte instruction, so disassembly advances 1 address per instruction.

    private static TestMonitor CreateMonitor(ushort pc = 0x1000)
    {
        var system = new TestSystem();

        // Fill memory with NOPs so the disassembler advances by a known, fixed amount.
        for (int address = 0; address <= 0xffff; address++)
            system.Mem[(ushort)address] = OpCodeNop;

        system.CPU.PC = pc;

        var systemRunner = new SystemRunner(system);
        return new TestMonitor(systemRunner, new MonitorConfig());
    }

    // ---- 'd' (disassemble) ----

    [Fact]
    public void Disassemble_WithNoArguments_StartsAtCurrentPc()
    {
        var monitor = CreateMonitor(pc: 0x1000);

        monitor.SendCommand("d");

        Assert.StartsWith("1000", monitor.FirstOutputLine);
    }

    [Fact]
    public void Disassemble_RepeatedWithNoArguments_ContinuesFromPreviousPosition()
    {
        var monitor = CreateMonitor(pc: 0x1000);

        monitor.SendCommand("d"); // 10 single-byte instructions: 0x1000..0x1009, leaving the anchor at 0x100a.
        monitor.ClearOutput();
        monitor.SendCommand("d");

        Assert.StartsWith("100a", monitor.FirstOutputLine);
    }

    [Fact]
    public void Disassemble_WithExplicitStartAddress_StartsThere()
    {
        var monitor = CreateMonitor(pc: 0x1000);

        monitor.SendCommand("d 2000");

        Assert.StartsWith("2000", monitor.FirstOutputLine);
    }

    [Fact]
    public void Reset_ReAnchorsDisassemblyToCurrentPc()
    {
        var monitor = CreateMonitor(pc: 0x1000);
        monitor.SendCommand("d"); // Advance the disassembly anchor past PC.

        // Simulate re-entering the monitor at a new PC.
        monitor.System.CPU.PC = 0x3000;
        monitor.Reset();
        monitor.ClearOutput();

        monitor.SendCommand("d");

        Assert.StartsWith("3000", monitor.FirstOutputLine);
    }

    // ---- 'm' (memory dump) ----

    [Fact]
    public void MemoryDump_WithNoArguments_StartsAtZero()
    {
        var monitor = CreateMonitor();

        monitor.SendCommand("m");

        Assert.StartsWith("0000", monitor.FirstOutputLine);
    }

    [Fact]
    public void MemoryDump_RepeatedWithNoArguments_ContinuesFromPreviousPosition()
    {
        var monitor = CreateMonitor();

        monitor.SendCommand("m"); // Shows 128 bytes (0x0000..0x007f), leaving the position at 0x0080.
        monitor.ClearOutput();
        monitor.SendCommand("m");

        Assert.StartsWith("0080", monitor.FirstOutputLine);
    }

    [Fact]
    public void Reset_DoesNotResetMemoryDumpPosition()
    {
        var monitor = CreateMonitor();
        monitor.SendCommand("m"); // Advance the memory dump position to 0x0080.

        // Re-entering the monitor must NOT snap the memory dump back to 0x0000 (unlike disassembly).
        monitor.Reset();
        monitor.ClearOutput();

        monitor.SendCommand("m");

        Assert.StartsWith("0080", monitor.FirstOutputLine);
    }
}
