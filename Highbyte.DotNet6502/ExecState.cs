namespace Highbyte.DotNet6502
{
    public class ExecState
    {
        public ulong CyclesConsumed { get; set; }
        public ulong InstructionsExecutionCount { get; set; }
        public ulong UnknownOpCodeCount { get; set; }
        public bool LastOpCodeWasHandled  { get; set; }
        public byte? LastOpCode { get; set; }
        public ushort? PCBeforeLastOpCodeExecuted { get; set; }

        public ExecState()
        {
            CyclesConsumed = 0;
            InstructionsExecutionCount = 0;
            UnknownOpCodeCount = 0;
            LastOpCodeWasHandled = false;
            LastOpCode = null;
            PCBeforeLastOpCodeExecuted = null;
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
    }
}
