using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class MemoryHelperTest
    {

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

        // TODO: More unit tests for MemoryHelper class
    }
}
