namespace Highbyte.DotNet6502
{
    // TODO: Should probably be removed when new model (Instruction, InstructionExecutor) has stabalized.
    /// <summary>
    /// Old implementation of handling instructions.
    /// </summary>
    public partial class CPU
    {

        private bool HandleInstruction(Ins ins, Memory mem)
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
            switch(ins)
            {
                // ------------------------
                // LDA
                // ------------------------
                case Ins.LDA_I:
                    A = FetchOperand(mem);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.LDA_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    A = FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.LDA_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    A = FetchByte(mem, zeroPageAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.LDA_ABS:
                    fullAddress = FetchOperandWord(mem);
                    A = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.LDA_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress);
                    A = FetchByte(mem, fullAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.LDA_ABS_Y:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressY = CalcFullAddressY(fullAddress);
                    A = FetchByte(mem, fullAddressY);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.LDA_IX_IND:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress);
                    fullAddress = FetchWord(mem, zeroPageAddressX);
                    A = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;  

                case Ins.LDA_IND_IX:
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
                case Ins.LDX_I:
                    X = FetchOperand(mem);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(X, ProcessorStatus);
                    break;

                case Ins.LDX_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    X = FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(X, ProcessorStatus);
                    break;

                case Ins.LDX_ZP_Y:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressY = CalcZeroPageAddressY(zeroPageAddress, wrapZeroPage: true);
                    X = FetchByte(mem, zeroPageAddressY);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(X, ProcessorStatus);
                    break;

                case Ins.LDX_ABS:
                    fullAddress = FetchOperandWord(mem);
                    X = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(X, ProcessorStatus);
                    break;

                case Ins.LDX_ABS_Y:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressY = CalcFullAddressY(fullAddress);
                    X = FetchByte(mem, fullAddressY);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(X, ProcessorStatus);
                    break;

                // ------------------------
                // LDY
                // ------------------------
                case Ins.LDY_I:
                    Y = FetchOperand(mem);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(Y, ProcessorStatus);
                    break;

                case Ins.LDY_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    Y = FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(Y, ProcessorStatus);
                    break;

                case Ins.LDY_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    Y = FetchByte(mem, zeroPageAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(Y, ProcessorStatus);
                    break;

                case Ins.LDY_ABS:
                    fullAddress = FetchOperandWord(mem);
                    Y = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(Y, ProcessorStatus);
                    break;

                case Ins.LDY_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress);
                    Y = FetchByte(mem, fullAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(Y, ProcessorStatus);
                    break;

                // ------------------------
                // STA
                // ------------------------
                case Ins.STA_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    StoreByte(A, mem, zeroPageAddress);
                    break;

                case Ins.STA_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    StoreByte(A, mem, zeroPageAddressX);
                    break;

                case Ins.STA_ABS:
                    fullAddress = FetchOperandWord(mem);
                    StoreByte(A, mem, fullAddress);
                    break;

                case Ins.STA_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    // TODO: Why does STA_ABS_X (and not LDA_ABS_X) always take an extra cycle even if final address crosses page boundary? Or wrong in documentation?
                    fullAddressX = CalcFullAddressX(fullAddress, alwaysExtraCycleWhenCrossBoundary: true);
                    StoreByte(A, mem, fullAddressX);
                    break;

                case Ins.STA_ABS_Y:
                    fullAddress = FetchOperandWord(mem);
                    // TODO: Why does STA_ABS_Y (and not LDA_ABS_Y) always take an extra cycle even if final address crosses page boundary? Or wrong in documentation?
                    fullAddressY = CalcFullAddressY(fullAddress, alwaysExtraCycleWhenCrossBoundary: true);
                    StoreByte(A, mem, fullAddressY);
                    break;

                case Ins.STA_IX_IND:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress);
                    fullAddress = FetchWord(mem, zeroPageAddressX);
                    StoreByte(A, mem, fullAddress);
                    break;  

                case Ins.STA_IND_IX:
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
                case Ins.STX_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    StoreByte(X, mem, zeroPageAddress);
                    break;

                case Ins.STX_ZP_Y:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressY = CalcZeroPageAddressY(zeroPageAddress, wrapZeroPage: true);
                    StoreByte(X, mem, zeroPageAddressY);
                    break;

                case Ins.STX_ABS:
                    fullAddress = FetchOperandWord(mem);
                    StoreByte(X, mem, fullAddress);
                    break;

                // ------------------------
                // STY
                // ------------------------
                case Ins.STY_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    StoreByte(Y, mem, zeroPageAddress);
                    break;

                case Ins.STY_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    StoreByte(Y, mem, zeroPageAddressX);
                    break;

                case Ins.STY_ABS:
                    fullAddress = FetchOperandWord(mem);
                    StoreByte(Y, mem, fullAddress);
                    break;

                // ------------------------
                // INC
                // ------------------------
                case Ins.INC_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    memValue = FetchByte(mem, zeroPageAddress);
                    memValue++;
                    ExecState.CyclesConsumed++;  // Takes extra cycle to add 1?
                    StoreByte(memValue, mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(memValue, ProcessorStatus);
                    break;

                case Ins.INC_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    memValue = FetchByte(mem, zeroPageAddressX);
                    memValue++;
                    ExecState.CyclesConsumed++;  // Takes extra cycle to add 1?
                    StoreByte(memValue, mem, zeroPageAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(memValue, ProcessorStatus);
                    break;

                case Ins.INC_ABS:
                    fullAddress = FetchOperandWord(mem);
                    memValue = FetchByte(mem, fullAddress);
                    memValue++;
                    ExecState.CyclesConsumed++;  // Takes extra cycle to add 1?
                    StoreByte(memValue, mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(memValue, ProcessorStatus);
                    break;

                case Ins.INC_ABS_X:
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
                case Ins.INX:
                    X++;
                    ExecState.CyclesConsumed++;
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(X, ProcessorStatus);
                    break;

                // ------------------------
                // INY
                // ------------------------
                case Ins.INY:
                    Y++;
                    ExecState.CyclesConsumed++;
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(Y, ProcessorStatus);
                    break;

                // ------------------------
                // DEC
                // ------------------------
                case Ins.DEC_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    memValue = FetchByte(mem, zeroPageAddress);
                    memValue--;
                    ExecState.CyclesConsumed++;  // Takes extra cycle to add 1?
                    StoreByte(memValue, mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(memValue, ProcessorStatus);
                    break;

                case Ins.DEC_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    memValue = FetchByte(mem, zeroPageAddressX);
                    memValue--;
                    ExecState.CyclesConsumed++;  // Takes extra cycle to add 1?
                    StoreByte(memValue, mem, zeroPageAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(memValue, ProcessorStatus);
                    break;

                case Ins.DEC_ABS:
                    fullAddress = FetchOperandWord(mem);
                    memValue = FetchByte(mem, fullAddress);
                    memValue--;
                    ExecState.CyclesConsumed++;  // Takes extra cycle to add 1?
                    StoreByte(memValue, mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(memValue, ProcessorStatus);
                    break;

                case Ins.DEC_ABS_X:
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
                case Ins.DEX:
                    X--;
                    ExecState.CyclesConsumed++;
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(X, ProcessorStatus);
                    break;

                // ------------------------
                // DEY
                // ------------------------
                case Ins.DEY:
                    Y--;
                    ExecState.CyclesConsumed++;
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(Y, ProcessorStatus);
                    break;


                // ------------------------
                // JMP
                // ------------------------
                case Ins.JMP_ABS:
                    // Get the address we should jump to (will update PC to point to next instruction)
                    PC = FetchOperandWord(mem);
                    break;

                case Ins.JMP_IND:
                    // Get the address we should look for another address.
                    ushort indrectAddress = FetchOperandWord(mem);
                    // Get actual address
                    fullAddress = FetchWord(mem, indrectAddress);
                    PC = fullAddress;
                    break;


                // ------------------------
                // JSR
                // ------------------------
                case Ins.JSR:
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
                case Ins.RTS:
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
                case Ins.BEQ:
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
                case Ins.BNE:
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
                case Ins.BCC:
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
                case Ins.BCS:
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
                case Ins.BMI:
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
                case Ins.BPL:
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
                case Ins.BVC:
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
                case Ins.BVS:
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
                case Ins.ADC_I:
                    insValue = FetchOperand(mem);
                    A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case Ins.ADC_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    insValue = FetchByte(mem, zeroPageAddress);
                    A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case Ins.ADC_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    insValue = FetchByte(mem, zeroPageAddressX);
                    A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case Ins.ADC_ABS:
                    fullAddress = FetchOperandWord(mem);
                    insValue = FetchByte(mem, fullAddress);
                    A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case Ins.ADC_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress, alwaysExtraCycleWhenCrossBoundary: false);
                    insValue = FetchByte(mem, fullAddressX);
                    A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case Ins.ADC_ABS_Y:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressY = CalcFullAddressY(fullAddress, alwaysExtraCycleWhenCrossBoundary: false);
                    insValue = FetchByte(mem, fullAddressY);
                    A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case Ins.ADC_IX_IND:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress);
                    fullAddress = FetchWord(mem, zeroPageAddressX);
                    insValue = FetchByte(mem, fullAddress);
                    A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;  

                case Ins.ADC_IND_IX:
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
                case Ins.SBC_I:
                    insValue = FetchOperand(mem);
                    A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case Ins.SBC_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    insValue = FetchByte(mem, zeroPageAddress);
                    A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case Ins.SBC_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    insValue = FetchByte(mem, zeroPageAddressX);
                    A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case Ins.SBC_ABS:
                    fullAddress = FetchOperandWord(mem);
                    insValue = FetchByte(mem, fullAddress);
                    A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case Ins.SBC_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress, alwaysExtraCycleWhenCrossBoundary: false);
                    insValue = FetchByte(mem, fullAddressX);
                    A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case Ins.SBC_ABS_Y:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressY = CalcFullAddressY(fullAddress, alwaysExtraCycleWhenCrossBoundary: false);
                    insValue = FetchByte(mem, fullAddressY);
                    A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;

                case Ins.SBC_IX_IND:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress);
                    fullAddress = FetchWord(mem, zeroPageAddressX);
                    insValue = FetchByte(mem, fullAddress);
                    A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(A, insValue, ProcessorStatus);
                    break;  

                case Ins.SBC_IND_IX:
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
                case Ins.AND_I:
                    A &= FetchOperand(mem);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.AND_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    A &= FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.AND_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    A &= FetchByte(mem, zeroPageAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.AND_ABS:
                    fullAddress = FetchOperandWord(mem);
                    A &= FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.AND_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress);
                    A &= FetchByte(mem, fullAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.AND_ABS_Y:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressY = CalcFullAddressY(fullAddress);
                    A &= FetchByte(mem, fullAddressY);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.AND_IX_IND:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress);
                    fullAddress = FetchWord(mem, zeroPageAddressX);
                    A &= FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;  

                case Ins.AND_IND_IX:
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
                case Ins.ORA_I:
                    A |= FetchOperand(mem);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.ORA_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    A |= FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.ORA_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    A |= FetchByte(mem, zeroPageAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.ORA_ABS:
                    fullAddress = FetchOperandWord(mem);
                    A |= FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.ORA_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress);
                    A |= FetchByte(mem, fullAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.ORA_ABS_Y:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressY = CalcFullAddressY(fullAddress);
                    A |= FetchByte(mem, fullAddressY);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.ORA_IX_IND:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress);
                    fullAddress = FetchWord(mem, zeroPageAddressX);
                    A |= FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;  

                case Ins.ORA_IND_IX:
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
                case Ins.EOR_I:
                    A ^= FetchOperand(mem);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.EOR_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    A ^= FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.EOR_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    A ^= FetchByte(mem, zeroPageAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.EOR_ABS:
                    fullAddress = FetchOperandWord(mem);
                    A ^= FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.EOR_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress);
                    A ^= FetchByte(mem, fullAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.EOR_ABS_Y:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressY = CalcFullAddressY(fullAddress);
                    A ^= FetchByte(mem, fullAddressY);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                case Ins.EOR_IX_IND:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress);
                    fullAddress = FetchWord(mem, zeroPageAddressX);
                    A ^= FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;  

                case Ins.EOR_IND_IX:
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
                case Ins.CMP_I:
                    tempValue  = FetchOperand(mem);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(A, tempValue, ProcessorStatus);
                    break;

                case Ins.CMP_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    tempValue = FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(A, tempValue, ProcessorStatus);
                    break;

                case Ins.CMP_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    tempValue = FetchByte(mem, zeroPageAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(A, tempValue, ProcessorStatus);
                    break;

                case Ins.CMP_ABS:
                    fullAddress = FetchOperandWord(mem);
                    tempValue = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(A, tempValue, ProcessorStatus);
                    break;

                case Ins.CMP_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress);
                    tempValue = FetchByte(mem, fullAddressX);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(A, tempValue, ProcessorStatus);
                    break;

                case Ins.CMP_ABS_Y:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressY = CalcFullAddressY(fullAddress);
                    tempValue = FetchByte(mem, fullAddressY);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(A, tempValue, ProcessorStatus);
                    break;

                case Ins.CMP_IX_IND:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress);
                    fullAddress = FetchWord(mem, zeroPageAddressX);
                    tempValue = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(A, tempValue, ProcessorStatus);
                    break;  

                case Ins.CMP_IND_IX:
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
                case Ins.CPX_I:
                    tempValue  = FetchOperand(mem);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(X, tempValue, ProcessorStatus);
                    break;

                case Ins.CPX_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    tempValue = FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(X, tempValue, ProcessorStatus);
                    break;

                case Ins.CPX_ABS:
                    fullAddress = FetchOperandWord(mem);
                    tempValue = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(X, tempValue, ProcessorStatus);
                    break;

                // ------------------------
                // CPY
                // ------------------------
                case Ins.CPY_I:
                    tempValue  = FetchOperand(mem);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(Y, tempValue, ProcessorStatus);
                    break;

                case Ins.CPY_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    tempValue = FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(Y, tempValue, ProcessorStatus);
                    break;

                case Ins.CPY_ABS:
                    fullAddress = FetchOperandWord(mem);
                    tempValue = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.SetFlagsAfterCompare(Y, tempValue, ProcessorStatus);
                    break;

                // ------------------------
                // ASL
                // ------------------------
                case Ins.ASL_ACC:
                    A = BinaryArithmeticHelpers.PerformASLAndSetStatusRegisters(A, ProcessorStatus);
                    break;

                case Ins.ASL_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    tempValue = FetchByte(mem, zeroPageAddress);
                    tempValue = BinaryArithmeticHelpers.PerformASLAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, zeroPageAddress);
                    break;

                case Ins.ASL_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    tempValue = FetchByte(mem, zeroPageAddressX);
                    tempValue = BinaryArithmeticHelpers.PerformASLAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, zeroPageAddressX);
                    break;

