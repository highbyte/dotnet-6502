using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class MemoryTest
    {
        [Fact]
        public void Initializing_Memory_With_Defaults_Should_Create_64K_Memory()
        {
            // Arrange
            var mem = new Memory();
            // Act / Assert
            Assert.Equal(64*1024, (int)mem.Size);
        }

        [Fact]
        public void Can_Initialize_Memory_In_Bank_0_With_Data_And_Read_It_Back()
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
        public void Can_Initialize_Memory_In_Bank_1_With_Data_And_Read_It_Back()
        {
            // Arrange
            var mem = new Memory();
            mem[0x2000] = 0x42;
            mem[0x2001] = 0x21;

            // Act
            var data = mem.ReadData(0x2000, 2);

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
