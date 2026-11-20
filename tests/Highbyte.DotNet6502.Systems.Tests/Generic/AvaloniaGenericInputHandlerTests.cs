using Highbyte.DotNet6502.Impl.Avalonia.Generic.Input;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Input;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Generic;

public class AvaloniaGenericInputHandlerTests
{
    [Theory]
    [InlineData(HostKey.Space, ' ')]
    [InlineData(HostKey.Enter, '\n')]
    [InlineData(HostKey.KeyW, 'w')]
    [InlineData(HostKey.Digit7, '7')]
    public void Reads_key_press_and_release_from_host_input_state(HostKey key, char expected)
    {
        var system = new GenericComputerBuilder().WithMemory().Build();
        var config = system.GenericComputerConfig.Memory.Input;
        var handler = new AvaloniaGenericInputHandler(system, config, NullLoggerFactory.Instance);
        var keys = new HashSet<HostKey> { key };
        handler.Init(new HostInput(keys));

        handler.BeforeFrame();
        Assert.Equal((byte)expected, system.Mem[config.KeyDownAddress]);

        keys.Clear();
        handler.BeforeFrame();
        Assert.Equal(0, system.Mem[config.KeyDownAddress]);
    }

    private sealed class HostInput(IReadOnlySet<HostKey> keys) : IHostInputState
    {
        public IReadOnlySet<HostKey> KeysDown { get; private set; } = new HashSet<HostKey>();
        public IReadOnlySet<GamepadButton> GamepadButtonsDown { get; } = new HashSet<GamepadButton>();
        public bool CapsLockOn => false;
        public void UpdatePerFrame() => KeysDown = new HashSet<HostKey>(keys);
    }
}
