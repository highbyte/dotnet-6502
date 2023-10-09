using System.Runtime.InteropServices;
using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Logging;
using Silk.NET.SDL;

namespace Highbyte.DotNet6502.Impl.SilkNet;

public class SilkNetInputHandlerContext : IInputHandlerContext
{
    private readonly IWindow _silkNetWindow;
    private readonly ILogger<SilkNetInputHandlerContext> _logger;
    private static IInputContext s_inputcontext;
    public IInputContext InputContext => s_inputcontext;
    private IKeyboard _primaryKeyboard;
    public IKeyboard PrimaryKeyboard => _primaryKeyboard;

    public HashSet<Key> KeysDown = new();

    private bool _capsLockKeyDownCaptured;
    private bool _capsLockOn;

    public bool Quit { get; private set; }

    public bool IsKeyPressed(Key key) => _primaryKeyboard.IsKeyPressed(key);

    public SilkNetInputHandlerContext(IWindow silkNetWindow, ILoggerFactory loggerFactory)
    {
        _silkNetWindow = silkNetWindow;
        _logger = loggerFactory.CreateLogger<SilkNetInputHandlerContext>();
    }

    public void Init()
    {
        Quit = false;

        s_inputcontext = _silkNetWindow.CreateInput();

        // Silk.NET Input: Keyboard
        if (s_inputcontext == null)
            throw new Exception("Silk.NET Input Context not found.");
        if (s_inputcontext.Keyboards != null && s_inputcontext.Keyboards.Count != 0)
            _primaryKeyboard = s_inputcontext.Keyboards[0];
        if (_primaryKeyboard == null)
            throw new Exception("Keyboard not found");

        ListenForKeyboardInput(enabled: true);

    }

    public void ListenForKeyboardInput(bool enabled)
    {
        if (enabled)
        {
            // Unregister any existing handlers to avoid duplicates
            _primaryKeyboard.KeyUp -= KeyUp;
            _primaryKeyboard.KeyDown -= KeyDown;

            _primaryKeyboard.KeyUp += KeyUp;
            _primaryKeyboard.KeyDown += KeyDown;

        }
        else
        {
            _primaryKeyboard.KeyUp -= KeyUp;
            _primaryKeyboard.KeyDown -= KeyDown;
        }
    }

    private void KeyUp(IKeyboard keyboard, Key key, int scanCode)
    {
        if (KeysDown.Contains(key))
        {
            _logger.LogDebug($"Host KeyUp event: {key} ({scanCode})");
            KeysDown.Remove(key);
        }

        if (key == Key.CapsLock)
        {
            _capsLockKeyDownCaptured = false;
        }
    }

    private void KeyDown(IKeyboard keyboard, Key key, int scanCode)
    {
        if (!KeysDown.Contains(key))
        {
            _logger.LogDebug($"Host KeyDown event: {key} ({scanCode})");
            KeysDown.Add(key);
        }

        if (key == Key.CapsLock && !_capsLockKeyDownCaptured)
        {
            _capsLockKeyDownCaptured = true;
            _capsLockOn = !_capsLockOn; // Toggle state
        }
    }

    public bool GetCapsLockState()
    {
        // On Windows, Console.CapsLock can be used to check if CapsLock is on.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Console.CapsLock;

        // On Linux and Mac return our own captured caps lock state (which may be wrong if the user has pressed the caps lock key outside of the emulator).
        return _capsLockOn;
    }

    public void Cleanup()
    {
        s_inputcontext?.Dispose();
    }
}
