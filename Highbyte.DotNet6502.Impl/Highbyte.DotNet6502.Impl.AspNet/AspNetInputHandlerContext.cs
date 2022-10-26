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

    public bool IsKeyPressed(string key) => KeysPressed.Contains(key);

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
            KeysUp.Add(key);
        }
        if (KeysDown.Contains(key))
            KeysDown.Remove(key);
    }

    public void KeyDown(KeyboardEventArgs e)
    {
        var key = e.Key;
        if (!KeysDown.Contains(key))
        {
            KeysDown.Add(key);
        }
    }

    public void KeyPress(KeyboardEventArgs e)
    {
        var key = e.Key;
        if (!KeysPressed.Contains(key))
        {
            KeysPressed.Add(key);
        }
    }

    public void ClearKeys()
    {
        KeysUp.Clear();
        //KeysDown.Clear(); // KeysDown individual keys are removed in KeyUp event.
        KeysPressed.Clear();
    }

    //public void Cleanup()
    //{
    //}
}
