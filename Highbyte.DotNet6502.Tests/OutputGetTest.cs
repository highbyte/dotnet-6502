using Xunit;
using Highbyte.DotNet6502;
using System.Collections.Generic;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class OutputGetTest
    {
        [Fact]
        public void TODO_This_Test_If_For_Temporary_Output_Contains_Address()
        {
            // Arrange
            var cpu = new CPU
            {
            };
            cpu.ExecState.PCBeforeLastOpCodeExecuted = 0x1000;
            cpu.ExecState.LastOpCode = OpCodeId.LDA_ABS.ToByte();

            var mem = new Memory();

            // Act
            var outputString = OutputGen.FormatLastInstruction(cpu, mem);

            // Assert
            Assert.Contains(cpu.ExecState.PCBeforeLastOpCodeExecuted.Value.ToHex(), outputString);
        }

        [Fact]
        public void TODO_This_Test_If_For_Temporary_Output_Contains_Unknown_OpCode()
        {
            // Arrange
            var cpu = new CPU
            {
            };
            cpu.ExecState.PCBeforeLastOpCodeExecuted = 0x1000;
            cpu.ExecState.LastOpCode = 0xff;

            var mem = new Memory();

            // Act
            var outputString = OutputGen.FormatLastInstruction(cpu, mem);

            // Assert
            Assert.Contains(cpu.ExecState.LastOpCode.Value.ToHex(), outputString);
        }        

    }
}
