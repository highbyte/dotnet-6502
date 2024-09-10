using System.Text;
using System.Timers;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.ColorMaps;

namespace Highbyte.DotNet6502.Systems.Commodore64.Utils.BasicAssistant;
public class C64BasicCodingAssistant
{
    private readonly C64 _c64;
    private readonly Func<string, string, string> _getCodeCompletion;

    //private readonly ILogger<C64BasicCodingAssistant> _logger;
    private readonly ILogger _logger;
    public bool IsEnabled { get; private set; } = true;

    private const int DelayAfterKeyPressMilliseconds = 500;
    private const C64Key AcceptSuggestionKey = C64Key.Ctrl;
    private const C64Key SkipSuggestionKey = C64Key.Stop;

    private readonly HashSet<C64Key> _keysTriggeringSuggestion =
    [
        // Letters A-Z 
        C64Key.A, C64Key.B, C64Key.C, C64Key.D, C64Key.E, C64Key.F, C64Key.G, C64Key.H, C64Key.I, C64Key.J, C64Key.K, C64Key.L, C64Key.M, C64Key.N, C64Key.O, C64Key.P, C64Key.Q, C64Key.R, C64Key.S, C64Key.T, C64Key.U, C64Key.V, C64Key.W, C64Key.X, C64Key.Y, C64Key.Z,
        // Numbers 0-9
        C64Key.One, C64Key.Two, C64Key.Three, C64Key.Four, C64Key.Five, C64Key.Six, C64Key.Seven, C64Key.Eight, C64Key.Nine, C64Key.Zero,
        // Special characters
        C64Key.Space, C64Key.Period, C64Key.Comma, C64Key.Colon, C64Key.Semicol, C64Key.Astrix, C64Key.At, C64Key.Equal, C64Key.Minus, C64Key.Plus, C64Key.Slash, C64Key.Lira
    ];

    private const C64Colors SuggestionTextColor = C64Colors.Grey;

    private string _activeSuggestion = string.Empty;
    private int _suggestionStartColumn;
    private byte[] _originalTextBehindSuggestion = Array.Empty<byte>();
    private byte[] _originalTextColorBehindSuggestion = Array.Empty<byte>();

    private readonly System.Timers.Timer _delayAfterKeyPress = new System.Timers.Timer(DelayAfterKeyPressMilliseconds);

    public C64BasicCodingAssistant(C64 c64, Func<string, string, string>? getCodeCompletion, ILoggerFactory loggerFactory)
    {
        _c64 = c64;
        _getCodeCompletion = getCodeCompletion ?? GetFakeCodeCompletion;
        //_logger = loggerFactory.CreateLogger<C64BasicCodingAssistant>();
        _logger = loggerFactory.CreateLogger("C64 code assistant");

        _delayAfterKeyPress.Elapsed += DelayAfterKeyPress_Elapsed;
        _delayAfterKeyPress.Enabled = false; // Timer event will be triggered
        _delayAfterKeyPress.AutoReset = false;  // Only trigger once
    }

    public void Enable()
    {
        IsEnabled = true;
    }

    public void Disable()
    {
        IsEnabled = false;
    }

    public void KeyWasPressed(List<C64Key> keysPressed)
    {
        if (!IsEnabled || keysPressed.Count == 0)
            return;

        _delayAfterKeyPress.Stop();

        if (keysPressed.Contains(AcceptSuggestionKey) && HasActiveSugestion())
        {
            //_logger.LogInformation("Accept suggestion key pressed.");
            RestoreTextColorBehindSuggestionOnScreen();
            _c64.TextPaste.Paste(_activeSuggestion);
            ClearActiveSugestion();
            return;
        }
        if (keysPressed.Contains(SkipSuggestionKey) && HasActiveSugestion())
        {
            //_logger.LogInformation("Skip suggestion key pressed.");
            RestoreTextBehindSuggestionOnScreen();
            RestoreTextColorBehindSuggestionOnScreen();
            ClearActiveSugestion();
            return;
        }

        if (HasActiveSugestion())
            ClearActiveSugestion();

        if (_keysTriggeringSuggestion.Intersect(keysPressed).Any())
            _delayAfterKeyPress.Start();
    }

    private bool HasActiveSugestion()
    {
        return _activeSuggestion != string.Empty;
    }

    private void ClearActiveSugestion()
    {
        _activeSuggestion = string.Empty;
        _originalTextBehindSuggestion = Array.Empty<byte>();
        _originalTextColorBehindSuggestion = Array.Empty<byte>();
    }

