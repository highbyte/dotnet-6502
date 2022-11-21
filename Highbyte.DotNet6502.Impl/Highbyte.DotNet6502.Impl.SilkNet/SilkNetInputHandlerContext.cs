using System.Diagnostics;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.Impl.SilkNet;

public class SilkNetInputHandlerContext : IInputHandlerContext
{
    private readonly IWindow _silkNetWindow;
    private static IInputContext s_inputcontext;
    public IInputContext InputContext => s_inputcontext;
    private IKeyboard _primaryKeyboard;
    public IKeyboard PrimaryKeyboard => _primaryKeyboard;

    public HashSet<Key> KeysUp = new();
    public HashSet<Key> KeysDown = new();
    public HashSet<char> CharactersReceived = new();

    public bool Quit { get; private set; }

    public bool IsKeyPressed(Key key) => _primaryKeyboard.IsKeyPressed(key);

    public SilkNetInputHandlerContext(IWindow silkNetWindow)
    {
        _silkNetWindow = silkNetWindow;
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
            _primaryKeyboard.KeyChar -= KeyReceived;

            _primaryKeyboard.KeyUp += KeyUp;
            _primaryKeyboard.KeyDown += KeyDown;
            _primaryKeyboard.KeyChar += KeyReceived;

        }
        else
        {
            _primaryKeyboard.KeyUp -= KeyUp;
            _primaryKeyboard.KeyDown -= KeyDown;
            _primaryKeyboard.KeyChar -= KeyReceived;
        }
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
