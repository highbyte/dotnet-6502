using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class MemoryTest
    {
        [Fact]
        public void Can_Initialize_Memory_With_Data_And_Read_It_Back()
        {
            // Arrange
            var mem = new Memory();
            mem[0x1000] = 0x42;
            mem[0x1001] = 0x21;

            // Act
            var data = mem.ReadData(0x1000, 2);

            // Assert
            Assert.Equal(0x42, data[0]);
            Assert.Equal(0x21, data[1]);
        }

        [Fact]
        public void Set_Bit_Updates_Memory_As_Expected()
        {
            // Arrange
            var mem = new Memory();
            mem[0x1000] = 0b00010110;

            // Act
            mem.SetBit(0x1000, 3);

            // Assert
            Assert.Equal(0b00011110, mem[0x1000]);
        }

        [Fact]
        public void Clear_Bit_Updates_Memory_As_Expected()
        {
            // Arrange
            var mem = new Memory();
            mem[0x1000] = 0b00011110;

            // Act
            mem.ClearBit(0x1000, 3);

            // Assert
            Assert.Equal(0b00010110, mem[0x1000]);
        }             

        // TODO: More unit tests for Memory class
    }
}
