namespace Highbyte.DotNet6502
{
    public class ExecState
    {
        public ulong CyclesConsumed { get; private set; }
        public ulong InstructionsExecutionCount { get; private set; }
        public ulong UnknownOpCodeCount { get; private set; }

        public InstructionExecResult LastInstructionExecResult { get; private set; }
        public bool LastOpCodeWasHandled { get { return !LastInstructionExecResult.UnknownInstruction; } }
        public ushort? PCBeforeLastOpCodeExecuted { get; private set; }

        public ExecState()
        {
            CyclesConsumed = 0;
            InstructionsExecutionCount = 0;
            UnknownOpCodeCount = 0;
            LastInstructionExecResult = null;
            PCBeforeLastOpCodeExecuted = null;
        }
        public static ExecState ExecStateAfterInstruction(InstructionExecResult lastinstructionExecutionResult, ushort? lastPC)
        {
            var execState = new ExecState();
            execState.InstructionsExecutionCount = 1;
            execState.CyclesConsumed = lastinstructionExecutionResult.CyclesConsumed;
            execState.LastInstructionExecResult = lastinstructionExecutionResult;
            execState.UnknownOpCodeCount = lastinstructionExecutionResult.UnknownInstruction ? (ulong)1 : (ulong)0;
            execState.PCBeforeLastOpCodeExecuted = lastPC;
            return execState;
        }

        public ExecState Clone()
        {
            return new ExecState
            {
                CyclesConsumed = this.CyclesConsumed,
                InstructionsExecutionCount = this.InstructionsExecutionCount,
                UnknownOpCodeCount = this.UnknownOpCodeCount,
                LastInstructionExecResult = this.LastInstructionExecResult,
                PCBeforeLastOpCodeExecuted = this.PCBeforeLastOpCodeExecuted
            };
        }

        internal void UpdateTotal(ExecState newExecState)
        {
            CyclesConsumed += newExecState.CyclesConsumed;
            InstructionsExecutionCount += newExecState.InstructionsExecutionCount;
            UnknownOpCodeCount += newExecState.UnknownOpCodeCount;
            LastInstructionExecResult = newExecState.LastInstructionExecResult;
            PCBeforeLastOpCodeExecuted = newExecState.PCBeforeLastOpCodeExecuted;
        }
    }
}
