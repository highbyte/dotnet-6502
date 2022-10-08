// using Highbyte.DotNet6502.Systems;
// using System.Diagnostics;
// using System.Numerics;
// using Silk.NET.Input;
// using Silk.NET.Windowing;
// using SkiaSharp;

// namespace Highbyte.DotNet6502.App.SkiaNative
// {
//     public class SilkNetInput<TSystem> where TSystem: ISystem
//     {
//         private readonly Func<GRContext, SKCanvas, SystemRunner> _getSystemRunner;
//         private SystemRunner _systemRunner;


//         public static IInputContext s_inputcontext;
//         private IKeyboard _primaryKeyboard;

//         public bool Exit { get; private set; }


//         private HashSet<Key> _keysUp = new();
//         private HashSet<Key> _keysDown = new();
//         private HashSet<char> _keysReceived = new();

//         public bool IsKeyUp(Key key) => _keysUp.Contains(key);
//         public bool IsKeyDown(Key key) => _keysDown.Contains(key);
//         public bool IsKeyReceived(char character) => _keysReceived.Contains(character);

//         public bool IsKeyPressed(Key key) => _keysReceived.Contains((char)key);
//         //public bool IsKeyPressed(Key key) => _primaryKeyboard.IsKeyPressed(key);

//         public SilkNetInput(Func<GRContext, SKCanvas, SystemRunner> getSystemRunner)        
//         {
//             _getSystemRunner = getSystemRunner;
//             Exit = false;
//         }

//         public void Init(IWindow window)
//         {
//             //_systemRunner = _getSystemRunner();

//             s_inputcontext = window.CreateInput();

//             // Silk.NET Input: Keyboard
//             if(s_inputcontext==null)
//                 throw new Exception("Silk.NET Input Context not found.");
//             if(s_inputcontext.Keyboards!=null && s_inputcontext.Keyboards.Count!=0)
//                 _primaryKeyboard = s_inputcontext.Keyboards[0];
//             if(_primaryKeyboard==null)
//                 throw new Exception("Keyboard not found");

//             _primaryKeyboard.KeyUp += KeyUp;
//             _primaryKeyboard.KeyDown += KeyDown;
//             _primaryKeyboard.KeyChar += KeyReceived;
//         }

//         public void Cleanup()
//         {
//             s_inputcontext?.Dispose();
//         }

//         public void KeyUp(IKeyboard keyboard, Key key, int x)
//         {
//             Debug.WriteLine($"KeyUp: {key}");
//             if(!_keysUp.Contains(key))
//                 _keysUp.Add(key);
//         }

//         public void KeyDown(IKeyboard keyboard, Key key, int x)
//         {
//             Debug.WriteLine($"KeyDown: {key}");
//             if(!_keysDown.Contains(key))
//                 _keysDown.Add(key);
//         }

//         public void KeyReceived(IKeyboard keyboard, char character)
//         {
//             Debug.WriteLine($"KeyReceived: {character}");
//             if(!_keysReceived.Contains(character))
//                 _keysReceived.Add(character);
//         }    


//         // Call every frame. 
//         // Direct input such as movement, rotation.
//         // And key up/down events
//         public void HandleInput(double deltaTime)
//         {
//             ProcessKeyPressed(deltaTime);
//             //ProcessKeyUp(deltaTime);
//         }

//         public void FrameDone(double deltaTime)
//         {
//             _keysDown.Clear();
//             _keysUp.Clear();
//             _keysReceived.Clear();
//         }


//         private void ProcessKeyPressed(double deltaTime)
//         {
//             bool anyShiftIsDown = IsKeyPressed(Key.ShiftLeft) || IsKeyPressed(Key.ShiftRight);
//             bool rightCtrlIsDown = IsKeyPressed(Key.ControlRight);
//             bool anyAltIsDown =  IsKeyPressed(Key.AltLeft) || IsKeyPressed(Key.AltRight);

//         }

//         private void ProcessKeyUp(double deltaTime)
//         {
//             foreach(Key keyUp in _keysUp)
//                 KeyUp(keyUp);
//         }

//         // Toggles / selection
//         private void KeyUp(Key key)
//         {
//             bool anyShiftIsDown = IsKeyPressed(Key.ShiftLeft) || IsKeyPressed(Key.ShiftRight);
//             bool anyCtrlIsDown = IsKeyPressed(Key.ControlRight) || IsKeyPressed(Key.ControlLeft);
//             bool anyAltIsDown =  IsKeyPressed(Key.AltLeft) ||  IsKeyPressed(Key.AltRight);

//             if(key == Key.Escape && anyCtrlIsDown)
//             {
//                 Exit=true;
//                 return;
//             }
//         }
//     }
// }
