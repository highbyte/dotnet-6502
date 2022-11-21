using System.Collections.Generic;

namespace Highbyte.DotNet6502;

/// <summary>
/// 6502 addressing modes
/// </summary>
public enum AddrMode
{
    /// <summary>
    /// Immediate
    /// </summary>        
    I,
    /// <summary>
    /// Zero Page
    /// </summary>        
    ZP,
    /// <summary>
    /// Zero Page, X
    /// </summary>        
    ZP_X,
    /// <summary>
    /// Zero Page, Y
    /// </summary>        
    ZP_Y,
    /// <summary>
    /// Absolute
    /// </summary>        
    ABS,
    /// <summary>
    /// Absolute, X
    /// </summary>        
    ABS_X,
    /// <summary>
    /// Absolute, Y
    /// </summary>        
    ABS_Y,
    /// <summary>
    /// Indexed Indirect
    /// </summary>        
    IX_IND,
    /// <summary>
    /// Indirect Indexed
    /// </summary>        
    IND_IX,
    /// <summary>
    /// Implied/Implicit
    /// </summary>        
    Implied,
    /// <summary>
    /// Accumulator
    /// </summary>        
    Accumulator,
    /// <summary>
    /// Relative
    /// </summary>        
    Relative,
    /// <summary>
    /// Indirect
    /// </summary>        
    Indirect
}
