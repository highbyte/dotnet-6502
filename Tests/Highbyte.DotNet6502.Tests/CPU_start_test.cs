using Xunit;

namespace Highbyte.DotNet6502.Tests
{
    public class CPU_start_test
    {
        [Fact]
        public void CPU_Can_Be_Reset_And_Start_At_Address_Specified_In_ResetVector()
        {
            var cpu = new CPU();
            var mem = new Memory();
            mem.WriteWord(CPU.ResetVector, 0xc000);
            cpu.Reset(mem);
            Assert.Equal(0xc000, cpu.PC);

            // Not sure if the CPU hardware will have SP set to 0xff on power on, or if there is code in the reset vector in ROM that does this.
            // Assert.Equal(0xff, cpu.SP);  
        }
    }
}