                case Ins.ASL_ABS:
                    fullAddress = FetchOperandWord(mem);
                    tempValue = FetchByte(mem, fullAddress);
                    tempValue = BinaryArithmeticHelpers.PerformASLAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, fullAddress);
                    break;

                case Ins.ASL_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress, out didCrossPageBoundary);
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
                case Ins.LSR_ACC:
                    A = BinaryArithmeticHelpers.PerformLSRAndSetStatusRegisters(A, ProcessorStatus);
                    break;

                case Ins.LSR_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    tempValue = FetchByte(mem, zeroPageAddress);
                    tempValue = BinaryArithmeticHelpers.PerformLSRAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, zeroPageAddress);
                    break;

                case Ins.LSR_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    tempValue = FetchByte(mem, zeroPageAddressX);
                    tempValue = BinaryArithmeticHelpers.PerformLSRAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, zeroPageAddressX);
                    break;

                case Ins.LSR_ABS:
                    fullAddress = FetchOperandWord(mem);
                    tempValue = FetchByte(mem, fullAddress);
                    tempValue = BinaryArithmeticHelpers.PerformLSRAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, fullAddress);
                    break;

                case Ins.LSR_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress, out didCrossPageBoundary);
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
                case Ins.ROL_ACC:
                    A = BinaryArithmeticHelpers.PerformROLAndSetStatusRegisters(A, ProcessorStatus);
                    break;

                case Ins.ROL_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    tempValue = FetchByte(mem, zeroPageAddress);
                    tempValue = BinaryArithmeticHelpers.PerformROLAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, zeroPageAddress);
                    break;

                case Ins.ROL_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    tempValue = FetchByte(mem, zeroPageAddressX);
                    tempValue = BinaryArithmeticHelpers.PerformROLAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, zeroPageAddressX);
                    break;

                case Ins.ROL_ABS:
                    fullAddress = FetchOperandWord(mem);
                    tempValue = FetchByte(mem, fullAddress);
                    tempValue = BinaryArithmeticHelpers.PerformROLAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, fullAddress);
                    break;

                case Ins.ROL_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress, out didCrossPageBoundary);
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
                case Ins.ROR_ACC:
                    A = BinaryArithmeticHelpers.PerformRORAndSetStatusRegisters(A, ProcessorStatus);
                    break;

                case Ins.ROR_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    tempValue = FetchByte(mem, zeroPageAddress);
                    tempValue = BinaryArithmeticHelpers.PerformRORAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, zeroPageAddress);
                    break;

                case Ins.ROR_ZP_X:
                    zeroPageAddress = FetchOperand(mem);
                    zeroPageAddressX = CalcZeroPageAddressX(zeroPageAddress, wrapZeroPage: true);
                    tempValue = FetchByte(mem, zeroPageAddressX);
                    tempValue = BinaryArithmeticHelpers.PerformRORAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, zeroPageAddressX);
                    break;

                case Ins.ROR_ABS:
                    fullAddress = FetchOperandWord(mem);
                    tempValue = FetchByte(mem, fullAddress);
                    tempValue = BinaryArithmeticHelpers.PerformRORAndSetStatusRegisters(tempValue, ProcessorStatus);
                    // Extra cycle for ASL? before writing back to memory?
                    ExecState.CyclesConsumed++;
                    StoreByte(tempValue, mem, fullAddress);
                    break;

                case Ins.ROR_ABS_X:
                    fullAddress = FetchOperandWord(mem);
                    fullAddressX = CalcFullAddressX(fullAddress, out didCrossPageBoundary);
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
                case Ins.BIT_ZP:
                    zeroPageAddress = FetchOperand(mem);
                    tempValue = FetchByte(mem, zeroPageAddress);
                    BinaryArithmeticHelpers.PerformBITAndSetStatusRegisters(A, tempValue, ProcessorStatus);
                    break;

                case Ins.BIT_ABS:
                    fullAddress = FetchOperandWord(mem);
                    tempValue = FetchByte(mem, fullAddress);
                    BinaryArithmeticHelpers.PerformBITAndSetStatusRegisters(A, tempValue, ProcessorStatus);
                    break;

                // ------------------------
                // TAX
                // ------------------------
                case Ins.TAX:
                    X = A;
                    // Extra cycle for transfer register to another register?
                    ExecState.CyclesConsumed++;                        
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(X, ProcessorStatus);
                    break;                        

                // ------------------------
                // TAY
                // ------------------------
                case Ins.TAY:
                    Y = A;
                    // Extra cycle for transfer register to another register?
                    ExecState.CyclesConsumed++;                        
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(Y, ProcessorStatus);
                    break;                        

                // ------------------------
                // TXA
                // ------------------------
                case Ins.TXA:
                    A = X;
                    // Extra cycle for transfer register to another register?
                    ExecState.CyclesConsumed++;                        
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;                        

                // ------------------------
                // TYA
                // ------------------------
                case Ins.TYA:
                    A = Y;
                    // Extra cycle for transfer register to another register?
                    ExecState.CyclesConsumed++;                        
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                // ------------------------
                // TSX
                // ------------------------
                case Ins.TSX:
                    X = SP;
                    // Extra cycle for transfer register to another register?
                    ExecState.CyclesConsumed++;                        
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(X, ProcessorStatus);
                    break;

                // ------------------------
                // TXS
                // ------------------------
                case Ins.TXS:
                    SP = X;
                    // Extra cycle for transfer register to another register?
                    ExecState.CyclesConsumed++;
                    break;

                // ------------------------
                // PHA
                // ------------------------
                case Ins.PHA:
                    PushByteToStack(A, mem);
                    // Consume extra cycles to change SP?
                    ExecState.CyclesConsumed++;
                    break;

                // ------------------------
                // PHP
                // ------------------------
                case Ins.PHP:
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
                case Ins.PLA:
                    A = PopByteFromStack(mem);
                    // Consume two extra cycles to change SP? Why one more than PHA?
                    ExecState.CyclesConsumed += 2;
                    BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(A, ProcessorStatus);
                    break;

                // ------------------------
                // PLP
                // ------------------------
                case Ins.PLP:
                    ProcessorStatus.Value = PopByteFromStack(mem);
                    // Consume two extra cycles to change SP? Why one more than PHP?
                    ExecState.CyclesConsumed += 2;                     
                    break;

                // ------------------------
                // CLC
                // ------------------------
                case Ins.CLC:
                    ProcessorStatus.Carry = false;
                    // Consume extra cycle to clear flag?
                    ExecState.CyclesConsumed ++;
                    break;

                // ------------------------
                // CLD
                // ------------------------
                case Ins.CLD:
                    ProcessorStatus.Decimal = false;
                    // Consume extra cycle to clear flag?
                    ExecState.CyclesConsumed ++;
                    break;

                // ------------------------
                // CLI
                // ------------------------
                case Ins.CLI:
                    ProcessorStatus.InterruptDisable = false;
                    // Consume extra cycle to clear flag?
                    ExecState.CyclesConsumed ++;
                    break;

                // ------------------------
                // CLV
                // ------------------------
                case Ins.CLV:
                    ProcessorStatus.Overflow = false;
                    // Consume extra cycle to clear flag?
                    ExecState.CyclesConsumed ++;
                    break;

                // ------------------------
                // SEC
                // ------------------------
                case Ins.SEC:
                    ProcessorStatus.Carry = true;
                    // Consume extra cycle to set flag?
                    ExecState.CyclesConsumed ++;
                    break;

                // ------------------------
                // SED
                // ------------------------
                case Ins.SED:
                    ProcessorStatus.Decimal = true;
                    // Consume extra cycle to set flag?
                    ExecState.CyclesConsumed ++;
                    break;    

                // ------------------------
                // SEI
                // ------------------------
                case Ins.SEI:
                    ProcessorStatus.InterruptDisable = true;
                    // Consume extra cycle to set flag?
                    ExecState.CyclesConsumed ++;
                    break;                        

                // ------------------------
                // Misc system instructions
                // ------------------------
                case Ins.BRK:
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

                case Ins.RTI:
                    ProcessorStatus.Value = PopByteFromStack(mem);
                    ProcessorStatus.Break = false;
                    ProcessorStatus.Unused = false;
                    PC = PopWordFromStack(mem);
                    // Consume two cycles to change SP (?)
                    ExecState.CyclesConsumed += 2;
                    break;                                                            

                case Ins.NOP:
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