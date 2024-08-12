namespace Highbyte.DotNet6502.Systems.Utils;

public static class BytePixelManipulation
{
    /// <summary>
    /// Doubles the "width" of the bits ("pixels") in a byte into two bytes.
    /// Examples (with expandToRight = true):
    /// originalByte => doubleWidthBytes filled with (byte0,byte1)
    /// 10101010     => 11001100,11001100
    /// 11110000     => 11111111,00000000
    /// 01111110     => 00111111,11111100
    /// </summary>
    /// <param name="originalByte"></param>
    /// <param name="doubleWidthBytes">A pre-created span of two bytes</param>
    /// <param name="expandToRight"></param>
    /// <returns></returns>
    public static void StretchBits(this byte originalByte, ref Span<byte> doubleWidthBytes, bool expandToRight = true)
    {
        for (int i = 0; i < 8; i++)
        {
            bool bit = (originalByte & (1 << i)) != 0;
            var index = expandToRight ? 1 - (i / 4) : (i / 4);
            doubleWidthBytes[index] |= (byte)(bit ? (3 << (2 * (i % 4))) : 0);
        }
    }
}
