using System;
using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class MemorySegmentTest
    {

        [Fact]
        public void Initializing_Memory_With_Defaults_Should_Create_Appropriate_Number_Of_Segments()
        {
            // Arrange
            var mem = new Memory();
            
            // Act / Assert
            int requiredSegments = 1;   // Segment 0
            int additionalSegments = (int)((Memory.MAX_MEMORY_SIZE - Memory.SEGMENT_0_SIZE) / Memory.ADDITIONAL_SEGMENT_SIZE);
            Assert.Equal(requiredSegments + additionalSegments, mem.MemorySegments.Count);
        }


        [Fact]
        public void Creating_Memory_With_Only_The_Required_Segment_0_Should_Work()
        {
            // Arrange
            var mem = new Memory(Memory.SEGMENT_0_SIZE);

            // Act/Assert
            Assert.Single(mem.MemorySegments);
            Assert.Equal(Memory.SEGMENT_0_SIZE, mem.Size);
        }

        [Fact]
        public void Creating_Less_Memory_Than_Required_For_Segment_0_Should_Throw_Exception()
        {
            // Arrange // Act // Assert
            Assert.Throws<ArgumentException>(() => new Memory(Memory.SEGMENT_0_SIZE - 1));
        }

        [Fact]
        public void Creating_More_Memory_Than_Required_Segment_0_But_With_Size_Not_Evenly_Divisible_By_SegmentSize_Should_Throw_Exception()
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
        public void Creating_More_Memory_Than_Required_Segment_0_With_Size_Evenly_Divisible_By_SegmentSize_Should_Throw_Exception()
        {
            // Arrange
            var mem = new Memory(Memory.SEGMENT_0_SIZE + (1 * Memory.ADDITIONAL_SEGMENT_SIZE));

            // Act/Assert
            Assert.Equal(2, mem.MemorySegments.Count);
            Assert.Equal(Memory.SEGMENT_0_SIZE + Memory.ADDITIONAL_SEGMENT_SIZE, mem.Size);
        }          
    }
}
