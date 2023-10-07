using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.Impl.AspNet;

public class AspNetInputHandlerContext_old : IInputHandlerContext
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
        var key = GetKey(e);
        if (!KeysUp.Contains(key))
        {
            KeysUp.Add(key);
        }
        if (KeysDown.Contains(key))
            KeysDown.Remove(key);
    }

    public void KeyDown(KeyboardEventArgs e)
    {
        var key = GetKey(e);
        if (!KeysDown.Contains(key))
        {
            KeysDown.Add(key);
        }
    }

    public void KeyPress(KeyboardEventArgs e)
    {
        var key = GetKey(e);
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

    private string GetKey(KeyboardEventArgs e)
    {
        if (e.Key == "Control" || e.Key == "Shift" || e.Key == "Alt")
            return $"{e.Key}{GetLeftRightPosition(e)}";
        return e.Key;
    }

    private string GetLeftRightPosition(KeyboardEventArgs e)
    {
        if (e.Location == 1)
            return "Left";
        else if (e.Location == 2)
            return "Right";
        return "";
    }

    public void Cleanup()
    {
    }
}
