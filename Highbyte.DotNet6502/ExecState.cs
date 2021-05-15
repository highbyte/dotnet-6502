using System;

namespace Highbyte.DotNet6502
{
    public class ExecState
    {
        public ulong CyclesConsumed { get; private set; }
        public ulong InstructionsExecutionCount { get; private set; }
        public ulong UnknownOpCodeCount { get; private set; }
        public bool LastOpCodeWasHandled  { get; private set; }
        public byte? LastOpCode { get; private set; }
        public ushort? PCBeforeLastOpCodeExecuted { get; private set; }

        public ExecState()
        {
            CyclesConsumed = 0;
            InstructionsExecutionCount = 0;
            UnknownOpCodeCount = 0;
            LastOpCodeWasHandled = false;
            LastOpCode = null;
            PCBeforeLastOpCodeExecuted = null;
        }
        public static ExecState ExecStateAfterInstruction(ulong cyclesConsumed, bool unknownInstruction, byte? lastOpCode, ushort? lastPC)
        {
            var execState = new ExecState();
            execState.InstructionsExecutionCount = 1;
            execState.CyclesConsumed = cyclesConsumed;
            execState.UnknownOpCodeCount = unknownInstruction?(ulong)1:(ulong)0;
            execState.PCBeforeLastOpCodeExecuted = lastPC;
            execState.LastOpCode = lastOpCode;
            return execState;
        }        

        public ExecState Clone()
        {
            return new ExecState
            {
                CyclesConsumed = this.CyclesConsumed,
                InstructionsExecutionCount = this.InstructionsExecutionCount,
                UnknownOpCodeCount = this.UnknownOpCodeCount,
                LastOpCodeWasHandled = this.LastOpCodeWasHandled,
                LastOpCode = this.LastOpCode,
                PCBeforeLastOpCodeExecuted = this.PCBeforeLastOpCodeExecuted
            };
        }

        internal void UpdateTotal(ExecState newExecState)
        {
            CyclesConsumed += newExecState.CyclesConsumed;
            InstructionsExecutionCount += newExecState.InstructionsExecutionCount;
            UnknownOpCodeCount += newExecState.UnknownOpCodeCount;
            LastOpCodeWasHandled = newExecState.LastOpCodeWasHandled;
            LastOpCode = newExecState.LastOpCode;
            PCBeforeLastOpCodeExecuted = newExecState.PCBeforeLastOpCodeExecuted;
        }
    }
}
