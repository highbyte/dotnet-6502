using System.Diagnostics;
using Highbyte.DotNet6502.Systems;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace Highbyte.DotNet6502.Impl.SilkNet;

public class SilkNetInputHandlerContext : IInputHandlerContext
{
    private readonly IWindow _silkNetWindow;
    private static IInputContext s_inputcontext;
    private IKeyboard _primaryKeyboard;

    public bool Exit { get; private set; }

    public HashSet<Key> KeysUp = new();
    public HashSet<Key> KeysDown = new();
    public HashSet<char> KeysReceived = new();

    // public bool IsKeyUp(Key key) => KeysUp.Contains(key);
    // public bool IsKeyDown(Key key) => KeysDown.Contains(key);
    public bool IsKeyReceived(char character) => KeysReceived.Contains(character);

    public bool IsKeyPressed(Key key) => _primaryKeyboard.IsKeyPressed(key);

    private HashSet<Key> _specialKeyDown = new();
    public HashSet<Key> SpecialKeyReceived = new();


    public SilkNetInputHandlerContext(IWindow silkNetWindow)
    {
        _silkNetWindow = silkNetWindow;
    }

    public void Init()
    {
        Exit = false;

        s_inputcontext = _silkNetWindow.CreateInput();

        // Silk.NET Input: Keyboard
        if (s_inputcontext == null)
            throw new Exception("Silk.NET Input Context not found.");
        if (s_inputcontext.Keyboards != null && s_inputcontext.Keyboards.Count != 0)
            _primaryKeyboard = s_inputcontext.Keyboards[0];
        if (_primaryKeyboard == null)
            throw new Exception("Keyboard not found");

        _primaryKeyboard.KeyUp += KeyUp;
        _primaryKeyboard.KeyDown += KeyDown;
        _primaryKeyboard.KeyChar += KeyReceived;
    }

    public void KeyUp(IKeyboard keyboard, Key key, int x)
    {
        Debug.WriteLine($"KeyUp: {key}");
        if (!KeysUp.Contains(key))
            KeysUp.Add(key);

        if (_specialKeyDown.Contains(key))
        {
            _specialKeyDown.Remove(key);
        }

    }

    public void KeyDown(IKeyboard keyboard, Key key, int x)
    {
        Debug.WriteLine($"KeyDown: {key}");
        if (!KeysDown.Contains(key))
            KeysDown.Add(key);

        if (!_specialKeyDown.Contains(key))
        {
            _specialKeyDown.Add(key);
            SpecialKeyReceived.Add(key);
            Debug.WriteLine($"Special key received: {key}");
        }
    }

    public void KeyReceived(IKeyboard keyboard, char character)
    {
        Debug.WriteLine($"KeyReceived: {character}");
        if (!KeysReceived.Contains(character))
            KeysReceived.Add(character);
    }

    public void ClearKeys()
    {
        KeysUp.Clear();
        KeysDown.Clear();
        KeysReceived.Clear();

        SpecialKeyReceived.Clear();
    }

    public void Cleanup()
    {
        s_inputcontext?.Dispose();
    }
}
