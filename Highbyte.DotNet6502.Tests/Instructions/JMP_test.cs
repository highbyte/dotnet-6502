using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class JMP_test
    {
        const uint memorySize = 1024*64; // 0xffff
        readonly Memory _mem = new(memorySize);

        [Fact]
        public void JMP_ABS_Takes_3_Cycles()
        {
            var test = new TestSpec()
            {
                Instruction    = OpCodeId.JMP_ABS,
                ExpectedCycles = 3,
            };
            test.Execute_And_Verify(AddrMode.ABS);
        }

        [Fact]
        public void JMP_Can_Jump_To_Another_Address()
        {
            // Arrange
            ushort startPos = 0x0020;
            CPU cpu = new();
            cpu.PC = startPos;
            var cpuCopy  = cpu.Clone();

            byte expectedAValue=0x42;
            ushort newPos = 0x0500;

            // Code at start address
            _mem.WriteByte(ref startPos, OpCodeId.JMP_ABS);
            _mem.WriteWord(ref startPos, newPos);

            // Code at jmp address
            _mem.WriteByte(ref newPos, OpCodeId.LDA_I);
            _mem.WriteByte(ref newPos, expectedAValue);

            // Act
            var execOptions = new ExecOptions
            {
                MaxNumberOfInstructions = 2
            };
            cpu.Execute(_mem, execOptions);

            // Assert
            Assert.Equal(expectedAValue, cpu.A);
            Assert.Equal(newPos, cpu.PC);
            Assert.Equal(cpuCopy.SP, cpu.SP);
        }


        [Fact]
        public void JMP_IND_Takes_5_Cycles()
        {
            var test = new TestSpec()
            {
                Instruction    = OpCodeId.JMP_IND,
                ExpectedCycles = 5,
            };
            test.Execute_And_Verify(AddrMode.Indirect);
        }

        /// <summary>
        /// JMP is the only 6502 instruction to support indirection. 
        /// The instruction contains a 16 bit address which identifies the location of the least significant byte of another 
        /// 16 bit memory address which is the real target of the instruction.
        /// </summary>
        [Fact]
        public void JMP_IND_Can_Jump_To_Another_Address_Indirect()
        {
            // Arrange
            ushort startPos = 0x0020;
            CPU cpu = new();
            cpu.PC = startPos;
            var cpuCopy = cpu.Clone();

            ushort newPos = 0x0500;
            ushort indirectAddress = 0x0400;
            byte expectedAValue=0x42;

            // Prepare the indirect address with and address to the final jump location
            _mem.WriteWord(indirectAddress, newPos);

            // Code at start address
            _mem.WriteByte(ref startPos, OpCodeId.JMP_IND);
            _mem.WriteWord(ref startPos, indirectAddress);

            // Code at final jmp address
            _mem.WriteByte(ref newPos, OpCodeId.LDA_I);
            _mem.WriteByte(ref newPos, expectedAValue);

            // Act
            var execOptions = new ExecOptions
            {
                MaxNumberOfInstructions = 2
            };            
            cpu.Execute(_mem, execOptions);

            // Assert
            Assert.Equal(expectedAValue, cpu.A);
            Assert.Equal(newPos, cpu.PC);
            Assert.Equal(cpuCopy.SP, cpu.SP);
        }
    }
}
