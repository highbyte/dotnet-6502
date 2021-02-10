namespace Highbyte.DotNet6502
{
    // TODO: Should probably be removed when new model (Instruction, InstructionExecutor) has stabalized.
    /// <summary>
    /// Old implementation of handling instructions.
    /// </summary>
    public partial class CPU
    {

        private bool HandleInstruction(OpCodeId opCodeId, Memory mem)
        {
            byte zeroPageAddress;
            ushort zeroPageAddressX;
            ushort zeroPageAddressY;
            ushort fullAddress;
            ushort fullAddressX;
            ushort fullAddressY;
            ushort indirectIndexedAddress;
            byte memValue;
            sbyte branchOffset;
            byte insValue;
            byte tempValue;
            bool didCrossPageBoundary;
            ProcessorStatus processorStatusCopy;

            bool instructionHandled = true;
            switch(opCodeId)
            {
                // ------------------------
                // LDA
                // ------------------------
                case OpCodeId.LDA_I:
                    A = FetchOperand(mem);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.LDA_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    A = FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.LDA_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    A = FetchByte(mem, zeroPageAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.LDA_ABS:
                    fullAddress = FetchOperandWord(mem);
                    A = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.LDA_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress);
                    A = FetchByte(mem, fullAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.LDA_ABS_Y:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressY = CalcFullAddressY(fullAddress);
                    A = FetchByte(mem, fullAddressY);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.LDA_IX_IND:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress);
                    fullAddress = FetchWord(mem, zeroPageAddressX);
                    A = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;  

                case OpCodeId.LDA_IND_IX:
                    zeroPageAddress = FetchOperand(mem);
                    indirectIndexedAddress = FetchWord(mem, zeroPageAddress);
                    // Note: CalcFullAddressY will check if adding Y to address will cross page boundary. If so, one more cycle is consumed.
                    fullAddress = CalcFullAddressY(indirectIndexedAddress);
                    A = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;


                // ------------------------
                // LDX
                // ------------------------
                case OpCodeId.LDX_I:
                    X = FetchOperand(mem);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(X, ProcessorStatus);
                    break;

                case OpCodeId.LDX_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    X = FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(X, ProcessorStatus);
                    break;

                case OpCodeId.LDX_ZP_Y:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressY = CalcZeroPageAddressY(zeroPageAddress, wrapZeroPage: true);
                    X = FetchByte(mem, zeroPageAddressY);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(X, ProcessorStatus);
                    break;

                case OpCodeId.LDX_ABS:
                    fullAddress = FetchOperandWord(mem);
                    X = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(X, ProcessorStatus);
                    break;

                case OpCodeId.LDX_ABS_Y:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressY = CalcFullAddressY(fullAddress);
                    X = FetchByte(mem, fullAddressY);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(X, ProcessorStatus);
                    break;

                // ------------------------
                // LDY
                // ------------------------
                case OpCodeId.LDY_I:
                    Y = FetchOperand(mem);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(Y, ProcessorStatus);
                    break;

                case OpCodeId.LDY_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    Y = FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(Y, ProcessorStatus);
                    break;

                case OpCodeId.LDY_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    Y = FetchByte(mem, zeroPageAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(Y, ProcessorStatus);
                    break;

                case OpCodeId.LDY_ABS:
                    fullAddress = FetchOperandWord(mem);
                    Y = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(Y, ProcessorStatus);
                    break;

                case OpCodeId.LDY_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress);
                    Y = FetchByte(mem, fullAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(Y, ProcessorStatus);
                    break;

                // ------------------------
                // STA
                // ------------------------
                case OpCodeId.STA_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    StoreByte(A, mem, zeroPageAddress);
                    break;

                case OpCodeId.STA_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    StoreByte(A, mem, zeroPageAddressX);
                    break;

                case OpCodeId.STA_ABS:
                    fullAddress = FetchOperandWord(mem);
                    StoreByte(A, mem, fullAddress);
                    break;

                case OpCodeId.STA_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    // TODO: Why does STA_ABS_X (and not LDA_ABS_X) always take an extra cycle even if final address crosses page boundary? Or wrong in documentation?
                    fullAddressX = CalcFullAddressX(fullAddress, alwaysExtraCycleWhenCrossBoundary: true);
                    StoreByte(A, mem, fullAddressX);
                    break;

                case OpCodeId.STA_ABS_Y:
                    fullAddress = FetchOperandWord(mem);
                    // TODO: Why does STA_ABS_Y (and not LDA_ABS_Y) always take an extra cycle even if final address crosses page boundary? Or wrong in documentation?
                    fullAddressY = CalcFullAddressY(fullAddress, alwaysExtraCycleWhenCrossBoundary: true);
                    StoreByte(A, mem, fullAddressY);
                    break;

                case OpCodeId.STA_IX_IND:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress);
                    fullAddress = FetchWord(mem, zeroPageAddressX);
                    StoreByte(A, mem, fullAddress);
                    break;  

                case OpCodeId.STA_IND_IX:
                    zeroPageAddress = FetchOperand(mem);
                    indirectIndexedAddress = FetchWord(mem, zeroPageAddress);
                    // Note: CalcFullAddressY with alwaysExtraCycleWhenCrossBoundary = true will allways take one extra cycle..
                    // TODO: If this correct? STA_IND_X according to doc always takes 6 cycles, doesn't matter if page boundary is crossed (as opposed to LDA_IND_IX). We always add an extra cycle here.
                    fullAddress = CalcFullAddressY(indirectIndexedAddress, alwaysExtraCycleWhenCrossBoundary: true);
                    StoreByte(A, mem, fullAddress);
                    break;

                // ------------------------
                // STX
                // ------------------------
                case OpCodeId.STX_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    StoreByte(X, mem, zeroPageAddress);
                    break;

                case OpCodeId.STX_ZP_Y:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressY = CalcZeroPageAddressY(zeroPageAddress, wrapZeroPage: true);
                    StoreByte(X, mem, zeroPageAddressY);
                    break;

                case OpCodeId.STX_ABS:
                    fullAddress = FetchOperandWord(mem);
                    StoreByte(X, mem, fullAddress);
                    break;

                // ------------------------
                // STY
                // ------------------------
                case OpCodeId.STY_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    StoreByte(Y, mem, zeroPageAddress);
                    break;

                case OpCodeId.STY_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    StoreByte(Y, mem, zeroPageAddressX);
                    break;

                case OpCodeId.STY_ABS:
                    fullAddress = FetchOperandWord(mem);
                    StoreByte(Y, mem, fullAddress);
                    break;

                // ------------------------
                // INC
                // ------------------------
                case OpCodeId.INC_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    memValue = FetchByte(mem, zeroPageAddress);
                    memValue++;
                    ExecState.CyclesConsumed++;  // Takes extra cycle to add 1?
                    StoreByte(memValue, mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(memValue, ProcessorStatus);
                    break;

                case OpCodeId.INC_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    memValue = FetchByte(mem, zeroPageAddressX);
                    memValue++;
                    ExecState.CyclesConsumed++;  // Takes extra cycle to add 1?
                    StoreByte(memValue, mem, zeroPageAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(memValue, ProcessorStatus);
                    break;

                case OpCodeId.INC_ABS:
                    fullAddress = FetchOperandWord(mem);
                    memValue = FetchByte(mem, fullAddress);
                    memValue++;
                    ExecState.CyclesConsumed++;  // Takes extra cycle to add 1?
                    StoreByte(memValue, mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(memValue, ProcessorStatus);
                    break;

                case OpCodeId.INC_ABS_X:
                    // TODO: Why does INC_ABS_X (and not LDA_ABS_X) always take an extra cycle even if final address crosses page boundary? Or wrong in documentation?
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress, alwaysExtraCycleWhenCrossBoundary: true);
                    memValue = FetchByte(mem, fullAddressX);
                    memValue++;
                    ExecState.CyclesConsumed++;  // Takes extra cycle to add 1?
                    StoreByte(memValue, mem, fullAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(memValue, ProcessorStatus);
                    break;


                // ------------------------
                // INX
                // ------------------------
                case OpCodeId.INX:
                    X++;
                    ExecState.CyclesConsumed++;
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(X, ProcessorStatus);
                    break;

                // ------------------------
                // INY
                // ------------------------
                case OpCodeId.INY:
                    Y++;
                    ExecState.CyclesConsumed++;
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(Y, ProcessorStatus);
                    break;

                // ------------------------
                // DEC
                // ------------------------
                case OpCodeId.DEC_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    memValue = FetchByte(mem, zeroPageAddress);
                    memValue--;
                    ExecState.CyclesConsumed++;  // Takes extra cycle to add 1?
                    StoreByte(memValue, mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(memValue, ProcessorStatus);
                    break;

                case OpCodeId.DEC_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    memValue = FetchByte(mem, zeroPageAddressX);
                    memValue--;
                    ExecState.CyclesConsumed++;  // Takes extra cycle to add 1?
                    StoreByte(memValue, mem, zeroPageAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(memValue, ProcessorStatus);
                    break;

                case OpCodeId.DEC_ABS:
                    fullAddress = FetchOperandWord(mem);
                    memValue = FetchByte(mem, fullAddress);
                    memValue--;
                    ExecState.CyclesConsumed++;  // Takes extra cycle to add 1?
                    StoreByte(memValue, mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(memValue, ProcessorStatus);
                    break;

                case OpCodeId.DEC_ABS_X:
                    // TODO: Why does INC_ABS_X (and not LDA_ABS_X) always take an extra cycle even if final address crosses page boundary? Or wrong in documentation?
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress, alwaysExtraCycleWhenCrossBoundary: true);
                    memValue = FetchByte(mem, fullAddressX);
                    memValue--;
                    ExecState.CyclesConsumed++;  // Takes extra cycle to add 1?
                    StoreByte(memValue, mem, fullAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(memValue, ProcessorStatus);
                    break;


                // ------------------------
                // DEX
                // ------------------------
                case OpCodeId.DEX:
                    X--;
                    ExecState.CyclesConsumed++;
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(X, ProcessorStatus);
                    break;

                // ------------------------
                // DEY
                // ------------------------
                case OpCodeId.DEY:
                    Y--;
                    ExecState.CyclesConsumed++;
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(Y, ProcessorStatus);
                    break;


                // ------------------------
                // JMP
                // ------------------------
                case OpCodeId.JMP_ABS:
                    // Get the address we should jump to (will update PC to point to next instruction)
                    PC = FetchOperandWord(mem);
                    break;

                case OpCodeId.JMP_IND:
                    // Get the address we should look for another address.
                    ushort indrectAddress = FetchOperandWord(mem);
                    // Get actual address
                    fullAddress = FetchWord(mem, indrectAddress);
                    PC = fullAddress;
                    break;


                // ------------------------
                // JSR
                // ------------------------
                case OpCodeId.JSR:
                    // Get the address we should branch to (will update PC to point to next instruction)
                    fullAddress = FetchOperandWord(mem);
                    // The JSR instruction pushes the address of the last byte of the instruction.
                    // As PC now points to the next instruction, we push PC minus one to the stack.
                    PushWordToStack((ushort) (PC-1), mem);
                    // Consume extra cycles to change SP?
                    ExecState.CyclesConsumed++;

                    // Set PC to address we will jump to
                    PC = fullAddress;
                    break;

                // ------------------------
                // RTS
                // ------------------------
                case OpCodeId.RTS:
                    // Set PC back to the returning address from stack.
                    // As the address that was pushed on stack by JSR was the last byte of the JSR instruction, 
                    // we add one byte to the address we read from the stack (to get to next instruction)
                    PC = (ushort) (PopWordFromStack(mem) + 1);
                    // TODO: How may cycles to change SP? This seems odd, not the same as other RTI at also uses PopWordFromStack
                    ExecState.CyclesConsumed +=3;
                    break;

                // ------------------------
                // BEQ
                // ------------------------
                case OpCodeId.BEQ:
                    branchOffset = (sbyte)FetchOperand(mem);
                    if(ProcessorStatus.Zero)
                    {
                        PC = BranchHelper.CalculateNewAbsoluteBranchAddress(PC, branchOffset, out ulong cyclesConsumed);
                        ExecState.CyclesConsumed += cyclesConsumed;
                    }
                    break;

                // ------------------------
                // BNE
                // ------------------------
                case OpCodeId.BNE:
                    branchOffset = (sbyte)FetchOperand(mem);
                    if(!ProcessorStatus.Zero)
                    {
                        PC = BranchHelper.CalculateNewAbsoluteBranchAddress(PC, branchOffset, out ulong cyclesConsumed);
                        ExecState.CyclesConsumed += cyclesConsumed;
                    }
                    break;

                // ------------------------
                // BCC
                // ------------------------
                case OpCodeId.BCC:
                    branchOffset = (sbyte)FetchOperand(mem);
                    if(!ProcessorStatus.Carry)
                    {
                        PC = BranchHelper.CalculateNewAbsoluteBranchAddress(PC, branchOffset, out ulong cyclesConsumed);
                        ExecState.CyclesConsumed += cyclesConsumed;
                    }
                    break;

                // ------------------------
                // BCS
                // ------------------------
                case OpCodeId.BCS:
                    branchOffset = (sbyte)FetchOperand(mem);
                    if(ProcessorStatus.Carry)
                    {
                        PC = BranchHelper.CalculateNewAbsoluteBranchAddress(PC, branchOffset, out ulong cyclesConsumed);
                        ExecState.CyclesConsumed += cyclesConsumed;
                    }
                    break;

                // ------------------------
                // BMI
                // ------------------------
                case OpCodeId.BMI:
                    branchOffset = (sbyte)FetchOperand(mem);
                    if(ProcessorStatus.Negative)
                    {
                        PC = BranchHelper.CalculateNewAbsoluteBranchAddress(PC, branchOffset, out ulong cyclesConsumed);
                        ExecState.CyclesConsumed += cyclesConsumed;
                    }
                    break;

                // ------------------------
                // BPL
                // ------------------------
                case OpCodeId.BPL:
                    branchOffset = (sbyte)FetchOperand(mem);
                    if(!ProcessorStatus.Negative)
                    {
                        PC = BranchHelper.CalculateNewAbsoluteBranchAddress(PC, branchOffset, out ulong cyclesConsumed);
                        ExecState.CyclesConsumed += cyclesConsumed;
                    }
                    break;

                // ------------------------
                // BVC
                // ------------------------
                case OpCodeId.BVC:
                    branchOffset = (sbyte)FetchOperand(mem);
                    if(!ProcessorStatus.Overflow)
                    {
                        PC = BranchHelper.CalculateNewAbsoluteBranchAddress(PC, branchOffset, out ulong cyclesConsumed);
                        ExecState.CyclesConsumed += cyclesConsumed;
                    }
                    break;                        

                // ------------------------
                // BVS
                // ------------------------
                case OpCodeId.BVS:
                    branchOffset = (sbyte)FetchOperand(mem);
                    if(ProcessorStatus.Overflow)
                    {
                        PC = BranchHelper.CalculateNewAbsoluteBranchAddress(PC, branchOffset, out ulong cyclesConsumed);
                        ExecState.CyclesConsumed += cyclesConsumed;
                    }
                    break;  

                // ------------------------
                // ADC
                // ------------------------
                case OpCodeId.ADC_I:
                    insValue = FetchOperand(mem);
                    A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case OpCodeId.ADC_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    insValue = FetchByte(mem, zeroPageAddress);
                    A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case OpCodeId.ADC_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    insValue = FetchByte(mem, zeroPageAddressX);
                    A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case OpCodeId.ADC_ABS:
                    fullAddress = FetchOperandWord(mem);
                    insValue = FetchByte(mem, fullAddress);
                    A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case OpCodeId.ADC_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress, alwaysExtraCycleWhenCrossBoundary: false);
                    insValue = FetchByte(mem, fullAddressX);
                    A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case OpCodeId.ADC_ABS_Y:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressY = CalcFullAddressY(fullAddress, alwaysExtraCycleWhenCrossBoundary: false);
                    insValue = FetchByte(mem, fullAddressY);
                    A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case OpCodeId.ADC_IX_IND:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress);
                    fullAddress = FetchWord(mem, zeroPageAddressX);
                    insValue = FetchByte(mem, fullAddress);
                    A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;  

                case OpCodeId.ADC_IND_IX:
                    zeroPageAddress = FetchOperand(mem);
                    indirectIndexedAddress = FetchWord(mem, zeroPageAddress);
                    // Note: CalcFullAddressY will check if adding Y to address will cross page boundary. If so, one more cycle is consumed.
                    fullAddress = CalcFullAddressY(indirectIndexedAddress);
                    insValue = FetchByte(mem, fullAddress);
                    A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;


                // ------------------------
                // SBC
                // ------------------------
                case OpCodeId.SBC_I:
                    insValue = FetchOperand(mem);
                    A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case OpCodeId.SBC_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    insValue = FetchByte(mem, zeroPageAddress);
                    A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case OpCodeId.SBC_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    insValue = FetchByte(mem, zeroPageAddressX);
                    A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case OpCodeId.SBC_ABS:
                    fullAddress = FetchOperandWord(mem);
                    insValue = FetchByte(mem, fullAddress);
                    A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case OpCodeId.SBC_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress, alwaysExtraCycleWhenCrossBoundary: false);
                    insValue = FetchByte(mem, fullAddressX);
                    A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case OpCodeId.SBC_ABS_Y:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressY = CalcFullAddressY(fullAddress, alwaysExtraCycleWhenCrossBoundary: false);
                    insValue = FetchByte(mem, fullAddressY);
                    A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case OpCodeId.SBC_IX_IND:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress);
                    fullAddress = FetchWord(mem, zeroPageAddressX);
                    insValue = FetchByte(mem, fullAddress);
                    A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;  

                case OpCodeId.SBC_IND_IX:
                    zeroPageAddress = FetchOperand(mem);
                    indirectIndexedAddress = FetchWord(mem, zeroPageAddress);
                    // Note: CalcFullAddressY will check if adding Y to address will cross page boundary. If so, one more cycle is consumed.
                    fullAddress = CalcFullAddressY(indirectIndexedAddress);
                    insValue = FetchByte(mem, fullAddress);
                    A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                // ------------------------
                // AND
                // ------------------------
                case OpCodeId.AND_I:
                    A &= FetchOperand(mem);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.AND_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    A &= FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.AND_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    A &= FetchByte(mem, zeroPageAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.AND_ABS:
                    fullAddress = FetchOperandWord(mem);
                    A &= FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.AND_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress);
                    A &= FetchByte(mem, fullAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.AND_ABS_Y:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressY = CalcFullAddressY(fullAddress);
                    A &= FetchByte(mem, fullAddressY);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.AND_IX_IND:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress);
                    fullAddress = FetchWord(mem, zeroPageAddressX);
                    A &= FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;  

                case OpCodeId.AND_IND_IX:
                    zeroPageAddress = FetchOperand(mem);
                    indirectIndexedAddress = FetchWord(mem, zeroPageAddress);
                    // Note: CalcFullAddressY will check if adding Y to address will cross page boundary. If so, one more cycle is consumed.
                    fullAddress = CalcFullAddressY(indirectIndexedAddress);
                    A &= FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                // ------------------------
                // ORA
                // ------------------------
                case OpCodeId.ORA_I:
                    A |= FetchOperand(mem);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.ORA_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    A |= FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.ORA_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    A |= FetchByte(mem, zeroPageAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.ORA_ABS:
                    fullAddress = FetchOperandWord(mem);
                    A |= FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.ORA_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress);
                    A |= FetchByte(mem, fullAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.ORA_ABS_Y:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressY = CalcFullAddressY(fullAddress);
                    A |= FetchByte(mem, fullAddressY);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.ORA_IX_IND:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress);
                    fullAddress = FetchWord(mem, zeroPageAddressX);
                    A |= FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;  

                case OpCodeId.ORA_IND_IX:
                    zeroPageAddress = FetchOperand(mem);
                    indirectIndexedAddress = FetchWord(mem, zeroPageAddress);
                    // Note: CalcFullAddressY will check if adding Y to address will cross page boundary. If so, one more cycle is consumed.
                    fullAddress = CalcFullAddressY(indirectIndexedAddress);
                    A |= FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                // ------------------------
                // EOR
                // ------------------------
                case OpCodeId.EOR_I:
                    A ^= FetchOperand(mem);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.EOR_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    A ^= FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.EOR_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    A ^= FetchByte(mem, zeroPageAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.EOR_ABS:
                    fullAddress = FetchOperandWord(mem);
                    A ^= FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.EOR_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress);
                    A ^= FetchByte(mem, fullAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.EOR_ABS_Y:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressY = CalcFullAddressY(fullAddress);
                    A ^= FetchByte(mem, fullAddressY);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case OpCodeId.EOR_IX_IND:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress);
                    fullAddress = FetchWord(mem, zeroPageAddressX);
                    A ^= FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;  

                case OpCodeId.EOR_IND_IX:
                    zeroPageAddress = FetchOperand(mem);
                    indirectIndexedAddress = FetchWord(mem, zeroPageAddress);
                    // Note: CalcFullAddressY will check if adding Y to address will cross page boundary. If so, one more cycle is consumed.
                    fullAddress = CalcFullAddressY(indirectIndexedAddress);
                    A ^= FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                // ------------------------
                // CMP
                // ------------------------
                case OpCodeId.CMP_I:
                    tempValue  = FetchOperand(mem);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(A, tempValue, ProcessorStatus);
                    break;

                case OpCodeId.CMP_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    tempValue = FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(A, tempValue, ProcessorStatus);
                    break;

                case OpCodeId.CMP_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    tempValue = FetchByte(mem, zeroPageAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(A, tempValue, ProcessorStatus);
                    break;

                case OpCodeId.CMP_ABS:
                    fullAddress = FetchOperandWord(mem);
                    tempValue = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(A, tempValue, ProcessorStatus);
                    break;

                case OpCodeId.CMP_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress);
                    tempValue = FetchByte(mem, fullAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(A, tempValue, ProcessorStatus);
                    break;

                case OpCodeId.CMP_ABS_Y:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressY = CalcFullAddressY(fullAddress);
                    tempValue = FetchByte(mem, fullAddressY);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(A, tempValue, ProcessorStatus);
                    break;

                case OpCodeId.CMP_IX_IND:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress);
                    fullAddress = FetchWord(mem, zeroPageAddressX);
                    tempValue = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(A, tempValue, ProcessorStatus);
                    break;  

                case OpCodeId.CMP_IND_IX:
                    zeroPageAddress = FetchOperand(mem);
                    indirectIndexedAddress = FetchWord(mem, zeroPageAddress);
                    // Note: CalcFullAddressY will check if adding Y to address will cross page boundary. If so, one more cycle is consumed.
                    fullAddress = CalcFullAddressY(indirectIndexedAddress);
                    tempValue = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(A, tempValue, ProcessorStatus);
                    break;

                // ------------------------
                // CPX
                // ------------------------
                case OpCodeId.CPX_I:
                    tempValue  = FetchOperand(mem);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(X, tempValue, ProcessorStatus);
                    break;

                case OpCodeId.CPX_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    tempValue = FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(X, tempValue, ProcessorStatus);
                    break;

                case OpCodeId.CPX_ABS:
                    fullAddress = FetchOperandWord(mem);
                    tempValue = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(X, tempValue, ProcessorStatus);
                    break;

                // ------------------------
                // CPY
                // ------------------------
                case OpCodeId.CPY_I:
                    tempValue  = FetchOperand(mem);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(Y, tempValue, ProcessorStatus);
                    break;

                case OpCodeId.CPY_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    tempValue = FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(Y, tempValue, ProcessorStatus);
                    break;

                case OpCodeId.CPY_ABS:
                    fullAddress = FetchOperandWord(mem);
                    tempValue = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(Y, tempValue, ProcessorStatus);
                    break;

                // ------------------------
                // ASL
                // ------------------------
                case OpCodeId.ASL_ACC:
                    A = BinaryArithmeticHelpers.PerformASLAndSetStatusRegisters(A, ProcessorStatus);
                    break;

                case OpCodeId.ASL_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    tempValue = FetchByte(mem, zeroPageAddress);
                    tempValue = BinaryArithmeticHelpers.PerformASLAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, zeroPageAddress);
                    break;

                case OpCodeId.ASL_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    tempValue = FetchByte(mem, zeroPageAddressX);
                    tempValue = BinaryArithmeticHelpers.PerformASLAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, zeroPageAddressX);
                    break;

                case OpCodeId.ASL_ABS:
                    fullAddress = FetchOperandWord(mem);
                    tempValue = FetchByte(mem, fullAddress);
                    tempValue = BinaryArithmeticHelpers.PerformASLAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, fullAddress);
                    break;

                case OpCodeId.ASL_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress, out didCrossPageBoundary, false);
                    tempValue = FetchByte(mem, fullAddressX);
                    tempValue = BinaryArithmeticHelpers.PerformASLAndSetStatusRegisters(tempValue, ProcessorStatus);
                    if(!didCrossPageBoundary)
                        // TODO: Is this correCt: Two extra cycles for ASL before writing back to memory if we did NOT cross page boundary?
                        ExecState.CyclesConsumed += 2;
                    else
                        // TODO: Is this correct: Extra cycle if the address + X crosses page boundary (1 extra was already added in CalcFullAddressX)
                        ExecState.CyclesConsumed ++;
                    StoreByte(tempValue, mem, fullAddressX);
                    break;

                // ------------------------
                // LSR
                // ------------------------
                case OpCodeId.LSR_ACC:
                    A = BinaryArithmeticHelpers.PerformLSRAndSetStatusRegisters(A, ProcessorStatus);
                    break;

                case OpCodeId.LSR_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    tempValue = FetchByte(mem, zeroPageAddress);
                    tempValue = BinaryArithmeticHelpers.PerformLSRAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, zeroPageAddress);
                    break;

                case OpCodeId.LSR_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    tempValue = FetchByte(mem, zeroPageAddressX);
                    tempValue = BinaryArithmeticHelpers.PerformLSRAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, zeroPageAddressX);
                    break;

                case OpCodeId.LSR_ABS:
                    fullAddress = FetchOperandWord(mem);
                    tempValue = FetchByte(mem, fullAddress);
                    tempValue = BinaryArithmeticHelpers.PerformLSRAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, fullAddress);
                    break;

                case OpCodeId.LSR_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress, out didCrossPageBoundary, false);
                    tempValue = FetchByte(mem, fullAddressX);
                    tempValue = BinaryArithmeticHelpers.PerformLSRAndSetStatusRegisters(tempValue, ProcessorStatus);
                    if(!didCrossPageBoundary)
                        // TODO: Is this correCt: Two extra cycles for ASL before writing back to memory if we did NOT cross page boundary?
                        ExecState.CyclesConsumed += 2;
                    else
                        // TODO: Is this correct: Extra cycle if the address + X crosses page boundary (1 extra was already added in CalcFullAddressX)
                        ExecState.CyclesConsumed ++;
                    StoreByte(tempValue, mem, fullAddressX);
                    break;

                // ------------------------
                // ROL
                // ------------------------
                case OpCodeId.ROL_ACC:
                    A = BinaryArithmeticHelpers.PerformROLAndSetStatusRegisters(A, ProcessorStatus);
                    break;

                case OpCodeId.ROL_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    tempValue = FetchByte(mem, zeroPageAddress);
                    tempValue = BinaryArithmeticHelpers.PerformROLAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, zeroPageAddress);
                    break;

                case OpCodeId.ROL_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    tempValue = FetchByte(mem, zeroPageAddressX);
                    tempValue = BinaryArithmeticHelpers.PerformROLAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, zeroPageAddressX);
                    break;

                case OpCodeId.ROL_ABS:
                    fullAddress = FetchOperandWord(mem);
                    tempValue = FetchByte(mem, fullAddress);
                    tempValue = BinaryArithmeticHelpers.PerformROLAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, fullAddress);
                    break;

                case OpCodeId.ROL_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress, out didCrossPageBoundary, false);
                    tempValue = FetchByte(mem, fullAddressX);
                    tempValue = BinaryArithmeticHelpers.PerformROLAndSetStatusRegisters(tempValue, ProcessorStatus);
                    if(!didCrossPageBoundary)
                        // TODO: Is this correCt: Two extra cycles for ASL before writing back to memory if we did NOT cross page boundary?
                        ExecState.CyclesConsumed += 2;
                    else
                        // TODO: Is this correct: Extra cycle if the address + X crosses page boundary (1 extra was already added in CalcFullAddressX)
                        ExecState.CyclesConsumed ++;
                    StoreByte(tempValue, mem, fullAddressX);
                    break;

                // ------------------------
                // ROR
                // ------------------------
                case OpCodeId.ROR_ACC:
                    A = BinaryArithmeticHelpers.PerformRORAndSetStatusRegisters(A, ProcessorStatus);
                    break;

                case OpCodeId.ROR_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    tempValue = FetchByte(mem, zeroPageAddress);
                    tempValue = BinaryArithmeticHelpers.PerformRORAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, zeroPageAddress);
                    break;

                case OpCodeId.ROR_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    tempValue = FetchByte(mem, zeroPageAddressX);
                    tempValue = BinaryArithmeticHelpers.PerformRORAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, zeroPageAddressX);
                    break;

                case OpCodeId.ROR_ABS:
                    fullAddress = FetchOperandWord(mem);
                    tempValue = FetchByte(mem, fullAddress);
                    tempValue = BinaryArithmeticHelpers.PerformRORAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, fullAddress);
                    break;

                case OpCodeId.ROR_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress, out didCrossPageBoundary,false);
                    tempValue = FetchByte(mem, fullAddressX);
                    tempValue = BinaryArithmeticHelpers.PerformRORAndSetStatusRegisters(tempValue, ProcessorStatus);
                    if(!didCrossPageBoundary)
                        // TODO: Is this correCt: Two extra cycles for ASL before writing back to memory if we did NOT cross page boundary?
                        ExecState.CyclesConsumed += 2;
                    else
                        // TODO: Is this correct: Extra cycle if the address + X crosses page boundary (1 extra was already added in CalcFullAddressX)
                        ExecState.CyclesConsumed ++;
                    StoreByte(tempValue, mem, fullAddressX);
                    break;

                // ------------------------
                // BIT
                // ------------------------
                case OpCodeId.BIT_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    tempValue = FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.PerformBITAndSetStatusRegisters(A, tempValue, ProcessorStatus);
                    break;

                case OpCodeId.BIT_ABS:
                    fullAddress = FetchOperandWord(mem);
                    tempValue = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.PerformBITAndSetStatusRegisters(A, tempValue, ProcessorStatus);
                    break;

                // ------------------------
                // TAX
                // ------------------------
                case OpCodeId.TAX:
                    X = A;
                    // Extra cycle for transfer register to another register?
                    ExecState.CyclesConsumed++;                        
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(X, ProcessorStatus);
                    break;                        

                // ------------------------
                // TAY
                // ------------------------
                case OpCodeId.TAY:
                    Y = A;
                    // Extra cycle for transfer register to another register?
                    ExecState.CyclesConsumed++;                        
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(Y, ProcessorStatus);
                    break;                        

                // ------------------------
                // TXA
                // ------------------------
                case OpCodeId.TXA:
                    A = X;
                    // Extra cycle for transfer register to another register?
                    ExecState.CyclesConsumed++;                        
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;                        

                // ------------------------
                // TYA
                // ------------------------
                case OpCodeId.TYA:
                    A = Y;
                    // Extra cycle for transfer register to another register?
                    ExecState.CyclesConsumed++;                        
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                // ------------------------
                // TSX
                // ------------------------
                case OpCodeId.TSX:
                    X = SP;
                    // Extra cycle for transfer register to another register?
                    ExecState.CyclesConsumed++;                        
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(X, ProcessorStatus);
                    break;

                // ------------------------
                // TXS
                // ------------------------
                case OpCodeId.TXS:
                    SP = X;
                    // Extra cycle for transfer register to another register?
                    ExecState.CyclesConsumed++;
                    break;

                // ------------------------
                // PHA
                // ------------------------
                case OpCodeId.PHA:
                    PushByteToStack(A, mem);
                    // Consume extra cycles to change SP?
                    ExecState.CyclesConsumed++;
                    break;

                // ------------------------
                // PHP
                // ------------------------
                case OpCodeId.PHP:
                    // Set the Break flag on the copy of the ProcessorStatus that will be stored in stack.
                    processorStatusCopy = ProcessorStatus.Clone();
                    processorStatusCopy.Break = true;
                    processorStatusCopy.Unused = true;
                    PushByteToStack(processorStatusCopy.Value, mem);
                    // Consume extra cycles to change SP?
                    ExecState.CyclesConsumed++;
                    break;

                // ------------------------
                // PLA
                // ------------------------
                case OpCodeId.PLA:
                    A = PopByteFromStack(mem);
                    // Consume two extra cycles to change SP? Why one more than PHA?
                    ExecState.CyclesConsumed += 2;
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                // ------------------------
                // PLP
                // ------------------------
                case OpCodeId.PLP:
                    ProcessorStatus.Value = PopByteFromStack(mem);
                    // Consume two extra cycles to change SP? Why one more than PHP?
                    ExecState.CyclesConsumed += 2;                     
                    break;

                // ------------------------
                // CLC
                // ------------------------
                case OpCodeId.CLC:
                    ProcessorStatus.Carry = false;
                    // Consume extra cycle to clear flag?
                    ExecState.CyclesConsumed ++;
                    break;

                // ------------------------
                // CLD
                // ------------------------
                case OpCodeId.CLD:
                    ProcessorStatus.Decimal = false;
                    // Consume extra cycle to clear flag?
                    ExecState.CyclesConsumed ++;
                    break;

                // ------------------------
                // CLI
                // ------------------------
                case OpCodeId.CLI:
                    ProcessorStatus.InterruptDisable = false;
                    // Consume extra cycle to clear flag?
                    ExecState.CyclesConsumed ++;
                    break;

                // ------------------------
                // CLV
                // ------------------------
                case OpCodeId.CLV:
                    ProcessorStatus.Overflow = false;
                    // Consume extra cycle to clear flag?
                    ExecState.CyclesConsumed ++;
                    break;

                // ------------------------
                // SEC
                // ------------------------
                case OpCodeId.SEC:
                    ProcessorStatus.Carry = true;
                    // Consume extra cycle to set flag?
                    ExecState.CyclesConsumed ++;
                    break;

                // ------------------------
                // SED
                // ------------------------
                case OpCodeId.SED:
                    ProcessorStatus.Decimal = true;
                    // Consume extra cycle to set flag?
                    ExecState.CyclesConsumed ++;
                    break;    

                // ------------------------
                // SEI
                // ------------------------
                case OpCodeId.SEI:
                    ProcessorStatus.InterruptDisable = true;
                    // Consume extra cycle to set flag?
                    ExecState.CyclesConsumed ++;
                    break;                        

                // ------------------------
                // Misc system instructions
                // ------------------------
                case OpCodeId.BRK:
                    // BRK is strange. The complete instruction is only one byte but the processor increases 
                    // the return address pushed to stack is the *second* byte after the opcode!
                    // It is advisable to use a NOP after it to avoid issues (when returning from BRK with RTI, the PC will point to the next-next instruction)
                    ushort pcPushedToStack = PC;
                    pcPushedToStack++;
                    PushWordToStack(pcPushedToStack, mem);
                    // Set the Break flag on the copy of the ProcessorStatus that will be stored in stack.
                    processorStatusCopy = ProcessorStatus.Clone();
                    processorStatusCopy.Break = true;
                    processorStatusCopy.Unused = true;
                    PushByteToStack(processorStatusCopy.Value, mem);
                    // TODO: Consume extra cycles to change SP? Why not as many extra as RTI?
                    ExecState.CyclesConsumed++;
                    // BRK sets current Interrupt flag
                    ProcessorStatus.InterruptDisable = true;
                    // Change PC to address found at BRK/IEQ handler vector
                    PC = FetchWord(mem, CPU.BrkIRQHandlerVector);
                    break;

                case OpCodeId.RTI:
                    ProcessorStatus.Value = PopByteFromStack(mem);
                    ProcessorStatus.Break = false;
                    ProcessorStatus.Unused = false;
                    PC = PopWordFromStack(mem);
                    // Consume two cycles to change SP (?)
                    ExecState.CyclesConsumed += 2;
                    break;                                                            

                case OpCodeId.NOP:
                    // TODO: What is extra cycle for?
                    ExecState.CyclesConsumed++;
                    break;                                                            

                default:
                    instructionHandled = false;
                    break;
            }
            return instructionHandled;
        }
   }
}