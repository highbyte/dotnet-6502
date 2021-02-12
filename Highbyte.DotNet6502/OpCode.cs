namespace Highbyte.DotNet6502
{
    public class OpCode
    {
        /// <summary>
        /// The OpCode for an instruction.
        /// </summary>
        /// <value></value>
        public OpCodeId Code { get; set;}

        /// <summary>
        /// The addressing mode for an instruction
        /// </summary>
        /// <value></value>
        public AddrMode AddressingMode { get; set;}

        /// <summary>
        /// How many bytes the instruction takes.
        /// </summary>
        /// <value></value>
        public int Size { get; set;}

        /// <summary>
        /// Number of cycles the instruction consumes, at a minimum.
        /// Can take more cycles depending on zero page wrap-around or crossing page boundary
        /// </summary>
        /// <value></value>
        public ulong MinimumCycles { get; set;}
    }
}