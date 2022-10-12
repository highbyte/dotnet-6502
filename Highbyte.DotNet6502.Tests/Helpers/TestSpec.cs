using Xunit;
using Highbyte.DotNet6502.Tests.Helpers;

namespace Highbyte.DotNet6502.Tests.Instructions
{
    public enum InstrEffect {Reg, Mem, RegAndMem, StatusOnly, StackPointerOnly, None}
    public class TestSpec
    {
        // Initial Program Counter address (where the specified Instruction will execute).
        // If not specified, a default address will be used
        public ushort? PC { get; set; }

        // Initial register values
        public byte? A { get; set; }
        public byte? X { get; set; }
        public byte? Y { get; set; }
        public byte? SP { get; set; }

        // Initial flag values
        // The entire processor status byte
        public byte? PS
        {
            get
            {
                byte status = 0x00;
                status.ChangeBit(StatusFlagBits.Carry, C.HasValue && C.Value);
                status.ChangeBit(StatusFlagBits.Zero, Z.HasValue && Z.Value);
                status.ChangeBit(StatusFlagBits.InterruptDisable, I.HasValue && I.Value);
                status.ChangeBit(StatusFlagBits.Decimal, D.HasValue && D.Value);
                status.ChangeBit(StatusFlagBits.Break, B.HasValue && B.Value);
                status.ChangeBit(StatusFlagBits.Unused, U.HasValue && U.Value);
                status.ChangeBit(StatusFlagBits.Overflow, V.HasValue && V.Value);
                status.ChangeBit(StatusFlagBits.Negative, N.HasValue && N.Value);
                return status;
            }
            set
            {
                C = value.HasValue && value.Value.IsBitSet(StatusFlagBits.Carry);
                Z = value.HasValue && value.Value.IsBitSet(StatusFlagBits.Zero);
                I = value.HasValue && value.Value.IsBitSet(StatusFlagBits.InterruptDisable);
                D = value.HasValue && value.Value.IsBitSet(StatusFlagBits.Decimal);
                B = value.HasValue && value.Value.IsBitSet(StatusFlagBits.Break);
                U = value.HasValue && value.Value.IsBitSet(StatusFlagBits.Unused);
                V = value.HasValue && value.Value.IsBitSet(StatusFlagBits.Overflow);
                N = value.HasValue && value.Value.IsBitSet(StatusFlagBits.Negative);
            }
        }   
        public bool? C { get; set; }
        public bool? Z { get; set; }
        public bool? I { get; set; }
        public bool? D { get; set; }
        public bool? B { get; set; }    
        public bool? U { get; set; } // Unused/un-documented bit
        public bool? V { get; set; }
        public bool? N { get; set; }

        /// <summary>
        /// Required.
        /// The CPU instruction value.
        /// </summary>
        /// <value></value>
        public OpCodeId OpCode { get; set; }

        /// <summary>
        /// Optional.
        /// By default, InsEffect is assumed Reg if not set.
        /// It is used to verify that other TestSpec settings makes sense.
        /// Example:
        ///   If TestSpec.InsEffect is set to InstrEffect.Mem, 
        ///   and TestSpec.ExpectA/X/Y are set, then there is something wrong with the test case, and an exception is thrown.
        /// </summary>
        /// <value></value>
        public InstrEffect? InsEffect { get; set; }

        /// <summary>
        /// Optional.
        /// - Not used in Immediate or Accumulator mode. Optional in other modes.
        /// - The final value used by the instruction, in whatever addressing mode it got to it.
        /// - For Immediate addressing mode, this is the actual operand value after the instruction.
        /// - For other addressing modes, the actual operand value after the instruction is automatically generated by the code, so the final value used by the instruction will be this value.
        /// </summary>
        /// <value></value>
        public byte? FinalValue { get; set; }


        /// <summary>
        /// Optional.
        /// Only used by Indirect addressing mode (JMP is the only instruction that uses it.)
        /// 
        /// </summary>
        /// <value></value>
        public ushort? FinalValueWord { get; set; }

