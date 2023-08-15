namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// </summary>
public class CPUHelperTest
{
    [Fact]
    public void Can_Get_Next_Instruction_Address_If_Current_Address_Has_Size_1()
    {
        // Arrange
        var cpu = new CPU();
        var mem = new Memory();

        var opCodeByte = (byte)OpCodeId.NOP;    // NOP has size of 1 byte.
        mem[0x1000] = opCodeByte;
        cpu.PC = 0x1000;

        // Act
        var nextInstructionAddress = cpu.GetNextInstructionAddress(mem);

        // Assert
        var insSize = (byte)cpu.InstructionList.GetOpCode(opCodeByte).Size;
        Assert.Equal(cpu.PC + insSize, nextInstructionAddress);
    }

    [Fact]
    public void Can_Get_Next_Instruction_Address_If_Current_Address_Has_Size_2()
    {
        // Arrange
        var cpu = new CPU();
        var mem = new Memory();

        var opCodeByte = (byte)OpCodeId.LDA_I;    // LDA_I has size of 2 byte.
        mem[0x1000] = opCodeByte;
        cpu.PC = 0x1000;

        // Act
        var nextInstructionAddress = cpu.GetNextInstructionAddress(mem);

        // Assert
        var insSize = (byte)cpu.InstructionList.GetOpCode(opCodeByte).Size;
        Assert.Equal(cpu.PC + insSize, nextInstructionAddress);
    }

    [Fact]
    public void Next_Instruction_Address_Returns_1_Byte_Ahead_If_Current_Address_Has_Unknown_Instruction()
    {
        // Arrange
        var cpu = new CPU();
        var mem = new Memory();

        var opCodeByte = (byte)0xff;    // 0xff (255) is not a known opcode.
        mem[0x1000] = opCodeByte;
        cpu.PC = 0x1000;
        
        // Act
        var nextInstructionAddress = cpu.GetNextInstructionAddress(mem);

        // Assert
        // An unknown OP code at current address should have assumed size of 1 byte
        var insSize = 1;
        Assert.Equal(cpu.PC + insSize, nextInstructionAddress);
    }

}
