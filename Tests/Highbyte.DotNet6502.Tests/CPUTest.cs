using Xunit;

namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// General CPU execution tests.
/// Note: Tests for individual instructions are in separate test in the Instructions directory.
/// </summary>
public class CPUTest
{
    [Fact]
    public void CPU_Handles_Hardware_IRQ_When_InterruptDisable_Is_Not_Set()
    {
        // Arrange
        var cpu = new CPU();
        var mem = new Memory();

        mem[0x1000] = (byte)OpCodeId.NOP;
        cpu.PC = 0x1000;
        // Set status flags that should be changed when IRQ occurs.
        cpu.ProcessorStatus.Break = true;
        cpu.ProcessorStatus.Unused = false;
        cpu.ProcessorStatus.InterruptDisable = false;

        cpu.IRQ = true; // Tell CPU that a hardware IRQ occurred.

        // Act
        var execState = cpu.Execute(
            mem,
            new LegacyExecEvaluator(new ExecOptions { MaxNumberOfInstructions = 1, UnknownInstructionThrowsException = false }));

        // Assert
        // The NMI flag cleared
        Assert.False(cpu.IRQ);
        // The current PS should have InterruptDisable set
        Assert.True(cpu.ProcessorStatus.InterruptDisable);

        // The current PC (after current instruction above was executed) should have been pushed to the stack
        var pcOnStackAddress = (ushort)(CPU.StackBaseAddress + (byte)(cpu.SP + 4)); // This should have been pushed first, and thus a higher address (Stack counts downwards)
        var addrFromStack = new byte[2];
        addrFromStack[0] = mem[pcOnStackAddress];                 // lowbyte first
        addrFromStack[1] = mem[(ushort)((byte)(pcOnStackAddress + 1))];   // highbyte second
        var pcOnStack = ByteHelpers.ToLittleEndianWord(addrFromStack);
        Assert.Equal(cpu.PC, pcOnStack);

        // After the PC pushed on stack, a copy of the ProcessorStatus should have been pushed, with Break = false and Unused = true
        var psOnStackAddress = (ushort)(CPU.StackBaseAddress + (byte)(cpu.SP + 1));
        var psOnStackValue = mem[psOnStackAddress];
        var psOnStack = new ProcessorStatus(psOnStackValue);
        Assert.False(psOnStack.Break);
        Assert.True(psOnStack.Unused);

        // The PC should have been changed to the IRQ handler address
        Assert.Equal(cpu.FetchWord(mem, CPU.BrkIRQHandlerVector), cpu.PC);
    }

    [Fact]
    public void CPU_Skips_Hardware_IRQ_When_InterruptDisable_Is_Set()
    {
        // Arrange
        var cpu = new CPU();
        var mem = new Memory();

        ushort originalPC = 0x1000;
        ushort originalSP = cpu.SP;

        mem[originalPC] = (byte)OpCodeId.NOP;
        cpu.PC = originalPC;
        // Set status flags that should be changed when IRQ occurs.
        cpu.ProcessorStatus.Break = true;
        cpu.ProcessorStatus.Unused = false;
        cpu.ProcessorStatus.InterruptDisable = true;

        cpu.IRQ = true; // Tell CPU that a hardware IRQ occurred.

        // Act
        var execState = cpu.Execute(
            mem,
            new LegacyExecEvaluator(new ExecOptions { MaxNumberOfInstructions = 1, UnknownInstructionThrowsException = false }));

        // Assert
        Assert.True(cpu.ProcessorStatus.InterruptDisable);
        Assert.Equal(originalPC + 1, cpu.PC);   // PC should not have been changed to the IRQ handler
        Assert.Equal(originalSP, cpu.SP);       // StackPointer should not have been changed (PC/PS not pushed to stack)
    }


