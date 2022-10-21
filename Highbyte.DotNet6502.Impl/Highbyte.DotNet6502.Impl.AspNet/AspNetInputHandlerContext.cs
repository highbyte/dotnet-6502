using System.Diagnostics;
using Highbyte.DotNet6502.Systems;
using Microsoft.AspNetCore.Components.Web;

namespace Highbyte.DotNet6502.Impl.AspNet;

public class AspNetInputHandlerContext : IInputHandlerContext
{

    public HashSet<string> KeysUp = new();
    public HashSet<string> KeysDown = new();
    public HashSet<string> KeysPressed = new();

    //public bool Quit { get; private set; }

    //public bool IsKeyPressed(Key key) => _primaryKeyboard.IsKeyPressed(key);

    //public AspNetInputHandlerContext()
    //{
    //}

    public void Init()
    {
        //Quit = false;

        //ListenForKeyboardInput(enabled: true);
    }


    public void KeyUp(KeyboardEventArgs e)
    {
        var key = e.Key;
        if (!KeysUp.Contains(key))
        {
            Debug.WriteLine($"KeyUp captured for frame: {key}");
            KeysUp.Add(key);
        }
    }

    public void KeyDown(KeyboardEventArgs e)
    {
        var key = e.Key;
        if (!KeysDown.Contains(key))
        {
            Debug.WriteLine($"KeyDown captured for frame: {key}");
            KeysDown.Add(key);
        }
    }

    public void KeyPress(KeyboardEventArgs e)
    {
        var key = e.Key;
        if (!KeysPressed.Contains(key))
        {
            Debug.WriteLine($"KeyPress for frame: {key}");
            KeysPressed.Add(key);
        }
    }

    public void ClearKeys()
    {
        KeysUp.Clear();
        KeysDown.Clear();
        //CharactersReceived.Clear();
    }

    //public void Cleanup()
    //{
    //}
}
