using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class JSR_and_RTS_test
    {
        const uint memorySize = 1024*64; // 0xffff
        readonly Memory _mem = new(memorySize);

        [Fact]
        public void JSR_Takes_3_Cycles()
        {
            var test = new TestSpec()
            {
                OpCode         = OpCodeId.JSR,
                ExpectedCycles = 6,
            };
            test.Execute_And_Verify(AddrMode.ABS);
        }

        [Fact]
        public void JSR_Can_Jump_To_Another_Address()
        {
            // Arrange
            ushort startPos = 0x0020;
            CPU cpu = new();
            cpu.PC = startPos;
            var cpuCopy  = cpu.Clone();

            byte expectedAValue=0x42;
            ushort branchPos = 0x0500;

            // Code at start address
            _mem.WriteByte(ref startPos, OpCodeId.JSR);
            _mem.WriteWord(ref startPos, branchPos);

            // Code at branch jsr address
            _mem.WriteByte(ref branchPos, OpCodeId.LDA_I);
            _mem.WriteByte(ref branchPos, expectedAValue);

            // Act
            var execOptions = new ExecOptions
            {
                MaxNumberOfInstructions = 2
            };            
            cpu.Execute(_mem, execOptions);

            // Assert
            Assert.Equal(expectedAValue, cpu.A);
            Assert.Equal(branchPos, cpu.PC);
            Assert.Equal((byte)(cpuCopy.SP-2), cpu.SP); // We didn't return from the jsr with an rts, so the SP should have used two bytes for the return address
        }


        [Fact]
        public void Can_Jump_To_Another_Address_And_Return_To_Original_And_Continue()
        {
            // Arrange
            var cpu = new CPU();

            byte expectedAValue=0x42;
            ushort branchPos = 0x0500;

            ushort startPos = 0x0020;
            cpu.PC = startPos;

            var cpuCopy  = cpu.Clone();

            // Code at start address
            _mem.WriteByte(ref startPos, OpCodeId.JSR);
            _mem.WriteWord(ref startPos, branchPos);
            _mem.WriteByte(ref startPos, OpCodeId.LDA_I);
            _mem.WriteByte(ref startPos, expectedAValue);

            // Code at branch jsr address
            _mem.WriteByte(ref branchPos, OpCodeId.LDA_I);
            _mem.WriteByte(ref branchPos, (byte) (expectedAValue + 1)); // In subroutine we set A to some other value than we will set later when we return.
            _mem.WriteByte(ref branchPos, OpCodeId.RTS);

            // Act
            var execOptions = new ExecOptions
            {
                MaxNumberOfInstructions = 4 // JSR + LDA_I + RTS + LDA_I
            };            
            cpu.Execute(_mem, execOptions);            

            // Assert
            Assert.Equal(expectedAValue, cpu.A);
            Assert.Equal(startPos, cpu.PC);
            Assert.Equal(cpuCopy.SP, cpu.SP);
        }

        [Fact]
        public void JSR_Pushes_Return_Address_To_Stack_Correctly()
        {
            // Arrange
            ushort startPos = 0xc000;
            CPU cpu = new();
            cpu.PC = startPos;
            var cpuCopy  = cpu.Clone();

            ushort branchPos = 0x0500;

            // Code at start address
            _mem.WriteByte(ref startPos, OpCodeId.JSR);
            _mem.WriteWord(ref startPos, branchPos);

            // Act
            var execOptions = new ExecOptions
            {
                MaxNumberOfInstructions = 1
            };            
            cpu.Execute(_mem, execOptions);

            // Assert
            Assert.Equal((byte)(cpuCopy.SP-2), cpu.SP); // We didn't return from the jsr with an rts, so the SP should have used two bytes for the return address

            ushort expectedReturnAddressPushedToStack = (ushort)(startPos - 1);

            byte expectedReturnAddressLowByte = expectedReturnAddressPushedToStack.Lowbyte();
            byte expectedReturnAddressHighByte = expectedReturnAddressPushedToStack.Highbyte();

            ushort expectedReturnAddressSPLowByteLocation = (ushort) (CPU.StackBaseAddress + (byte)(cpuCopy.SP - 1));
            ushort expectedReturnAddressSPHighByteLocation = (ushort) (CPU.StackBaseAddress + (byte)(cpuCopy.SP));

            Assert.Equal(expectedReturnAddressLowByte, _mem[expectedReturnAddressSPLowByteLocation]);
            Assert.Equal(expectedReturnAddressHighByte, _mem[expectedReturnAddressSPHighByteLocation]);
        }
    }
}
