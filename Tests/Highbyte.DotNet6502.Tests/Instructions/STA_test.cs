using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions;

public class STA_test
{
    [Fact]
    public void STA_ZP_Takes_3_Cycles()
    {
        var test = new TestSpec()
        {
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.STA_ZP,
            ExpectedCycles = 3,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    [Fact]
    public void STA_ZP_Stores_A_Register_To_Memory()
    {
        var test = new TestSpec
        {
            A              = 0x42,
            InsEffect      = InstrEffect.Mem,    // Defaults to Read
            OpCode         = OpCodeId.STA_ZP,
            FinalValue     = 0x01, // Initial value at memory the instruction will write to. If not specified, a default value will be written there before instruction executes.
            //ZeroPageAddress = 0x30, // Optional ZeroPage address. If not specified, a default address is used.
            ExpectedMemVal = 0x42, // Should be the value we had in A register before instruction executes.
            ExpectedCycles = 3,
        };
        test.Execute_And_Verify(AddrMode.ZP);
    }

    // ----------------------------------------------------------------------------------------
    // Other addressing modes than _ZP
    // ----------------------------------------------------------------------------------------

    /// <summary>
    /// Only test addressing mode works, the binary arithmetic (carry, zero, overflow flags) where tested
    /// in the STA_ZP instruction above, and are used by all addressing modes.
    /// </summary>
    [Fact]
    public void STA_ZP_X_Works()
    {
        var test = new TestSpec
        {
            A              = 0x42,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.STA_ZP_X,
            FinalValue     = 0x01,
            ExpectedMemVal = 0x42,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ZP_X);
    }

    /// <summary>
    /// The address calculation wraps around if the sum of the base address and the register exceed $FF. 
    /// If we repeat the last example but with $FF in the X register then the accumulator will be stored to memory $007F (e.g. $80 + $FF => $7F) and not $017F.
    /// </summary>
    [Fact]
    public void STA_ZP_X_Where_ZeroPage_Address_Plus_X_Wraps_Over_Byte_Size_Works()
    {
        var test = new TestSpec
        {
            A              = 0x42,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.STA_ZP_X,
            FinalValue     = 0x01,
            ExpectedMemVal = 0x42,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ZP_X, ZP_X_Should_Wrap_Over_Byte: true);
    }

    [Fact]
    public void STA_ABS_Works()
    {
        var test = new TestSpec
        {
            A              = 0x42,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.STA_ABS,
            FinalValue     = 0x01,
            ExpectedMemVal = 0x42,
            ExpectedCycles = 4,
        };
        test.Execute_And_Verify(AddrMode.ABS);
    }

    [Fact]
    public void STA_ABS_X_Works()
    {
        var test = new TestSpec
        {
            A              = 0x42,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.STA_ABS_X,
            FinalValue     = 0x01,
            ExpectedMemVal = 0x42,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.ABS_X);
    }

    [Fact]
    public void STA_ABS_X_Where_Address_Plus_X_Crosses_Page_Boundary_Works()
    {
        var test = new TestSpec
        {
            A              = 0x42,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.STA_ABS_X,
            FinalValue     = 0x01,
            ExpectedMemVal = 0x42,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.ABS_X, FullAddress_Should_Cross_Page_Boundary: true);
    }

    [Fact]
    public void STA_ABS_Y_Works()
    {
        var test = new TestSpec
        {
            A              = 0x42,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.STA_ABS_Y,
            FinalValue     = 0x01,
            ExpectedMemVal = 0x42,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.ABS_Y);
    }

    [Fact]
    public void STA_ABS_Y_Where_Address_Plus_Y_Crosses_Page_Boundary_Works()
    {
        var test = new TestSpec
        {
            A              = 0x42,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.STA_ABS_Y,
            FinalValue     = 0x01,
            ExpectedMemVal = 0x42,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.ABS_Y, FullAddress_Should_Cross_Page_Boundary: true);
    }
    
    
    /// <summary>
    /// This mode is only used with the X register.
    /// Consider a situation where the instruction is
    ///    SOME_INSTRUCTION ($20,X)
    ///  X contains $04, and memory at $24 contains 74 20.
    /// First, X is added to $20 to get $24.
    /// The target address will be fetched from $24 resulting in a target address of $2074.
    /// The final value to use in the instruction will be at memory $2074.
    /// 
    /// If X + the immediate byte will wrap around to a zero-page address. 
    /// So you could code that like targetAddress = (X + opcode[1]) & 0xFF 
    /// 
    /// Indexed Indirect instructions are 2 bytes - the second byte is the zero-page address - $20 in the example. Obviously the fetched address has to be stored in the zero page.
    /// </summary>
    [Fact]
    public void STA_IX_IND_Works()
    {
        var test = new TestSpec
        {
            A              = 0x42,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.STA_IX_IND,
            FinalValue     = 0x01,
            ExpectedMemVal = 0x42,
            ExpectedCycles = 6,
        };
        test.Execute_And_Verify(AddrMode.IX_IND);            
    }
    
    /// <summary>
    /// Same as _IX_IND test above, but test zero-page wrap around if zero page address + X > 0xff
    /// </summary>
    [Fact]
    public void STA_IX_IND_When_ZeroPage_Address_Plus_X_Wraps_Around_Works()
    {
        var test = new TestSpec
        {
            A              = 0x42,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.STA_IX_IND,
            FinalValue     = 0x01,
            ExpectedMemVal = 0x42,
            ExpectedCycles = 6,
        };
        test.Execute_And_Verify(AddrMode.IX_IND, ZP_X_Should_Wrap_Over_Byte: true);
    }


    /// <summary>
    /// This mode is only used with the Y register. 
    /// 
    /// It differs in the order that Y is applied to the indirectly fetched address. 
    /// An example instruction that uses indirect index addressing is 
    ///    SOME_INSTRUCTION ($86),Y
    /// To calculate the target address, the CPU will first fetch the address stored at zero page location $86. 
    /// That address will be added to register Y to get the final target address. 
    /// For SOME_INSTRUCTION ($86),Y, if the address stored at $86 is $4028 (memory is 0086: 28 40, remember little endian) 
    /// and register Y contains $10, then the final target address would be $4038. 
    /// 
    /// The final value to be used with the instruction is at memory $4038.
    /// 
    /// Indirect Indexed instructions are 2 bytes - the second byte is the zero-page address - $86 in the example. (So the fetched address has to be stored in the zero page.)
    /// 
    /// While indexed indirect addressing will only generate a zero-page address, this mode's target address is not wrapped - it can be anywhere in the 16-bit address space.
    /// </summary>
    [Fact]
    public void STA_IND_IX_Works()
    {
        var test = new TestSpec
        {
            A              = 0x42,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.STA_IND_IX,
            FinalValue     = 0x01,
            ExpectedMemVal = 0x42,
            ExpectedCycles = 6,
        };
        test.Execute_And_Verify(AddrMode.IND_IX);   
    }

    /// <summary>
    /// Same as _IND_IX above, 
    /// but tests if indirect address (read from zero page) + Y register crosses a page boundary.
    /// If so, an extra cycle is used.
    /// </summary>
    [Fact]
    public void STA_IND_IX_When_ZeroPage_Address_Vector_Plus_Y_Crosses_Page_Works()
    {
        var test = new TestSpec
        {
            A              = 0x42,
            InsEffect      = InstrEffect.Mem,
            OpCode         = OpCodeId.STA_IND_IX,
            FinalValue     = 0x01,
            ExpectedMemVal = 0x42,
            ExpectedCycles = 6,
        };
        test.Execute_And_Verify(AddrMode.IND_IX, FullAddress_Should_Cross_Page_Boundary: true); 
    }
}