    private void SetActiveSugestion(string suggestion)
    {
        _activeSuggestion = suggestion.ToLower();
        _logger.LogInformation($"AI: {suggestion}");

        WriteSuggestionOnScreen(_activeSuggestion);
    }

    private void WriteSuggestionOnScreen(string suggestion)
    {
        var cursorColumnPos = _c64.Mem.FetchByte(0xd3);
        var suggestionLength = suggestion.Length;
        var screenMemLineStart = _c64.Mem.FetchWord(0xd1);
        var colorRamLineStart = _c64.Mem.FetchWord(0xf3);

        _suggestionStartColumn = cursorColumnPos;

        // Save original text that will be overriten by suggestion
        _originalTextBehindSuggestion = _c64.Mem.ReadData((ushort)(screenMemLineStart + _suggestionStartColumn), (ushort)suggestionLength);

        // Save original color that will be overriten by suggestion
        _originalTextColorBehindSuggestion = new byte[suggestionLength];
        for (var i = 0; i < suggestionLength; i++)
        {
            _originalTextColorBehindSuggestion[i] = _c64.ReadIOStorage((ushort)(colorRamLineStart + _suggestionStartColumn));
        }

        // Write suggestion text directly on screen (without moving the cursor)
        for (var i = 0; i < suggestionLength; i++)
        {
            var asciiCode = suggestion[i];
            var petsciiCode = Petscii.CharToPetscii[asciiCode];
            var screenCode = Petscii.PetscIIToC64ScreenCode(petsciiCode);
            _c64.Mem[(ushort)(screenMemLineStart + _suggestionStartColumn + i)] = screenCode;
        }

        // Set color for temporary suggestion (at cursor position + length)
        for (var i = 0; i < suggestionLength; i++)
        {
            _c64.WriteIOStorage((ushort)(colorRamLineStart + _suggestionStartColumn + i), (byte)SuggestionTextColor);
        }
    }

    private void RestoreTextBehindSuggestionOnScreen()
    {
        // Restore original text that previously was overwriten by suggestion
        var screenMemLineStart = _c64.Mem.FetchWord(0xd1);
        _c64.Mem.StoreData((ushort)(screenMemLineStart + _suggestionStartColumn), _originalTextBehindSuggestion);

    }

    private void RestoreTextColorBehindSuggestionOnScreen()
    {
        // Restore original color that previously was overwriten by suggestion
        var colorRamLineStart = _c64.Mem.FetchWord(0xf3);

        for (var i = 0; i < _originalTextColorBehindSuggestion.Length; i++)
        {
            _c64.WriteIOStorage((ushort)(colorRamLineStart + _suggestionStartColumn + i), _originalTextColorBehindSuggestion[i]);
        }
    }

    private void DelayAfterKeyPress_Elapsed(object? sender, ElapsedEventArgs e)
    {
        //_logger.LogInformation("Delay reached after key press elapsed.");
        _delayAfterKeyPress.Stop();

        var suggestion = GetAISuggestion();
        if (!string.IsNullOrEmpty(suggestion))
            SetActiveSugestion(suggestion);
    }

    private string GetAISuggestion()
    {
        GetText(out string textBeforeCursor, out string textAfterCursor);

        return _getCodeCompletion(textBeforeCursor, textAfterCursor);
    }

    private string GetFakeCodeCompletion(string textBeforeCursor, string textAfterCursor)
    {
        if (textBeforeCursor == "10 print" && textAfterCursor == "")
            return "\"hello world!\"";
        return string.Empty;
    }

    private void GetText(out string textBeforeCursor, out string textAfterCursor)
    {
        var screenMemLineStart = _c64.Mem.FetchWord(0xd1);
        var screenLineBytes = _c64.Mem.ReadData(screenMemLineStart, 40);

        var cursorColumnPos = _c64.Mem.FetchByte(0xd3);

        var sb = new StringBuilder();
        for (var i = 0; i < cursorColumnPos; i++)
        {
            var petsciiCode = Petscii.C64ScreenCodeToPetscII(screenLineBytes[i]);
            var asciiCode = Petscii.PetscIIToAscII(petsciiCode);
            sb.Append((char)asciiCode);
        }
        textBeforeCursor = sb.ToString();

        sb.Clear();
        for (var i = cursorColumnPos; i < 40; i++)
        {
            var petsciiCode = Petscii.C64ScreenCodeToPetscII(screenLineBytes[i]);
            var asciiCode = Petscii.PetscIIToAscII(petsciiCode);
            sb.Append((char)asciiCode);
        }
        textAfterCursor = sb.ToString().TrimEnd();
    }
}
