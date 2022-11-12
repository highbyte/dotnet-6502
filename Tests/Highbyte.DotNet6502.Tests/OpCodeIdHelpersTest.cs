using Xunit;
using Highbyte.DotNet6502;
using System.Collections.Generic;

namespace Highbyte.DotNet6502.Tests
{
    public class OpCodeIdHelpersTest
    {
        [Fact]
        public void Contains_Returns_True_If_List_Contains_OpCodeId_That_Is_Defined_In_Enum()
        {
            // Arrange
            List<OpCodeId> opCodeIdList = new List<OpCodeId>
            {
                OpCodeId.BRK,
                OpCodeId.NOP,
                OpCodeId.RTI
            };

            // Act
            bool containsOpCodeId = opCodeIdList.Contains(OpCodeId.NOP.ToByte());

            // Assert
            Assert.True(containsOpCodeId);
        }

        [Fact]
        public void Contains_Returns_False_If_OpCodeIs_Is_Defined_In_Enum_But_Does_Not_Exist_In_List()
        {
            // Arrange
            List<OpCodeId> opCodeIdList = new List<OpCodeId>
            {
                OpCodeId.BRK,
                OpCodeId.NOP,
                OpCodeId.RTI
            };

            // Act
            bool containsOpCodeId = opCodeIdList.Contains(OpCodeId.LDA_ZP.ToByte());

            // Assert
            Assert.False(containsOpCodeId);
        }        

        [Fact]
        public void Contains_Returns_False_If_OpCodeId_Is_Not_Defined_In_Enum()
        {
            // Arrange
            List<OpCodeId> opCodeIdList = new List<OpCodeId>
            {
                OpCodeId.BRK,
                OpCodeId.NOP,
                OpCodeId.RTI
            };

            // Act
            bool containsOpCodeId = opCodeIdList.Contains(0xff);
            
            // Assert
            Assert.False(containsOpCodeId);
        }        
    }
}
