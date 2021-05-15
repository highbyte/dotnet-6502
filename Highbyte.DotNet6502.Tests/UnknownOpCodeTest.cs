using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class UnknownOpCodeTest
    {
        [Fact]
        public void CPU_Can_Detect_Unknown_OpCode()
        {
            // Arrange
            var cpu = new CPU();
            var mem = new Memory();

            mem[0x1000] = 0x02; // OpCode that does not exist
            cpu.PC = 0x1000;
            
            // Act
            var execState = cpu.Execute(mem, new ExecOptions{MaxNumberOfInstructions=1, UnknownInstructionThrowsException = false});

            // Assert
            Assert.False(execState.LastOpCodeWasHandled);
            Assert.Equal((ulong)1, (ulong)execState.UnknownOpCodeCount);
        }
    }
}
