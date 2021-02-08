using System.Collections.Generic;

namespace Highbyte.DotNet6502
{
    public class OpCode
    {
        /// <summary>
        /// The OpCode for an instruction.
        /// </summary>
        /// <value></value>
        public Ins Code { get; set;}

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
        /// Can take more depending on zero page wrap-around or crossing page boundary
        /// </summary>
        /// <value></value>
        public int Cycles { get; set;}
    }
}
