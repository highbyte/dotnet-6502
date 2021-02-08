using System.Collections.Generic;

namespace Highbyte.DotNet6502.Instructions
{
    /// <summary>
    /// Store Accumulator.
    /// Stores the contents of the accumulator into memory.
    /// </summary>
    public class STA : Instruction
    {
        private readonly List<OpCode> _opCodes;
        public override List<OpCode> OpCodes => _opCodes;

        public override bool Execute(CPU cpu, Memory mem, AddrModeCalcResult addrModeCalcResult)
        {
            cpu.StoreByte(cpu.A, mem, addrModeCalcResult.InsAddress.Value);

            // TODO: If this correct? ABS_X, ABS_Y and IND_IX modes takes an extra cycle for this instruction?
            //       Note: A previous method, cpu.CalcFullAddressX/cpu.CalcFullAddressY has already added one cycle if page boundary was crossed.
            if(!addrModeCalcResult.AddressCalculationCrossedPageBoundary &&
                    (addrModeCalcResult.OpCode.AddressingMode == AddrMode.ABS_X
                    || addrModeCalcResult.OpCode.AddressingMode == AddrMode.ABS_Y
                    || addrModeCalcResult.OpCode.AddressingMode == AddrMode.IND_IX))
                cpu.ExecState.CyclesConsumed++;
            return true;
        }

        public STA()
        {
            _opCodes = new List<OpCode>
                {
                    new OpCode
                    {
                        Code = Ins.STA_ZP,
                        AddressingMode = AddrMode.ZP,
                        Size = 2,
                        Cycles = 3,
                    },
                    new OpCode
                    {
                        Code = Ins.STA_ZP_X,
                        AddressingMode = AddrMode.ZP_X,
                        Size = 2,
                        Cycles = 4,
                    },
                    new OpCode
                    {
                        Code = Ins.STA_ABS,
                        AddressingMode = AddrMode.ABS,
                        Size = 3,
                        Cycles = 4,
                    },
                    new OpCode
                    {
                        Code = Ins.STA_ABS_X,
                        AddressingMode = AddrMode.ABS_X,
                        Size = 3,
                        Cycles = 5,
                    },
                    new OpCode
                    {
                        Code = Ins.STA_ABS_Y,
                        AddressingMode = AddrMode.ABS_Y,
                        Size = 3,
                        Cycles = 5,
                    },
                    new OpCode
                    {
                        Code = Ins.STA_IX_IND,
                        AddressingMode = AddrMode.IX_IND,
                        Size = 2,
                        Cycles = 6,
                    },
                    new OpCode
                    {
                        Code = Ins.STA_IND_IX,
                        AddressingMode = AddrMode.IND_IX,
                        Size = 2,
                        Cycles = 6,
                    },
            };
        }
    }
}