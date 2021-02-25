using System;
using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class MemorySegmentBankSwitchingTest
    {
        [Fact]
        public void Changing_SegmentBank_On_Segment_0_Should_Not_Be_Allowed_And_Throws_Exception()
        {
            // Arrange
            var mem = new Memory();

            // Act/Assert
            Assert.Throws<ArgumentException>(() => mem.ChangeCurrentSegmentBank(0,0));
        }

        [Fact]
        public void Changing_A_Memory_SegmentNumber_That_Does_Not_Exist_Should_Not_Be_Allowed_And_Throws_Exception()
        {
            // Arrange
            var mem = new Memory();

            // Act/Assert
            Assert.Throws<ArgumentException>(() => mem.ChangeCurrentSegmentBank((byte)(mem.MemorySegments.Count + 1),0));
        }

        [Fact]
        public void Adding_MemorySegmentBank_With_SegmentBankNumber_0_Is_Not_Allowed_And_Throws_Exception()
        {
            // Arrange
            var mem = new Memory();

            // Act/Assert
            Assert.Throws<ArgumentException>(() => mem.AddMemorySegmentBank(segmentNumber: 0, new byte[Memory.ADDITIONAL_SEGMENT_SIZE]));
        }

        [Fact]
        public void Adding_MemorySegmentBank_With_Size_Not_Same_As_Segment_Size_Is_Not_Allowed_And_Throws_Exception()
        {
            // Arrange
            var mem = new Memory();

            // Act/Assert
            Assert.Throws<ArgumentException>(() => mem.AddMemorySegmentBank(segmentNumber: 1, new byte[1234]));
        }         

        [Fact]
        public void Adding_MemorySegmentBank_With_SegmentNumber_That_Does_Not_Exists_Is_Not_Allowed_And_Throws_Exception()
        {
            // Arrange
            var mem = new Memory();

            // Act/Assert
            Assert.Throws<ArgumentException>(() => mem.AddMemorySegmentBank(segmentNumber: (byte)mem.MemorySegments.Count, new byte[Memory.ADDITIONAL_SEGMENT_SIZE]));
        } 

        [Fact]
        public void Can_Switch_SegmentBank()
        {
            // Arrange
            var mem = new Memory();
            mem.AddMemorySegmentBank(segmentNumber: 1, new byte[Memory.ADDITIONAL_SEGMENT_SIZE]);

            mem[0x2000] = 0x42;
            mem[0x2001] = 0x21;

            // Act
            mem.ChangeCurrentSegmentBank(segmentNumber: 1, segmentBankNumber: 1);

            // Assert
            var data = mem.ReadData(0x2000, 2);
            Assert.Equal(0x00, data[0]);
            Assert.Equal(0x00, data[1]);
        }


        [Fact]
        public void Can_Switch_SegmentBank_And_Back_Again_Retain_Original_Memory()
        {
            // Arrange
            var mem = new Memory();
            mem.AddMemorySegmentBank(segmentNumber: 1, new byte[Memory.ADDITIONAL_SEGMENT_SIZE]);

            mem[0x2000] = 0x42;
            mem[0x2001] = 0x21;

            // Act
            mem.ChangeCurrentSegmentBank(segmentNumber: 1, segmentBankNumber: 1);
            mem.ChangeCurrentSegmentBank(segmentNumber: 1, segmentBankNumber: 0);

            // Assert
            var data = mem.ReadData(0x2000, 2);
            Assert.Equal(0x42, data[0]);
            Assert.Equal(0x21, data[1]);
        }

        [Fact]
        public void Can_Switching_SegmentBank_In_One_Segment_Does_Not_Affect_Other_Segments()
        {
            // Arrange
            var mem = new Memory();
            mem.AddMemorySegmentBank(segmentNumber: 1, new byte[Memory.ADDITIONAL_SEGMENT_SIZE]);

            mem[0x2000] = 0x42;
            mem[0x2001] = 0x21;

            // This memory (in segment number 2) should not be effected by changing bank in segment 1
            mem[0x4000] = 0x20;
            mem[0x4001] = 0x40;

            // Act
            mem.ChangeCurrentSegmentBank(segmentNumber: 1, segmentBankNumber: 1);

            // Assert
            var data = mem.ReadData(0x4000, 2);
            Assert.Equal(0x20, data[0]);
            Assert.Equal(0x40, data[1]);
        }          
    }
}
