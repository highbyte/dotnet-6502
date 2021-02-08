using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class CPU_start_test
    {
        [Fact]
        public void Computer_Can_Be_Reset_And_Start_At_Address_Specified_In_ResetVector()
        {
            var computer = new Computer();
            computer.Mem.WriteWord(CPU.ResetVector, 0xc000);

            computer.Reset();

            Assert.Equal(0xc000, computer.CPU.PC);
            
            // Not sure if the CPU hardware will have SP set to 0xff on power on, or if there is code in the reset vector in ROM that does this.
            // Assert.Equal(0xff, cpu.SP);  
        }
    }
}
