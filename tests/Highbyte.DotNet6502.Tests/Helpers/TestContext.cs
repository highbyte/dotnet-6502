namespace Highbyte.DotNet6502.Tests.Helpers;

public class TestContext
{
    public CPU CPU { get; private set; }
    public Memory Mem { get; private set; }
    private TestContext() {}
    public static TestContext NewTestContext(ushort startPos = 0x1000, int memorySize = 1024*64)
    {
        var cpu = new CPU();
        cpu.PC = startPos;
        var mem = new Memory(memorySize);
        return new TestContext
        {
            CPU = cpu,
            Mem = mem,
        };
    }
}
