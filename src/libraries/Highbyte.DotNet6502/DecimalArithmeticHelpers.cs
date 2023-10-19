using Highbyte.DotNet6502.Instructions;
using System.Drawing;

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
    public static byte AddWithCarryAndOverFlowDecimalMode(byte value1, byte value2, ProcessorStatus processorStatus)
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
        //2e.The N flag result is 1 if bit 7 of A2 is 1, and is 0 if bit 7 if A2 is 0
        //2f.The V flag result is 1 if A < -128 or A2 > 127, and is 0 if -128 <= A2 <= 127

        // Since the Z flag after ADC on the 6502 is "bin", that means the decimal mode Z flag is clear. Thus, to predict the value of the Z flag, simply perform the ADC using binary arithmetic.

        var al = (value1 & 0x0f) + (value2 & 0x0f) + (byte)(processorStatus.Carry ? 1 : 0);
        if (al >= 0x0a)
            al = ((al + 0x06) & 0x0f) + 0x10;
        // Note that sum can be >= $100 at this point
        ushort sum = ((ushort)((value1 & 0xf0) + (value2 & 0xf0) + al));
        if (sum >= 0xa0)
            sum += 0x60;
        processorStatus.Carry = sum >= 0x100;

        // Use signed twos complement arithmetic to calculate a2, which will only be used to set N and V flags
        var value1Signed = (sbyte)value1;
        var value2Signed = (sbyte)value2;
        var a2 = value1Signed + value2Signed + al;
        processorStatus.Negative = (a2 & 0b10000000) == 0b10000000;
        processorStatus.Overflow = a2 < -128 || a2 > 127;

        // Perform a subtraction in binary mode to get Z flag
        var processorStatusBinary = new ProcessorStatus();
        BinaryArithmeticHelpers.AddWithCarryAndOverflow(value1, value2, processorStatusBinary);
        processorStatus.Zero = processorStatusBinary.Zero;

        return (byte)sum; //Lower 8 bits of sum is the result
    }

    public static byte SubtractWithCarryAndOverflowDecimalMode(byte value1, byte value2, ProcessorStatus processorStatus)
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
        BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(value1, value2, processorStatusBinary);
        processorStatus.Carry = processorStatusBinary.Carry;
        processorStatus.Negative = processorStatusBinary.Negative;
        processorStatus.Overflow = processorStatusBinary.Overflow;
        processorStatus.Zero = processorStatusBinary.Zero;

        return (byte)sum;   // Lower 8 bits of sum is the result
    }
}
