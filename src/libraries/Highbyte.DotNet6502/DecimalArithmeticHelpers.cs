namespace Highbyte.DotNet6502;

public static class DecimalArithmeticHelpers
{

    /// <summary>
    /// Perform Add with Carry (ADC) in decimal mode.
    /// </summary>
    /// <param name="value1"></param>
    /// <param name="value2"></param>
    /// <param name="processorStatus"></param>
    /// <returns></returns>
    public static byte AddWithCarryAndOverFlowDecimalMode(byte value1, byte value2, ref ProcessorStatus processorStatus)
    {
        // Pseudo code from http://6502.org/tutorials/decimal_mode.html
        //1a.AL = (A & $0F) +(B & $0F) +C
        //1b.If AL >= $0A, then AL = ((AL + $06) & $0F) + $10
        //1c.A = (A & $F0) +(B & $F0) +AL
        //1d.Note that A can be >= $100 at this point
        //1e.If(A >= $A0), then A = A + $60
        //1f.The accumulator result is the lower 8 bits of A
        //1g.The carry result is 1 if A >= $100, and is 0 if A < $100
        //
        //2c. A2 = (A & $F0) + (B & $F0) + AL, using signed (twos complement) arithmetic
        //2e. The N flag result is 1 if bit 7 of A2 is 1, and is 0 if bit 7 if A2 is 0
        //2f. The V flag result is 1 if A2 < -128 or A2> 127, and is 0 if -128 <= A2 <= 127

        // Since the Z flag after ADC on the 6502 is "bin", that means the decimal mode Z flag is clear. Thus, to predict the value of the Z flag, simply perform the ADC using binary arithmetic.
        var originalCarry = processorStatus.Carry;

        byte al = (byte)((value1 & 0x0f) + (value2 & 0x0f) + (byte)(processorStatus.Carry ? 1 : 0));
        if (al >= 0x0a)
            al = (byte)(((al + 0x06) & 0x0f) + 0x10);
        // Note that sum can be >= $100 at this point
        var sum = ((value1 & 0xf0) + (value2 & 0xf0) + al);
        if (sum >= 0xa0)
            sum += 0x60;
        processorStatus.Carry = sum >= 0x100;

        // Use signed twos complement arithmetic to calculate a2, which will only be used to set N and V flags
        var value1Signed = (sbyte)value1;
        var value2Signed = (sbyte)value2;
        short a2 = (short)((sbyte)(value1Signed & 0xf0) + (sbyte)(value2Signed & 0xf0) + (sbyte)al);
        processorStatus.Negative = (a2 & 0b10000000) == 0b10000000;
        processorStatus.Overflow = a2 < -128 || a2 > 127;

        // Perform a addition in binary mode to get Z flag
        var processorStatusBinary = new ProcessorStatus();
        processorStatusBinary.Carry = originalCarry;
        BinaryArithmeticHelpers.AddWithCarryAndOverflow(value1, value2, ref processorStatusBinary);
        processorStatus.Zero = processorStatusBinary.Zero;

        return (byte)sum; //Lower 8 bits of sum is the result
    }

    /// <summary>
    /// Perform Add with Carry (ADC) in decimal mode as the 65C02 does it.
    /// Same accumulator/carry sequence as the NMOS 6502, but N and Z are computed from
    /// the FINAL decimal result ("valid" flags — the reason decimal ADC costs an extra
    /// cycle on the 65C02). V comes from the same signed intermediate as on NMOS.
    /// Pseudo code from http://6502.org/tutorials/decimal_mode.html (Appendix A).
    /// </summary>
    public static byte AddWithCarryAndOverFlowDecimalModeCmos(byte value1, byte value2, ref ProcessorStatus processorStatus)
    {
        byte al = (byte)((value1 & 0x0f) + (value2 & 0x0f) + (byte)(processorStatus.Carry ? 1 : 0));
        if (al >= 0x0a)
            al = (byte)(((al + 0x06) & 0x0f) + 0x10);
        var sum = ((value1 & 0xf0) + (value2 & 0xf0) + al);

        // V from the signed intermediate (before the high-nibble +$60 correction), as on NMOS.
        var value1Signed = (sbyte)value1;
        var value2Signed = (sbyte)value2;
        short a2 = (short)((sbyte)(value1Signed & 0xf0) + (sbyte)(value2Signed & 0xf0) + (sbyte)al);
        processorStatus.Overflow = a2 < -128 || a2 > 127;

        if (sum >= 0xa0)
            sum += 0x60;
        processorStatus.Carry = sum >= 0x100;

        var result = (byte)sum;
        // 65C02: N and Z are valid — computed from the final decimal result.
        processorStatus.Negative = (result & 0b10000000) == 0b10000000;
        processorStatus.Zero = result == 0;
        return result;
    }

