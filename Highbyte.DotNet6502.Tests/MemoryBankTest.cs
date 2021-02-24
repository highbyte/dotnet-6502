using System;
using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class MemorySegmentswitchingTest
    {
        [Fact]
        public void Creating_Memory_With_Only_The_Required_Bank_0()
        {
            // Arrange
            var mem = new Memory(8192);

            // Act/Assert
            Assert.Single(mem.MemorySegments);
        }

        [Fact]
        public void Creating_Less_Memory_Than_Required_For_Bank_0_Should_Throw_Exception()
        {
            // Arrange // Act // Assert
            Assert.Throws<ArgumentException>(() => new Memory(Memory.SEGMENT_0_SIZE - 1));
        }

        [Fact]
        public void Creating_More_Memory_Than_Required_Bank_0_But_With_Size_Not_Evenly_Divisible_By_BankSize_Should_Throw_Exception()
        {
            // Arrange // Act // Assert
            Assert.Throws<ArgumentException>(() => new Memory(Memory.SEGMENT_0_SIZE + 1));
        }

        [Fact]
        public void Creating_More_Memory_Than_Maximum_64K_Should_Throw_Exception()
        {
            // Arrange // Act // Assert
            Assert.Throws<ArgumentException>(() => new Memory((64*1024)+1));
        }


        [Fact]
        public void Creating_More_Memory_Than_Required_Bank_0_With_Size_Evenly_Divisible_By_BankSize_Should_Throw_Exception()
        {
            // Arrange
            var mem = new Memory(Memory.SEGMENT_0_SIZE + (1 * Memory.ADDITIONAL_SEGMENT_SIZE));

            // Act/Assert
            Assert.Equal(2, mem.MemorySegments.Count);
        }          

        [Fact]
        public void Changing_Bank_0_Should_Not_Be_Allowed_And_Throws_Exception()
        {
            // Arrange
            var mem = new Memory();

            // Act/Assert
            Assert.Throws<ArgumentException>(() => mem.ChangeSegmentBank(0,0));
        }

        [Fact]
        public void Changing_A_SegmentNumber_That_Does_Not_Exist_Should_Not_Be_Allowed_And_Throws_Exception()
        {
            // Arrange
            var mem = new Memory();

            // Act/Assert
            Assert.Throws<ArgumentException>(() => mem.ChangeSegmentBank((byte)(mem.MemorySegments.Count + 1),0));
        }

        [Fact]
        public void Setting_Bank_ContentBlock_With_SegmentBankId_0_Is_Not_Allowed_And_Throws_Exception()
        {
            // Arrange
            var mem = new Memory();

            // Act/Assert
            Assert.Throws<ArgumentException>(() => mem.ConfigureMemorySegmentBank(segmentNumber: 1, segmentBankId: 0, new byte[Memory.ADDITIONAL_SEGMENT_SIZE]));
        }

        [Fact]
        public void Setting_Bank_ContentBlock_With_Size_Not_Same_As_BankSize_Is_Not_Allowed_And_Throws_Exception()
        {
            // Arrange
            var mem = new Memory();

            // Act/Assert
            Assert.Throws<ArgumentException>(() => mem.ConfigureMemorySegmentBank(segmentNumber: 1, segmentBankId: 0, new byte[1234]));
        }         

        [Fact]
        public void Setting_Bank_ContentBlock_With_BankId_That_Does_Not_Exists_Is_Not_Allowed_And_Throws_Exception()
        {
            // Arrange
            var mem = new Memory();

            // Act/Assert
            Assert.Throws<ArgumentException>(() => mem.ConfigureMemorySegmentBank(segmentNumber: (byte)mem.MemorySegments.Count, segmentBankId: 1, new byte[Memory.ADDITIONAL_SEGMENT_SIZE]));
        } 

        [Fact]
        public void Can_Switch_Bank_In_Memory_Area()
        {
            // Arrange
            var mem = new Memory();
            mem.ConfigureMemorySegmentBank(segmentNumber: 1, segmentBankId: 1, new byte[Memory.ADDITIONAL_SEGMENT_SIZE]);

            mem[0x2000] = 0x42;
            mem[0x2001] = 0x21;

            // Act
            mem.ChangeSegmentBank(segmentNumber: 1, segmentBankId: 1);

            // Assert
            var data = mem.ReadData(0x2000, 2);
            Assert.Equal(0x00, data[0]);
            Assert.Equal(0x00, data[1]);
        }


        [Fact]
        public void Can_Switch_Bank_And_Back_Again_Retain_Original_Memory()
        {
            // Arrange
            var mem = new Memory();
            mem.ConfigureMemorySegmentBank(segmentNumber: 1, segmentBankId: 1, new byte[Memory.ADDITIONAL_SEGMENT_SIZE]);

            mem[0x2000] = 0x42;
            mem[0x2001] = 0x21;

            // Act
            mem.ChangeSegmentBank(segmentNumber: 1, segmentBankId: 1);
            mem.ChangeSegmentBank(segmentNumber: 1, segmentBankId: 0);

            // Assert
            var data = mem.ReadData(0x2000, 2);
            Assert.Equal(0x42, data[0]);
            Assert.Equal(0x21, data[1]);
        }        

    }
}