        /// <summary>
        /// Optional, can be used with addressing modes that uses ZeroPage.
        /// If not set, a ZeroPage address will be selected automatically.
        /// </summary>
        /// <value></value>
        public ushort? ZeroPageAddress  { get; set; }

        /// <summary>
        /// Optional, can be used with addressing modes that uses Absolute addressing, or a relative addressing that will generate a full (non-zeropage) address.
        /// If not set, a full address will be selected automatically.
        /// </summary>
        /// <value></value>
        public ushort? FullAddress  { get; set; }

        public ulong? ExpectedCycles { get; set; }

        // Expected Program Counter after instruction has executed.
        // If not specified, it verifies that the PC has increased as many bytes the instruction took (instruction + optional operand).
        public ushort? ExpectedPC { get; set; }

        // Expected register values
        public byte? ExpectedA { get; set; }
        public byte? ExpectedX { get; set; }
        public byte? ExpectedY { get; set; }
        public byte? ExpectedSP { get; set; }

        // Expected processor status flag values
        public byte? ExpectedPS
        {
            get
            {
                byte status = 0x00;
                status.ChangeBit(StatusFlagBits.Carry, ExpectedC.HasValue && ExpectedC.Value);
                status.ChangeBit(StatusFlagBits.Zero, ExpectedZ.HasValue && ExpectedZ.Value);
                status.ChangeBit(StatusFlagBits.InterruptDisable, ExpectedI.HasValue && ExpectedI.Value);
                status.ChangeBit(StatusFlagBits.Decimal, ExpectedD.HasValue && ExpectedD.Value);
                status.ChangeBit(StatusFlagBits.Break, ExpectedB.HasValue && ExpectedB.Value);
                status.ChangeBit(StatusFlagBits.Unused, ExpectedU.HasValue && ExpectedU.Value);
                status.ChangeBit(StatusFlagBits.Overflow, ExpectedV.HasValue && ExpectedV.Value);
                status.ChangeBit(StatusFlagBits.Negative, ExpectedN.HasValue && ExpectedN.Value);
                return status;
            }
            set
            {
                ExpectedC = value.HasValue && value.Value.IsBitSet(StatusFlagBits.Carry);
                ExpectedZ = value.HasValue && value.Value.IsBitSet(StatusFlagBits.Zero);
                ExpectedI = value.HasValue && value.Value.IsBitSet(StatusFlagBits.InterruptDisable);
                ExpectedD = value.HasValue && value.Value.IsBitSet(StatusFlagBits.Decimal);
                ExpectedB = value.HasValue && value.Value.IsBitSet(StatusFlagBits.Break);
                ExpectedU = value.HasValue && value.Value.IsBitSet(StatusFlagBits.Unused);
                ExpectedV = value.HasValue && value.Value.IsBitSet(StatusFlagBits.Overflow);
                ExpectedN = value.HasValue && value.Value.IsBitSet(StatusFlagBits.Negative);
            }

        }
        public bool? ExpectedC { get; set; }
        public bool? ExpectedZ { get; set; }
        public bool? ExpectedI { get; set; }
        public bool? ExpectedD { get; set; }
        public bool? ExpectedB { get; set; }
        public bool? ExpectedU { get; set; }    // Unused/un-documented bit
        public bool? ExpectedN { get; set; }
        public bool? ExpectedV { get; set; }

        // For instructions that write/change memory, set this to compare the contents of the memory that was changed (final location, depending on addressing mode)
        public byte? ExpectedMemVal { get; set; }

        /// <summary>
        /// TestContext contains the CPU and Memory objects used during test.
        /// Can be inspected for further verification by caller (after Execute_And_Verify has been run)
        /// </summary>
        /// <value></value>
        public TestContext TestContext {get; private set;}

        public TestSpec()
        {
            TestContext = TestContext.NewTestContext();
        }