    public static byte SubtractWithCarryAndOverflowDecimalMode(byte value1, byte value2, ref ProcessorStatus processorStatus)
    {
        // Pseudo code from http://6502.org/tutorials/decimal_mode.html
        //3a.AL = (A & $0F) -(B & $0F) +C - 1
        //3b.If AL< 0, then AL = ((AL - $06) & $0F) - $10
        //3c.A = (A & $F0) -(B & $F0) +AL
        //3d.If A < 0, then A = A - $60
        //3e.The accumulator result is the lower 8 bits of A

        // The C,N,V, and Z flags are set in "bin" mode, which is the same they would have been if subtracting in binary mode (not decimal)

        var al = (value1 & 0x0f) - (value2 & 0x0f) + (byte)(processorStatus.Carry ? 1 : 0) - 1;
        if (al < 0)
            al = ((al - 0x06) & 0x0f) - 0x10;
        var sum = (value1 & 0xf0) - (value2 & 0xf0) + al;
        if (sum < 0)
            sum -= 0x60;

        // Perform a subtraction in binary mode to get C,N,V, and Z flags
        var processorStatusBinary = new ProcessorStatus();
        processorStatusBinary.Carry = processorStatus.Carry;
        BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(value1, value2, ref processorStatusBinary);
        processorStatus.Carry = processorStatusBinary.Carry;
        processorStatus.Negative = processorStatusBinary.Negative;
        processorStatus.Overflow = processorStatusBinary.Overflow;
        processorStatus.Zero = processorStatusBinary.Zero;

        return (byte)sum;   // Lower 8 bits of sum is the result
    }

    /// <summary>
    /// Perform Subtract with Carry (SBC) in decimal mode as the 65C02 does it.
    /// The 65C02 uses a DIFFERENT correction sequence than the NMOS 6502 (Seq. 4 vs
    /// Seq. 3 in the tutorial) — for invalid BCD operands the accumulator results can
    /// differ between the two chips. C and V are the binary-mode results; N and Z are
    /// computed from the FINAL decimal result ("valid" flags, +1 cycle).
    /// Pseudo code from http://6502.org/tutorials/decimal_mode.html (Appendix A):
    /// 4a. AL = (A &amp; $0F) - (B &amp; $0F) + C-1
    /// 4b. A = A - B + C-1
    /// 4c. If A &lt; 0, then A = A - $60
    /// 4d. If AL &lt; 0, then A = A - $06
    /// </summary>
    public static byte SubtractWithCarryAndOverflowDecimalModeCmos(byte value1, byte value2, ref ProcessorStatus processorStatus)
    {
        var al = (value1 & 0x0f) - (value2 & 0x0f) + (byte)(processorStatus.Carry ? 1 : 0) - 1;
        var sum = value1 - value2 + (byte)(processorStatus.Carry ? 1 : 0) - 1;
        if (sum < 0)
            sum -= 0x60;
        if (al < 0)
            sum -= 0x06;

        // C and V are the binary-mode results.
        var processorStatusBinary = new ProcessorStatus();
        processorStatusBinary.Carry = processorStatus.Carry;
        BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(value1, value2, ref processorStatusBinary);
        processorStatus.Carry = processorStatusBinary.Carry;
        processorStatus.Overflow = processorStatusBinary.Overflow;

        var result = (byte)sum;
        // 65C02: N and Z are valid — computed from the final decimal result.
        processorStatus.Negative = (result & 0b10000000) == 0b10000000;
        processorStatus.Zero = result == 0;
        return result;
    }
}
