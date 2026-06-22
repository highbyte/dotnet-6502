namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge.Crt;

public class C64CrtImageException : IOException
{
    public C64CrtImageException(string message)
        : base(message)
    {
    }
}

public sealed class C64UnsupportedCrtHardwareException : C64CrtImageException
{
    public C64UnsupportedCrtHardwareException(ushort hardwareType)
        : base($"CRT cartridge hardware type {hardwareType} is not supported.")
    {
        HardwareType = hardwareType;
    }

    public ushort HardwareType { get; }
}