    [Fact]
    public void CPU_Handles_Hardware_NMI_Even_When_InterruptDisable_Is_Set()
    {
        // Arrange
        var cpu = new CPU();
        var mem = new Memory();

        mem[0x1000] = (byte)OpCodeId.NOP;
        cpu.PC = 0x1000;
        // Set status flags that should be changed when IRQ occurs.
        cpu.ProcessorStatus.Break = true;
        cpu.ProcessorStatus.Unused = false;
        cpu.ProcessorStatus.InterruptDisable = true;

        cpu.NMI = true; // Tell CPU that a hardware NMI occurred.

        // Act
        var execState = cpu.Execute(
            mem,
            new LegacyExecEvaluator(new ExecOptions { MaxNumberOfInstructions = 1, UnknownInstructionThrowsException = false }));

        // Assert
        // The NMI flag cleared
        Assert.False(cpu.NMI);
        // The current PS should have InterruptDisable set
        Assert.True(cpu.ProcessorStatus.InterruptDisable);

        // The current PC (after current instruction above was executed) should have been pushed to the stack
        var pcOnStackAddress = (ushort)(CPU.StackBaseAddress + (byte)(cpu.SP + 4)); // This should have been pushed first, and thus a higher address (Stack counts downwards)
        var addrFromStack = new byte[2];
        addrFromStack[0] = mem[pcOnStackAddress];                 // lowbyte first
        addrFromStack[1] = mem[(ushort)((byte)(pcOnStackAddress + 1))];   // highbyte second
        var pcOnStack = ByteHelpers.ToLittleEndianWord(addrFromStack);
        Assert.Equal(cpu.PC, pcOnStack);

        // After the PC pushed on stack, a copy of the ProcessorStatus should have been pushed, with Break = false and Unused = true
        var psOnStackAddress = (ushort)(CPU.StackBaseAddress + (byte)(cpu.SP + 1));
        var psOnStackValue = mem[psOnStackAddress];
        var psOnStack = new ProcessorStatus(psOnStackValue);
        Assert.False(psOnStack.Break);
        Assert.True(psOnStack.Unused);

        // The PC should have been changed to the NMI handler address
        Assert.Equal(cpu.FetchWord(mem, CPU.NonMaskableIRQHandlerVector), cpu.PC);
    }

    [Fact]
    public void CPU_Can_Detect_Unknown_OpCode()
    {
        // Arrange
        var cpu = new CPU();
        var mem = new Memory();

        mem[0x1000] = 0x02; // OpCode that does not exist
        cpu.PC = 0x1000;

        // Act
        var execState = cpu.Execute(
            mem,
            new LegacyExecEvaluator(new ExecOptions{MaxNumberOfInstructions=1, UnknownInstructionThrowsException = false}));

        // Assert
        Assert.False(execState.LastOpCodeWasHandled);
        Assert.Equal((ulong)1, (ulong)execState.UnknownOpCodeCount);
    }

    [Fact]
    public void ExecuteOneInstruction_Only_Executes_One_Instruction()
    {
        // Arrange
        var cpu = new CPU();
        var mem = new Memory();

        ushort originalPC = 0x1000;
        mem[originalPC] = (byte)OpCodeId.NOP;
        cpu.PC = originalPC;

        // Act
        var execState = cpu.ExecuteOneInstruction(
            mem);

        // Assert
        Assert.Equal(originalPC + 1, cpu.PC);   // NOP is 1 byte
        Assert.Equal((ulong)1, execState.InstructionsExecutionCount);
    }

    [Fact]
    public void ExecuteOneInstructionMinimal_Only_Executes_One_Instruction()
    {
        // Arrange
        var cpu = new CPU();
        var mem = new Memory();

        ushort originalPC = 0x1000;
        mem[originalPC] = (byte)OpCodeId.NOP;
        cpu.PC = originalPC;

        // Act
        var knownInstruction = cpu.ExecuteOneInstructionMinimal(
            mem,
            out ulong cyclesConsumed
            );

        // Assert
        Assert.Equal(originalPC + 1, cpu.PC);   // NOP is 1 byte
    }

    [Fact]
    public void CPU_Can_Detect_Unknown_OpCode_With_MinimalExecution()
    {
        // Arrange
        var cpu = new CPU();
        var mem = new Memory();

        mem[0x1000] = 0x02; // OpCode that does not exist
        cpu.PC = 0x1000;

        // Act
        var knownInstruction = cpu.ExecuteOneInstructionMinimal(
            mem,
            out ulong cyclesConsumed
            );

        // Assert
        Assert.False(knownInstruction);
    }

    [Fact]
    public void CPU_Can_Be_Reset_And_Start_At_Address_Specified_In_ResetVector()
    {
        var cpu = new CPU();
        var mem = new Memory();
        mem.WriteWord(CPU.ResetVector, 0xc000);
        cpu.Reset(mem);
        Assert.Equal(0xc000, cpu.PC);

        // Not sure if the CPU hardware will have SP set to 0xff on power on, or if there is code in the reset vector in ROM that does this.
        // Assert.Equal(0xff, cpu.SP);  
    }
}
