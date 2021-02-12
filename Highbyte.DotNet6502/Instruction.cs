using System.Collections.Generic;

namespace Highbyte.DotNet6502
{
    /// <summary>
    /// </summary>
    public abstract class Instruction
    {
        public virtual string Name => GetType().Name;
        public abstract List<OpCode> OpCodes { get; }

        public bool SupportsAddressingMode(AddrMode mode)
        {
            return OpCodes.Exists(x=>x.AddressingMode == mode);
        }

    }
}
