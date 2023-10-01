namespace Highbyte.DotNet6502.Systems.Utils;

internal static class BytePixelManipulation
{

    /// <summary>
    /// Doubles the "width" of the bits ("pixels") in a byte into two bytes.
    /// Examples (with expandToRight = true):
    /// originalByte => return array (byte0,byte1)
    /// 10101010     => 11001100,11001100
    /// 11110000     => 11111111,00000000
    /// 01111110     => 00111111,11111100
    /// </summary>
    /// <param name="originalByte"></param>
    /// <param name="expandToRight"></param>
    /// <returns></returns>
    public static byte[] StretchBits(this byte originalByte, bool expandToRight = true)
    {
        byte[] doubleWidthBytes = new byte[2];
        for (int i = 0; i < 8; i++)
        {
            bool bit = (originalByte & (1 << i)) != 0;
            var index = expandToRight ? 1 - (i / 4) : (i / 4);
            doubleWidthBytes[index] |= (byte)(bit ? (3 << (2 * (i % 4))) : 0);
        }
        return doubleWidthBytes;
    }
}
