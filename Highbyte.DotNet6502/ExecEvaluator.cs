namespace Highbyte.DotNet6502
{
    public interface IExecEvaluator
    {
        public bool Check(ExecState execState, CPU cpu, Memory mem);
    }

    public class LegacyExecEvaluator : IExecEvaluator
    {
        public ExecOptions ExecOptions => _execOptions;

        private readonly ExecOptions _execOptions;

        public static LegacyExecEvaluator OneInstructionExecEvaluator = new LegacyExecEvaluator(new ExecOptions { MaxNumberOfInstructions = 1 });

        public static LegacyExecEvaluator InstructionCountExecEvaluator(ulong numberOfInstructions)
        {
            return new LegacyExecEvaluator(new ExecOptions { MaxNumberOfInstructions = numberOfInstructions });
        }

        public LegacyExecEvaluator(ExecOptions execOptions)
        {
            _execOptions = execOptions;
        }

        public bool Check(ExecState execState, CPU cpu, Memory mem)
        {
            var cont = true;

            var instructionExecutionResult = execState.LastInstructionExecResult;

            // Check if we're configured to throw exception when unknown exception occurs
            if (instructionExecutionResult.UnknownInstruction && ExecOptions.UnknownInstructionThrowsException)
                throw new DotNet6502Exception($"Unknown opcode: {instructionExecutionResult.OpCodeByte.ToHex()}");

            // Check if we should continue executing instructions
            if (ExecOptions.CyclesRequested.HasValue && execState.CyclesConsumed >= ExecOptions.CyclesRequested.Value)
                cont = false;
            if (ExecOptions.MaxNumberOfInstructions.HasValue && execState.InstructionsExecutionCount >= ExecOptions.MaxNumberOfInstructions.Value)
                cont = false;
            if (!instructionExecutionResult.UnknownInstruction && ExecOptions.ExecuteUntilInstruction.HasValue && instructionExecutionResult.OpCodeByte == ExecOptions.ExecuteUntilInstruction.Value.ToByte())
                cont = false;
            if (ExecOptions.ExecuteUntilInstructions.Count > 0 && ExecOptions.ExecuteUntilInstructions.Contains(instructionExecutionResult.OpCodeByte))
                cont = false;
            if (ExecOptions.ExecuteUntilPC.HasValue && cpu.PC == ExecOptions.ExecuteUntilPC.Value)
                cont = false;
            if (ExecOptions.ExecuteUntilExecutedInstructionAtPC.HasValue && execState.PCBeforeLastOpCodeExecuted == ExecOptions.ExecuteUntilExecutedInstructionAtPC.Value)
                cont = false;

            return cont;
        }
    }

    public class AlwaysExecEvaluator : IExecEvaluator
    {
        public bool Check(ExecState execState, CPU cpu, Memory mem) => true;
    }
}
