namespace Highbyte.DotNet6502
{
    public class InstructionLogicResult
    {
        public ulong ExtraConsumedCycles { get; set; }
        public static InstructionLogicResult WithNoExtraCycles()
        {
            return new InstructionLogicResult
            {
                ExtraConsumedCycles = 0
            };
        }
        public static InstructionLogicResult WithExtraCycles(ulong extraCycles)
        {
            return new InstructionLogicResult
            {
                ExtraConsumedCycles = extraCycles
            };
        }
    }    
}