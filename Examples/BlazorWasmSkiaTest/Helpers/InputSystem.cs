namespace BlazorWasmSkiaTest.Helpers
{
    /// <summary>
    /// Input handling code based on example: https://github.com/mizrael/BlazorCanvas
    /// </summary>
    public class InputSystem
    {
        private readonly IDictionary<int, ButtonState> _keyboardStates;

        private int _lastKeyDown = 0;
        private int _lastKeyUp = 0;

        private InputSystem()
        {
            _keyboardStates = new Dictionary<int, ButtonState>();
        }

        private static readonly Lazy<InputSystem> s_instance = new Lazy<InputSystem>(new InputSystem());
        public static InputSystem Instance => s_instance.Value;

        public void SetKeyState(int keyCode, ButtonState.States state)
        {
            if (state == ButtonState.States.Down)
                _lastKeyDown = keyCode;
            if (state == ButtonState.States.Up)
                _lastKeyUp = keyCode;

            if (!_keyboardStates.ContainsKey(keyCode))
                _keyboardStates.Add(keyCode, ButtonState.None);
            var oldState = _keyboardStates[keyCode];
            _keyboardStates[keyCode] = new ButtonState(state, oldState.State == ButtonState.States.Down);
        }

        public ButtonState GetKeyState(int keyCode)
        {
            if (!_keyboardStates.ContainsKey(keyCode))
                return ButtonState.None;
            return _keyboardStates[keyCode];
        }
        public int[] GetKeysDown()
        {
            return _keyboardStates.Where(x => x.Value.State == ButtonState.States.Down).Select(x => x.Key).ToArray();
        }

        public int[] GetKeysUp()
        {
            return _keyboardStates.Where(x => x.Value.State == ButtonState.States.Up).Select(x => x.Key).ToArray();
        }

        public int[] GetKeysWasPressed()
        {
            return _keyboardStates.Where(x => x.Value.WasPressed).Select(x => x.Key).ToArray();
        }
    }

    public struct ButtonState
    {
        public ButtonState(States state, bool wasPressed)
        {
            State = state;
            WasPressed = wasPressed;
        }

        public bool WasPressed { get; }
        public States State { get; }

        public enum States
        {
            Up = 0,
            Down = 1
        }

        public static readonly ButtonState None = new ButtonState(States.Up, false);
    }

    public enum Keys
    {
        Up = 38,
        Left = 37,
        Down = 40,
        Right = 39,
        Space = 32,
        LeftCtrl = 17,
        LeftAlt = 18,
    }
}
