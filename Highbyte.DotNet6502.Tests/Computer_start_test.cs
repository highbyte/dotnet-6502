using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class Computer_start_test
    {
        [Fact]
        public void Computer_Can_Be_Reset_And_Restart_At_ResetVector()
        {
            // Arrange
            var builder = new ComputerBuilder()
            .WithCPU()
            .WithMemory(1024*64);

            var computer = builder.Build();

            // Act
            computer.Reset();

            // Assert
            Assert.Equal(computer.Mem[CPU.ResetVector], computer.CPU.PC);
            
            // Not sure if the CPU hardware will have SP set to 0xff on power on, or if there is code in the reset vector in ROM that does this.
            // Assert.Equal(0xff, cpu.SP);  
        }
    }
}
