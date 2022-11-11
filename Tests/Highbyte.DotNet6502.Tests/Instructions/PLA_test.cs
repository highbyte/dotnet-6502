using Xunit;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public class PLA_test
    {
        [Fact]
        public void PLA_Takes_4_Cycles()
        {
            var test = new TestSpec()
            {
                OpCode         = OpCodeId.PLA,
                ExpectedCycles = 4,
            };
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void PLA_Pops_A_From_Stack()
        {

            var test = new TestSpec()
            {
                SP             = 0xfe,
                A              = 0x33,
                OpCode         = OpCodeId.PLA,
                ExpectedA      = 0x12,
                ExpectedSP     = 0xff,
            };

            // Prepare a value on the stack (one address higher than the current SP we use in test)
            // Remember that stack works downwards (0xff-0x00), points to the next free location, and is located at address 0x0100 + SP
            ushort stackPointerFullAddress = CPU.StackBaseAddress + 0xff;
            test.TestContext.Mem[stackPointerFullAddress] = 0x12;

            test.Execute_And_Verify(AddrMode.Implied);

        }

        [Fact]
        public void PLA_Sets_Zero_Flag_If_Result_Is_Zero()
        {
            var test = new TestSpec()
            {
                SP             = 0xfe,
                A              = 0x34,
                OpCode         = OpCodeId.PLA,
                ExpectedZ      = true,
                ExpectedA      = 0x00,
            };
            ushort stackPointerFullAddress = CPU.StackBaseAddress + 0xff;
            test.TestContext.Mem[stackPointerFullAddress] = 0x00;
            test.Execute_And_Verify(AddrMode.Implied);        }

        [Fact]
        public void PLA_Clears_Zero_Flag_If_Result_Is_Not_Zero()
        {
            var test = new TestSpec()
            {
                SP             = 0xfe,
                A              = 0x34,
                OpCode         = OpCodeId.PLA,
                ExpectedZ      = false,
                ExpectedA      = 0x01,
            };
            ushort stackPointerFullAddress = CPU.StackBaseAddress + 0xff;
            test.TestContext.Mem[stackPointerFullAddress] = 0x01;
            test.Execute_And_Verify(AddrMode.Implied);        
        }

        [Fact]
        public void PLA_Sets_Negative_Flag_If_Result_Is_Negative()
        {
            var test = new TestSpec()
            {
                SP             = 0xfe,
                A              = 0x34,
                OpCode         = OpCodeId.PLA,
                ExpectedN      = true,
                ExpectedA      = 0xfe,
            };
            ushort stackPointerFullAddress = CPU.StackBaseAddress + 0xff;
            test.TestContext.Mem[stackPointerFullAddress] = 0xfe;
            test.Execute_And_Verify(AddrMode.Implied);
        }

        [Fact]
        public void PLA_Clears_Negative_Flag_If_Result_Is_Positive()
        {
            var test = new TestSpec()
            {
                SP             = 0xfe,
                A              = 0x34,
                OpCode         = OpCodeId.PLA,
                ExpectedN      = false,
                ExpectedA      = 0x70,
            };
            ushort stackPointerFullAddress = CPU.StackBaseAddress + 0xff;
            test.TestContext.Mem[stackPointerFullAddress] = 0x70;
            test.Execute_And_Verify(AddrMode.Implied);
        }        
    }
}
