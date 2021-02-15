using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class OutputMemGenTest
    {
        [Fact]
        public void OutputGen_Returns_Correctly_Formatted_Memory_If_More_Than_Row_Size()
        {
            // Arrange
            var mem = new Memory();
            ushort address = 0x1000;
            mem[address++] = 0x01;
            mem[address++] = 0xa1;
            mem[address++] = 0xff;

            // Act
            var outputList = OutputMemoryGen.GetFormattedMemoryList(mem, 0x1000, 0x1002);

            // Assert
            Assert.Single(outputList);
            Assert.Equal("1000  01 a1 ff                                          ", outputList[0]);
        }

        [Fact]
        public void OutputGen_Returns_Correctly_Formatted_Memory_If_Exactly_Row_Size()
        {
            // Arrange
            var mem = new Memory();
            ushort address = 0xc0a0;
            // Row 1
            mem[address++] = 0x00;
            mem[address++] = 0x01;
            mem[address++] = 0x02;
            mem[address++] = 0x03;

            mem[address++] = 0x04;
            mem[address++] = 0x05;
            mem[address++] = 0x06;
            mem[address++] = 0x07;

            mem[address++] = 0x08;
            mem[address++] = 0x09;
            mem[address++] = 0x0a;
            mem[address++] = 0x0b;

            mem[address++] = 0x0c;
            mem[address++] = 0x0d;
            mem[address++] = 0x0e;
            mem[address++] = 0x0f;

            // Act
            var outputList = OutputMemoryGen.GetFormattedMemoryList(mem, 0xc0a0, 0xc0af);

            // Assert
            Assert.Single(outputList);
            Assert.Equal("c0a0  00 01 02 03  04 05 06 07  08 09 0a 0b  0c 0d 0e 0f", outputList[0]);
        }

        [Fact]
        public void OutputGen_Returns_Correctly_Formatted_Memory_If_Exactly_More_Thane_One_Row_But_Less_Than_Two()
        {
            // Arrange
            var mem = new Memory();
            ushort address = 0x1000;
            // Row 1
            mem[address++] = 0x00;
            mem[address++] = 0x01;
            mem[address++] = 0x02;
            mem[address++] = 0x03;

            mem[address++] = 0x04;
            mem[address++] = 0x05;
            mem[address++] = 0x06;
            mem[address++] = 0x07;

            mem[address++] = 0x08;
            mem[address++] = 0x09;
            mem[address++] = 0x0a;
            mem[address++] = 0x0b;

            mem[address++] = 0x0c;
            mem[address++] = 0x0d;
            mem[address++] = 0x0e;
            mem[address++] = 0x0f;

            // Row 2
            mem[address++] = 0x10;

            // Act
            var outputList = OutputMemoryGen.GetFormattedMemoryList(mem, 0x1000, 0x1010);

            // Assert
            Assert.Equal(2, outputList.Count);
            Assert.Equal("1000  00 01 02 03  04 05 06 07  08 09 0a 0b  0c 0d 0e 0f", outputList[0]);
            Assert.Equal("1010  10                                                ", outputList[1]);
        }              


        // [Theory]
        // [InlineData(AddrMode.Accumulator,   new byte[]{},           "A")]
        // [InlineData(AddrMode.I,             new byte[]{0xee},       "#$EE")]
        // [InlineData(AddrMode.ZP,            new byte[]{0x01},       "$01")]
        // [InlineData(AddrMode.ZP_X,          new byte[]{0x02},       "$02,X")]
        // [InlineData(AddrMode.ZP_Y,          new byte[]{0x03},       "$03,Y")]
        // [InlineData(AddrMode.Relative,      new byte[]{0x00},       "*+0")]
        // [InlineData(AddrMode.Relative,      new byte[]{0x04},       "*+4")]
        // [InlineData(AddrMode.Relative,      new byte[]{0x80},       "*-128")]
        // [InlineData(AddrMode.ABS,           new byte[]{0x10,0xc0},  "$C010")]
        // [InlineData(AddrMode.ABS_X,         new byte[]{0xf0,0x80},  "$80F0,X")]
        // [InlineData(AddrMode.ABS_Y,         new byte[]{0x42,0x21},  "$2142,Y")]
        // [InlineData(AddrMode.Indirect,      new byte[]{0x37,0x13},  "($1337)")]
        // [InlineData(AddrMode.IX_IND,        new byte[]{0x42},       "($42,X)")]
        // [InlineData(AddrMode.IND_IX,        new byte[]{0x21},       "($21),Y")]
        // public void OutputGen_Returns_Correctly_Formatted_Operand_String_For_AddrMode(AddrMode addrMode, byte[] operand, string expectedOutput)
        // {
        //     // Act
        //     var outputString = OutputGen.BuildOperandString(addrMode, operand);

        //     // Assert
        //     Assert.Equal(expectedOutput, outputString);
        // }

    }
}
