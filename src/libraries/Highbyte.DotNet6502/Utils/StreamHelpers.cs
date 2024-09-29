namespace Highbyte.DotNet6502.Utils;

public static class StreamHelpers
{
    /// <summary>
    /// Read a little endian word (two bytes) from stream.
    /// Return -1 if end of stream.
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public static int FetchWord(this Stream stream)
    {
        var byte1 = stream.ReadByte();
        if (byte1 < 0)
            return -1;
        var byte2 = stream.ReadByte();
        if (byte2 < 0)
            return -1;
        return ByteHelpers.ToLittleEndianWord((byte)byte1, (byte)byte2);
    }
}
