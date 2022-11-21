namespace Highbyte.DotNet6502;

public class OpCode
{
    private OpCodeId _code;
    private byte _codeRaw;

    /// <summary>
    /// The OpCodeId Enum representation for an instruction.
    /// </summary>
    /// <value></value>
    public OpCodeId Code { get { return _code; } set { _code = value; _codeRaw = value.ToByte(); } }

    /// <summary>
    /// The op-code 8-bit value for an instruction.
    /// </summary>
    /// <value></value>
    public byte CodeRaw => _codeRaw;

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
