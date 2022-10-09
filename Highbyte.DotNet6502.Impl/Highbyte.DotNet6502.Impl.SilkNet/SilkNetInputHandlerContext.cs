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
    public HashSet<char> CharactersReceived = new();

    public bool IsKeyPressed(Key key) => _primaryKeyboard.IsKeyPressed(key);

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

    private void KeyUp(IKeyboard keyboard, Key key, int x)
    {
        if (!KeysUp.Contains(key))
        {
            Debug.WriteLine($"KeyUp captured for frame: {key}");
            KeysUp.Add(key);
        }
    }

    private void KeyDown(IKeyboard keyboard, Key key, int x)
    {
        if (!KeysDown.Contains(key))
        {
            Debug.WriteLine($"KeyDown captured for frame: {key}");
            KeysDown.Add(key);
        }
    }

    private void KeyReceived(IKeyboard keyboard, char character)
    {
        Debug.WriteLine($"Character received: {character}");
        if (!CharactersReceived.Contains(character))
            CharactersReceived.Add(character);
    }

    public void ClearKeys()
    {
        KeysUp.Clear();
        KeysDown.Clear();
        CharactersReceived.Clear();
    }

    public void Cleanup()
    {
        s_inputcontext?.Dispose();
    }
}
