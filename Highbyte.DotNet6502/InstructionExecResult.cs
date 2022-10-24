namespace Highbyte.DotNet6502
{
    public class InstructionExecResult
    {
        public byte OpCodeByte { get; private set; }
        public bool UnknownInstruction { get; set; }
        public ulong CyclesConsumed { get; set; }

        private static InstructionExecResult successfullIntstructionExecResult = new(0) { UnknownInstruction = false };

        public InstructionExecResult(byte opCodeByte)
        {
            OpCodeByte = opCodeByte;
            UnknownInstruction = false;
        }

        public static InstructionExecResult UnknownInstructionResult(byte opCodeByte)
        {
            return new InstructionExecResult(opCodeByte)
            {
                UnknownInstruction = true,
                CyclesConsumed = 1
            };
        }

        public static InstructionExecResult SuccessfulInstructionResult(byte opCodeByte, ulong cyclesConsumed)
        {
            // Note: Minor perf improvement to reuse same InstructionExecResult object instead creating new one each time.
            successfullIntstructionExecResult.OpCodeByte = opCodeByte;
            successfullIntstructionExecResult.CyclesConsumed = cyclesConsumed;
            return successfullIntstructionExecResult;

            //return new InstructionExecResult(opCodeByte)
            //{
            //    UnknownInstruction = false,
            //    CyclesConsumed = cyclesConsumed
            //};
        }

    }    
}
