using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.AspNet;

public class AspNetInputHandlerContext : IInputHandlerContext
{
    private readonly ILogger<AspNetInputHandlerContext> _logger;

    public HashSet<string> KeysDown = new();

    private bool _capsLockKeyDownCaptured;
    private bool _capsLockOn;

    public AspNetInputHandlerContext(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<AspNetInputHandlerContext>();
    }

    public void Init()
    {
    }

    public void KeyUp(KeyboardEventArgs e)
    {
        if (KeysDown.Contains(e.Code))
        {
            _logger.LogDebug($"Host KeyUp event: {e.Key} ({e.Code})");
            KeysDown.Remove(e.Code);
        }

        if (e.Code == "CapsLock")
        {
            _capsLockKeyDownCaptured = false;
        }
    }

    public void KeyDown(KeyboardEventArgs e)
    {
        if (!KeysDown.Contains(e.Code))
        {
            _logger.LogDebug($"Host KeyDown event: {e.Key} ({e.Code})");
            KeysDown.Add(e.Code);
        }

        if (e.Code == "CapsLock" && !_capsLockKeyDownCaptured)
        {
            _capsLockKeyDownCaptured = true;
            _capsLockOn = !_capsLockOn; // Toggle state
        }
    }

    public void OnFocus(FocusEventArgs e)
    {
        _logger.LogDebug($"Host OnFocus event");
        KeysDown.Clear();
    }

    public bool GetCapsLockState()
    {
        // TODO: Is there a built-in way in Javascript/WASM to check if CapsLock is on?
        //       That could improve the custom detection in this code, which might not match the actual caps lock state of the user. 
        return _capsLockOn;
    }

    public void Cleanup()
    {
    }
}
