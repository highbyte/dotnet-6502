namespace Highbyte.DotNet6502.Systems.Recording;

/// <summary>Bounds actual decompressed bytes, not just the untrusted ZIP directory length.</summary>
internal sealed class DemoLimitedReadStream(Stream source, long expectedLength) : Stream
{
    private long _read;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => expectedLength;
    public override long Position { get => _read; set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public override int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty) return 0;
        int maximum = (int)Math.Min(buffer.Length, expectedLength - _read + 1);
        int count = source.Read(buffer[..maximum]);
        CheckCount(count);
        return count;
    }
    private void CheckCount(int count)
    {
        _read += count;
        if (_read > expectedLength || (count == 0 && _read != expectedLength))
            throw new InvalidDataException("Demo ZIP entry length differs from its expanded data.");
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() { }
    protected override void Dispose(bool disposing)
    {
        if (disposing) source.Dispose();
        base.Dispose(disposing);
    }
}
