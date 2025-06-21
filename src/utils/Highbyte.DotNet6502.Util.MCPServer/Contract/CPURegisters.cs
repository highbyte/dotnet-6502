
namespace Highbyte.DotNet6502.Util.MCPServer.Contract;
public class CPURegisters
{
    /// <summary>
    /// Program Counter
    /// </summary>
    public ushort PC { get; set; }

    /// <summary>
    /// Stack Pointer
    /// The 6502 microprocessor supports a 256 byte stack fixed between memory locations $0100 and $01FF. 
    /// A special 8-bit register, S, is used to keep track of the next free byte of stack space. 
    /// Pushing a byte on to the stack causes the value to be stored at the current free location (e.g. $0100,S) 
    /// and then the stack pointer is post decremented. 
    /// Pull operations reverse this procedure.
    /// 
    /// The stack register can only be accessed by transferring its value to or from the X register via instructions TSX and TXS.
    /// Its value is automatically modified by push/pull instructions, subroutine calls and returns, interrupts and returns from interrupts.
    /// 
    /// Other instructions for storing values on stack: PHA, PHP, PLA, PLP
    /// </summary>
    public byte SP { get; set; }

    /// <summary>
    /// Accumulator
    /// </summary>
    public byte A { get; set; }

    /// <summary>
    /// Index Register X
    /// </summary>
    public byte X { get; set; }

    /// <summary>
    /// Index Register Y
    /// </summary>
    public byte Y { get; set; }

    /// <summary>
    /// Processor Status.
    /// 
    /// As instructions are executed a set of processor flags are set or clear to record the results of the operation. This flags and some additional control flags are held in a special status register. Each flag has a single bit within the register.
    /// 
    /// Instructions exist to test the values of the various bits, to set or clear some of them and to push or pull the entire set to or from the stack.
    /// </summary>
    public ProcessorStatus ProcessorStatus { get; set; }
}
