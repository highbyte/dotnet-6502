using Avalonia.Input;
using Highbyte.DotNet6502.Impl.Avalonia.Input;
using Highbyte.DotNet6502.Systems.Input;

namespace Highbyte.DotNet6502.Systems.Tests;

public class AvaloniaInputHandlerContextTests
{
    [Fact]
    public void LogicalPageUpOverride_Replaces_PhysicalArrowUp_InHostKeys()
    {
        var input = new AvaloniaInputHandlerContext();

        input.AddKeyDown(Key.PageUp, PhysicalKey.ArrowUp);

        var hostKeys = ((IHostInputState)input).KeysDown;
        Assert.Contains(HostKey.PageUp, hostKeys);
        Assert.DoesNotContain(HostKey.ArrowUp, hostKeys);
    }

    [Fact]
    public void PlainArrowUp_Remains_ArrowUp_InHostKeys()
    {
        var input = new AvaloniaInputHandlerContext();

        input.AddKeyDown(Key.Up, PhysicalKey.ArrowUp);

        var hostKeys = ((IHostInputState)input).KeysDown;
        Assert.Contains(HostKey.ArrowUp, hostKeys);
        Assert.DoesNotContain(HostKey.PageUp, hostKeys);
    }

    [Fact]
    public void RemovingLogicalOverride_Clears_The_Normalized_HostKey()
    {
        var input = new AvaloniaInputHandlerContext();

        input.AddKeyDown(Key.PageUp, PhysicalKey.ArrowUp);
        input.RemoveKeyDown(Key.PageUp, PhysicalKey.ArrowUp);

        Assert.Empty(((IHostInputState)input).KeysDown);
    }
}