        public void Execute_And_Verify(
            AddrMode addrMode
            , bool ZP_X_Should_Wrap_Over_Byte = false
            , bool ZP_Y_Should_Wrap_Over_Byte = false
            , bool FullAddress_Should_Cross_Page_Boundary = false
            )
        {
            if(!InsEffect.HasValue)
                InsEffect = InstrEffect.Reg;

            if(addrMode == AddrMode.Accumulator && FinalValue.HasValue)
                throw new DotNet6502Exception($"If {nameof(AddrMode)} is {nameof(AddrMode.Accumulator)}, {nameof(FinalValue)} cannot be used.");

            if(addrMode == AddrMode.Implied && FinalValue.HasValue)
                throw new DotNet6502Exception($"If {nameof(addrMode)} is {nameof(AddrMode.Implied)}, {nameof(FinalValue)} cannot be used.");

            if(addrMode != AddrMode.Indirect && FinalValueWord.HasValue)
                throw new DotNet6502Exception($"If {nameof(AddrMode)} is other than {nameof(AddrMode.Indirect)}, {nameof(FinalValueWord)} cannot be used.");

            if(InsEffect == InstrEffect.Reg)
            {
                if(ExpectedMemVal.HasValue)
                    throw new DotNet6502Exception($"If {nameof(InsEffect)} is {nameof(InstrEffect.Reg)}, {nameof(ExpectedMemVal)} is not supposed to be set (only used for comparing memory address changed by write)");
                if(addrMode == AddrMode.Accumulator && (ExpectedX.HasValue ||ExpectedY.HasValue ))
                    throw new DotNet6502Exception($"If {nameof(addrMode)} is {nameof(AddrMode.Accumulator)}, {nameof(ExpectedX)} or {nameof(ExpectedY)} cannot be used, because addressing mode {nameof(AddrMode.Accumulator)} only can affect A register.");
                if(ExpectedA.HasValue && !A.HasValue)
                    throw new DotNet6502Exception($"If {nameof(ExpectedA)} is set, {nameof(A)} must be set to an initial value");
                if(ExpectedX.HasValue && !X.HasValue)
                    throw new DotNet6502Exception($"If {nameof(ExpectedX)} is set, {nameof(X)} must be set to an initial value");
                if(ExpectedY.HasValue && !Y.HasValue)
                    throw new DotNet6502Exception($"If {nameof(ExpectedY)} is set, {nameof(Y)} must be set to an initial value");
            }
            else if (InsEffect == InstrEffect.Mem)
            {
                if(addrMode == AddrMode.Implied)
                    throw new DotNet6502Exception($"If {nameof(InsEffect)} is {nameof(InstrEffect.Mem)}, {nameof(addrMode)} {nameof(AddrMode.Implied)} cannot be used.");

                if(addrMode == AddrMode.Accumulator)
                    throw new DotNet6502Exception($"If {nameof(InsEffect)} is {nameof(InstrEffect.Mem)}, {nameof(addrMode)} {nameof(AddrMode.Accumulator)} cannot be used.");

                if(ExpectedMemVal.HasValue && addrMode == AddrMode.I)
                    throw new DotNet6502Exception($"{nameof(ExpectedMemVal)} cannot be used with addressing mode {nameof(AddrMode.I)}");

                if(ExpectedMemVal.HasValue && addrMode == AddrMode.Accumulator)
                    throw new DotNet6502Exception($"{nameof(ExpectedMemVal)} cannot be used with addressing mode {nameof(AddrMode.Accumulator)}");

                if(ExpectedMemVal.HasValue & !FinalValue.HasValue)
                    throw new DotNet6502Exception($"If {nameof(ExpectedMemVal)} is set, {nameof(FinalValue)} must also be set to an initial value that the memory will have before the instruction executes");

                if(ExpectedA.HasValue)
                    throw new DotNet6502Exception($"If {nameof(InsEffect)} is {nameof(InstrEffect.Mem)}, {nameof(ExpectedA)} is not supposed to be set (only used for comparing register change after instruction executed)");
                if(ExpectedX.HasValue)
                    throw new DotNet6502Exception($"If {nameof(InsEffect)} is {nameof(InstrEffect.Mem)}, {nameof(ExpectedX)} is not supposed to be set (only used for comparing register change after instruction executed)");
                if(ExpectedY.HasValue)
                    throw new DotNet6502Exception($"If {nameof(InsEffect)} is {nameof(InstrEffect.Mem)}, {nameof(ExpectedY)} is not supposed to be set (only used for comparing register change after instruction executed)");
            }
            else if (InsEffect == InstrEffect.RegAndMem)
            {
                if(addrMode == AddrMode.Implied)
                    throw new DotNet6502Exception($"If {nameof(InsEffect)} is {nameof(InstrEffect.RegAndMem)}, {nameof(addrMode)} {nameof(AddrMode.Implied)} cannot be used.");
            }

            // If processor flags aren't configured to be initialized by test, we automatically set them to the opposite of what was defined as the expected result
            if(ExpectedC.HasValue && !C.HasValue)
                C = ! ExpectedC.Value;
            if(ExpectedZ.HasValue && !Z.HasValue)
                Z = ! ExpectedZ.Value;
            if(ExpectedI.HasValue && !I.HasValue)
                I = ! ExpectedI.Value;
            if(ExpectedD.HasValue && !D.HasValue)
                D = ! ExpectedZ.Value;
            if(ExpectedB.HasValue && !B.HasValue)
                B = ! ExpectedB.Value;
            if(ExpectedU.HasValue && !U.HasValue)
                U = ! ExpectedU.Value;
            if(ExpectedV.HasValue && !V.HasValue)
                V = ! ExpectedV.Value;
            if(ExpectedN.HasValue && !N.HasValue)
                N = ! ExpectedN.Value;

            // Shorthand variables to cpu and memory
            var computer = TestContext.Computer;
            var cpu = computer.CPU;
            var mem = computer.Mem;

            // Init Program Counter
            if(PC.HasValue)
                cpu.PC = PC.Value;

            // We will start writing the instruction, and then operand, starting at the specified PC address
            var codeMemPos = cpu.PC;

            // Write instruction at start address
            mem.WriteByte(ref codeMemPos, OpCode);

            ushort ZPAddressX;
            ushort ZPAddressY;
            ushort fullAddressX;
            ushort fullAddressY;
            ushort? finalAddressUsed = null;
            switch(addrMode)
            {
                case AddrMode.Implied:
                    // Implied instructions does not have operand. The complete instruction takes 1 byte.
                    break;

                case AddrMode.Accumulator:
                    // Accumulator addressing mode means operate on A register (instead of memory). No instruction operand is used.
                    break;

                case AddrMode.Indirect:
                    // JMP is the only instruction that uses Indirect addressing.
                    // The instruction contains a 16 bit address which identifies the location of the least significant byte 
                    // of another 16 bit memory address which is the real target of the instruction.

                    // Define memory address the instruction should use
                    if(!FullAddress.HasValue)
                        FullAddress = 0xab12;

                    finalAddressUsed = FullAddress.Value;

                    // Initialize memory the instruction should read or write to
                    if(!FinalValueWord.HasValue)
                        FinalValueWord = 0x1265; // If not specified, initialize default value to be at final memory location
                    mem.WriteWord(finalAddressUsed.Value, FinalValueWord.Value); 

                    // Write instruction operand
                    mem.WriteWord(ref codeMemPos, FullAddress.Value);

                    break;

                case AddrMode.Relative:
                    // Relative addressing mode is used by branching instruction such as BEQ and BNE.
                    if(!FinalValue.HasValue)
                        FinalValue = 0xd0; // If not specified, initialize default value to be at final relative memory location to branch to
                    mem.WriteByte(ref codeMemPos, FinalValue.Value);
                    break;

                case AddrMode.I:
                    // Initialize memory the instruction should read or write to
                    if(!FinalValue.HasValue)
                        FinalValue = 0x12; // If not specified, initialize default value to be at final memory location
                    mem.WriteByte(ref codeMemPos, FinalValue.Value);
                    break;

                case AddrMode.ZP:
                    // To avoid testing mistakes, setting up a test as ZP (parameter AddrMode), when the instruction being run (property Instruction) is ZP_X or ZP_Y, 
                    // we initialize registers that should not be used with ZP to non-default values. 
                    if(!X.HasValue)
                        X = 111;
                    if(!Y.HasValue)
                        Y = 15;

                    // Define memory address the instruction should calculate
                    if(!ZeroPageAddress.HasValue)
                        ZeroPageAddress = 0x0010;

                    finalAddressUsed = ZeroPageAddress.Value;

                    if(!FinalValue.HasValue)
                        FinalValue = 0x12; // If not specified, initialize default value to be at final memory location
                    // Initialize memory the instruction should read or write to
                    mem.WriteByte(finalAddressUsed.Value, FinalValue.Value);

                    // Write instruction operand
                    mem.WriteByte(ref codeMemPos, ZeroPageAddress.Value.Lowbyte()); // Only least significant byte of the address is used in the instruction.
                    break;
                    
                case AddrMode.ZP_X:
                    // Define memory address the instruction should calculate
                    if(!ZeroPageAddress.HasValue)
                        ZeroPageAddress = 0x0010;
                    // Use a default value for X index if not specified in test
                    if(!X.HasValue)
                    {
                        if(!ZP_X_Should_Wrap_Over_Byte)
                            X = 0x05;
                        else
                            X = 0xf5;   // Force final ZP+X address bigger than one byte (0x0010 + 0xf5 = 0x0105)
                    }
                    // Calculate ZeroPage + X address
                    ZPAddressX = (ushort)(ZeroPageAddress + X);
                    // Adjust that we expect the final address to wrap when getting larger than a byte
                    if(ZP_X_Should_Wrap_Over_Byte)
                        ZPAddressX =  (ushort)(ZPAddressX & 0xff);

                    finalAddressUsed = ZPAddressX;

                    // Initialize memory the instruction should read or write to
                    if(!FinalValue.HasValue)
                        FinalValue = 0x12; // If not specified, initialize default value to be at final memory location
                    mem.WriteByte(finalAddressUsed.Value, FinalValue.Value);

                    // Write instruction operand
                    mem.WriteByte(ref codeMemPos, ZeroPageAddress.Value.Lowbyte()); // Only least significant byte of the address is used in the instruction.
                    break;

                case AddrMode.ZP_Y:
                    // Define memory address the instruction should calculate
                    if(!ZeroPageAddress.HasValue)
                        ZeroPageAddress = 0x0010;
                    // Use a default value for Y index if not specified in test
                    if(!Y.HasValue)
                    {
                        if(!ZP_Y_Should_Wrap_Over_Byte)
                            Y = 0x05;
                        else
                            Y = 0xf5;   // Force final ZP+Y address bigger than one byte (0x0010 + 0xf5 = 0x0105)
                    }
                    // Calculate ZeroPage + Y address
                    ZPAddressY = (ushort)(ZeroPageAddress + Y);
                    // Adjust that we expect the final address to wrap when getting larger than a byte
                    if(ZP_Y_Should_Wrap_Over_Byte)
                        ZPAddressY =  (ushort)(ZPAddressY & 0xff);

                    finalAddressUsed = ZPAddressY;

                    // Initialize memory the instruction should read or write to
                    if(!FinalValue.HasValue)
                        FinalValue = 0x12; // If not specified, initialize default value to be at final memory location
                    mem.WriteByte(finalAddressUsed.Value, FinalValue.Value);

                    // Write instruction operand
                    mem.WriteByte(ref codeMemPos, ZeroPageAddress.Value.Lowbyte()); // Only least significant byte of the address is used in the instruction.
                    break;

                case AddrMode.ABS:
                    // To avoid testing mistakes, setting up a test as ABS (parameter AddrMode), when the instruction being run (property Instruction) is ABX_X or ABS_Y, 
                    // we initialize registers that should not be used with Absolute addressing to non-default values. 
                    if(!X.HasValue)
                        X = 60;
                    if(!Y.HasValue)
                        Y = 100;

                    // Define memory address the instruction should use
                    if(!FullAddress.HasValue)
                        FullAddress = 0xab12;

                    finalAddressUsed = FullAddress.Value;

                    // Initialize memory the instruction should read or write to
                    if(!FinalValue.HasValue)
                        FinalValue = 0x12; // If not specified, initialize default value to be at final memory location
                    mem.WriteByte(finalAddressUsed.Value, FinalValue.Value); 

                    // Write instruction operand
                    mem.WriteWord(ref codeMemPos, FullAddress.Value);
                    break;
                    
                case AddrMode.ABS_X:

                    // Define memory address the instruction should use
                    if(!FullAddress.HasValue)
                        FullAddress = 0xab12;
                    // Use a default value for X index if not specified in test
                    if(!X.HasValue)
                    {
                        if(!FullAddress_Should_Cross_Page_Boundary)
                            X = 0x05;
                        else
                            X = 0xf0;   // Force final FullAddress+X address crosses page boundary (0xab12 + 0xf0 = 0xac02)
                    }
                    // Calculate final address with X offset
                    fullAddressX = (ushort) (FullAddress + X);

                    finalAddressUsed = fullAddressX;

                    // Initialize memory the instruction should read or write to
                    if(!FinalValue.HasValue)
                        FinalValue = 0x12; // If not specified, initialize default value to be at final memory location
                    mem.WriteByte(finalAddressUsed.Value, FinalValue.Value); 

                    // Write instruction operand
                    mem.WriteWord(ref codeMemPos, FullAddress.Value);

                    break;

                case AddrMode.ABS_Y:

                    // Define memory address the instruction should use
                    if(!FullAddress.HasValue)
                        FullAddress = 0xab12;
                    // Use a default value for X index if not specified in test
                    if(!Y.HasValue)
                    {
                        if(!FullAddress_Should_Cross_Page_Boundary)
                            Y = 0x05;
                        else
                            Y = 0xf0;   // Force final FullAddress+X address crosses page boundary (0xab12 + 0xf0 = 0xac02)
                    }
                    // Calculate final address with X offset
                    fullAddressY = (ushort) (FullAddress + Y);

                    finalAddressUsed = fullAddressY;

                    // Initialize memory the instruction should read or write to
                    if(!FinalValue.HasValue)
                        FinalValue = 0x12; // If not specified, initialize default value to be at final memory location
                    mem.WriteByte(finalAddressUsed.Value, FinalValue.Value); 

                    // Write instruction operand
                    mem.WriteWord(ref codeMemPos, FullAddress.Value);

                    break;                    
                    
                case AddrMode.IX_IND:
                    // Define memory address the instruction should use
                    if(!FullAddress.HasValue)
                        FullAddress = 0xab12;
                    // Use a default value for X index if not specified in test
                    if(!X.HasValue)
                    {
                        if(!ZP_X_Should_Wrap_Over_Byte)
                            X = 0x05;
                        else
                            X = 0xf0;   // Force final ZeroPage address +X to wrap around a byte (0x20 + 0xf0 = 0x10)
                    }

                    //Calculate locations used to calculate the actual address to read from (fullAddress)
                    // index_indirect_base_address = zero page address
                    if(!ZeroPageAddress.HasValue)
                        ZeroPageAddress  = 0x20;
                    // We should allways wrap around indirect ZeroPage address + X if exceeds one byte. Can be be done by truncating address to byte.
                    ushort index_indirect_zeropage_address = (byte)(ZeroPageAddress + X);

                    // Initialize zero page address + X the instruction will read final address from
                    mem.WriteWord(index_indirect_zeropage_address, FullAddress.Value); 

                    finalAddressUsed = FullAddress.Value;

                    // Initialize final memory the instruction should read or write to
                    if(!FinalValue.HasValue)
                        FinalValue = 0x12; // If not specified, initialize default value to be at final memory location
                    mem.WriteByte(finalAddressUsed.Value, FinalValue.Value);

                    // Write instruction operand
                    mem.WriteByte(ref codeMemPos, (byte)ZeroPageAddress.Value);
                    break;

                case AddrMode.IND_IX:
                    // Initialize memory we will read from
                    // We calculate a full address to store in a zero page address, adjusted by -Y (because the instruction will add Y when it retrieves it)
                    // If we want to force crossing page boundary FullAddress + Y, we will hard code FullAddress and Y values
                    if(FullAddress_Should_Cross_Page_Boundary)
                    {
                        // The address to be found in zero page address
                        FullAddress = 0xab12;
                        // Adding Y to the address found in zero page will cross page boundary
                        Y = 0xff;
                    }
                    // Use a default value for FullAddress if not specified in test
                    if(!FullAddress.HasValue)
                        FullAddress = 0xab12;
                    // Use a default value for Y index if not specified in test
                    if(!Y.HasValue)
                    {
                        Y = 0x05;
                    }

                    //Calculate locations used to calculate the actual address to read from (fullAddress)
                    // We use ZeroPage address as the "indirect indexed" zeropage address
                    if(!ZeroPageAddress.HasValue)
                        ZeroPageAddress = 0x86;
                    // The address to be found in the zero page address should be the final address - the Y register.
                    ushort address_at_zeropage_address = (ushort) (FullAddress - Y);

                    // Initialize indirect indexed zero page address
                    mem.WriteWord(ZeroPageAddress.Value, address_at_zeropage_address);

                    finalAddressUsed = FullAddress.Value;

                    // Initialize final memory the instruction should read or write to
                    if(!FinalValue.HasValue)
                        FinalValue = 0x12; // If not specified, initialize default value to be at final memory location
                    mem.WriteByte(finalAddressUsed.Value, FinalValue.Value);

                    // Write instruction operand
                    mem.WriteByte(ref codeMemPos, (byte)ZeroPageAddress.Value);
                    break;

                default:
                    throw new DotNet6502Exception($"Unhandled addressing mode: {addrMode}");
            }

            // Before we execute intruction, verify internal consistency of number of bytes the OpCode takes by our test-setup code above
            // with the value specified in OpCode.Bytes class.
            Assert.Equal(cpu.PC + cpu.InstructionList.GetOpCode(OpCode.ToByte()).Size , codeMemPos);

            // Init registers and flags
            if(A.HasValue)
                cpu.A = A.Value;
            if(X.HasValue)
                cpu.X = X.Value;
            if(Y.HasValue)
                cpu.Y = Y.Value;
            if(SP.HasValue)
                cpu.SP = SP.Value;
            if(C.HasValue)
                cpu.ProcessorStatus.Carry = C.Value;
            if(Z.HasValue)
                cpu.ProcessorStatus.Zero = Z.Value;
            if(I.HasValue)
                cpu.ProcessorStatus.InterruptDisable = I.Value;
            if(D.HasValue)
                cpu.ProcessorStatus.Decimal = D.Value;
            if(B.HasValue)
                cpu.ProcessorStatus.Break = B.Value;
            if(U.HasValue)
                cpu.ProcessorStatus.Unused = U.Value;
            if(V.HasValue)
                cpu.ProcessorStatus.Overflow = V.Value;
            if(N.HasValue)
                cpu.ProcessorStatus.Negative = N.Value;

            // Act
            var execOptions = new ExecOptions();
            if (ExpectedCycles.HasValue)
            {
                execOptions.CyclesRequested = ExpectedCycles.Value;
                execOptions.MaxNumberOfInstructions = 1;
            }
            else
            {
                execOptions.CyclesRequested = null;
                execOptions.MaxNumberOfInstructions = 1;
            }
            var thisExecState = cpu.Execute(mem, new LegacyExecEvaluator(execOptions));

            // Assert
            // Check that we didn't find any unknown opcode
            Assert.True(thisExecState.UnknownOpCodeCount == 0);

            // Verify Program Counter
            if(ExpectedPC.HasValue)
                Assert.Equal(ExpectedPC.Value, cpu.PC);
            else
            {
                // If no PC check has been defined, by default verify PC has been moved forward as many bytes we used up for the instruction.
                // But skip default verification if be used a branching or jump instruction
                //      -- All that uses Relative addressing mode
                //      -- Some other instructions in Implied addressing mode
                //      -- JMP instructions
                //      -- JSR instruction
                //
                if(addrMode != AddrMode.Relative
                    && OpCode != OpCodeId.BRK     // Implied addressing mode
                    && OpCode != OpCodeId.RTS     // Implied addressing mode
                    && OpCode != OpCodeId.RTI     // Implied addressing mode
                    && OpCode != OpCodeId.JMP_IND // Indirect addressing mode
                    && OpCode != OpCodeId.JMP_ABS // Absolute addressing mode
                    && OpCode != OpCodeId.JSR     // Absolute addressing mode
                )
                    Assert.Equal(codeMemPos, cpu.PC);
            }

            // Verify expected # of cycles
            if (ExpectedCycles.HasValue)
                Assert.Equal(ExpectedCycles, thisExecState.CyclesConsumed);

            // Verify registers (operations that affects registers)
            if(InsEffect == InstrEffect.Reg || InsEffect == InstrEffect.RegAndMem)
            {
                if (ExpectedA.HasValue)
                    Assert.Equal(ExpectedA.Value, cpu.A);
                if (ExpectedX.HasValue)
                    Assert.Equal(ExpectedX.Value, cpu.X);
                if (ExpectedY.HasValue)
                    Assert.Equal(ExpectedY.Value, cpu.Y);
            }
            // Verify affected memory (operations that affect memory)
            else if (InsEffect == InstrEffect.Mem || InsEffect == InstrEffect.RegAndMem)
            {
                if(!finalAddressUsed.HasValue)
                    throw new DotNet6502Exception($"Incorrect use of TestSpec class, or bug in TestSpec class. Is correct addressing mode being tested? Variable {nameof(finalAddressUsed)} must be initialized in all addressing mode code paths that can modify memory");
                if(ExpectedMemVal.HasValue)
                    Assert.Equal(ExpectedMemVal.Value, mem[finalAddressUsed.Value]);
            }

            // Verify stack pointer
            if (ExpectedSP.HasValue)
                Assert.Equal(ExpectedSP.Value, cpu.SP);

            // Verify status flags
            if (ExpectedC.HasValue)
                Assert.Equal(ExpectedC.Value, cpu.ProcessorStatus.Carry);
            if (ExpectedZ.HasValue)
                Assert.Equal(ExpectedZ.Value, cpu.ProcessorStatus.Zero);
            if (ExpectedI.HasValue)
                Assert.Equal(ExpectedI.Value, cpu.ProcessorStatus.InterruptDisable);
            if (ExpectedD.HasValue)
                Assert.Equal(ExpectedD.Value, cpu.ProcessorStatus.Decimal);
            if (ExpectedB.HasValue)
                Assert.Equal(ExpectedB.Value, cpu.ProcessorStatus.Break);
            if (ExpectedV.HasValue)
                Assert.Equal(ExpectedV.Value, cpu.ProcessorStatus.Overflow);
            if (ExpectedN.HasValue)
                Assert.Equal(ExpectedN.Value, cpu.ProcessorStatus.Negative);
        }
    }
}
