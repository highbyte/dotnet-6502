namespace Highbyte.DotNet6502
{
    public class InstructionExecResult
    {
        public byte OpCodeByte { get; set; }
        public bool UnknownInstruction { get; set; }
        public ulong CyclesConsumed { get; set; }
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
    }    
}