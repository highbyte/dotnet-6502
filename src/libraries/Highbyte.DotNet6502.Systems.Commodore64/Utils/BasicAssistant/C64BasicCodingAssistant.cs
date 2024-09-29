using System.Text;
using System.Timers;
using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.ColorMaps;

namespace Highbyte.DotNet6502.Systems.Commodore64.Utils.BasicAssistant;
public class C64BasicCodingAssistant
{
    private readonly C64 _c64;
    private readonly ICodeSuggestion _codeSuggestion;

    //private readonly ILogger<C64BasicCodingAssistant> _logger;
    private readonly ILogger _logger;

    private const int DelayAfterKeyPressMilliseconds = 500;
    private const C64Key AcceptSuggestionKey = C64Key.Ctrl;
    //private const C64Key SkipSuggestionKey = C64Key.Stop;

    private readonly HashSet<C64Key> _keysTriggeringSuggestion =
    [
        // Letters A-Z 
        C64Key.A, C64Key.B, C64Key.C, C64Key.D, C64Key.E, C64Key.F, C64Key.G, C64Key.H, C64Key.I, C64Key.J, C64Key.K, C64Key.L, C64Key.M, C64Key.N, C64Key.O, C64Key.P, C64Key.Q, C64Key.R, C64Key.S, C64Key.T, C64Key.U, C64Key.V, C64Key.W, C64Key.X, C64Key.Y, C64Key.Z,
        // Numbers 0-9
        C64Key.One, C64Key.Two, C64Key.Three, C64Key.Four, C64Key.Five, C64Key.Six, C64Key.Seven, C64Key.Eight, C64Key.Nine, C64Key.Zero,
        // Other characters
        C64Key.Return, C64Key.Space, C64Key.Period, C64Key.Comma, C64Key.Colon, C64Key.Semicol, C64Key.Astrix, C64Key.At, C64Key.Equal, C64Key.Minus, C64Key.Plus, C64Key.Slash, C64Key.Lira
    ];

    private const C64Colors SuggestionTextColor = C64Colors.Grey;

    private byte _triggerScreenCursorRow;
    private byte _triggerScreenCursorColumn;

    private string _activeSuggestion = string.Empty;
    private int _suggestionStartColumn;
    private ushort _suggestionScreenLineTextStartAddress;
    private ushort _suggestionScreenLineColorStartAddress;
    private byte[] _originalTextBehindSuggestion = Array.Empty<byte>();
    private byte[] _originalTextColorBehindSuggestion = Array.Empty<byte>();
    private int _lastSuggestionBasicLineNumber;

    private readonly System.Timers.Timer _delayAfterKeyPress = new System.Timers.Timer(DelayAfterKeyPressMilliseconds);

    public const string CODE_COMPLETION_LANGUAGE_DESCRIPTION = "Commodore 64 Basic";

    public async Task CheckAvailability()
    {
        await _codeSuggestion.CheckAvailability();
    }
    public bool IsAvailable => _codeSuggestion.IsAvailable;
    public string? LastError => _codeSuggestion.LastError;

    public C64BasicCodingAssistant(C64 c64, ICodeSuggestion codeSuggestion, ILoggerFactory loggerFactory)
    {
        _c64 = c64;
        _codeSuggestion = codeSuggestion;
        //_logger = loggerFactory.CreateLogger<C64BasicCodingAssistant>();
        _logger = loggerFactory.CreateLogger("C64 code assistant");

        _delayAfterKeyPress.Elapsed += DelayAfterKeyPress_Elapsed;
        _delayAfterKeyPress.Enabled = false; // Timer event will be triggered
        _delayAfterKeyPress.AutoReset = false;  // Only trigger once
    }

    public void KeyWasPressed(List<C64Key> keysPressed)
    {
        if (_codeSuggestion.IsAvailable == false)
            return;

        if (keysPressed.Count == 0)
            return;

        _delayAfterKeyPress.Stop();

        if (HasActiveSugestion())
        {
            if (keysPressed.Contains(AcceptSuggestionKey))
            {
                //_logger.LogInformation("Accept suggestion key pressed.");
                RestoreTextColorBehindSuggestionOnScreen();
                _c64.TextPaste.Paste(_activeSuggestion);
                ClearActiveSugestion();
                return;
            }

            // If any other key is pressed, remove suggestion
            RestoreTextBehindSuggestionOnScreen();
            RestoreTextColorBehindSuggestionOnScreen();
            ClearActiveSugestion();
            return;
        }


        if (keysPressed.Contains(C64Key.Return))
        {
            // Remember basic line number that the user pressed Return on
            // Basic line number range is 0 to 63999
            var currentScreenLineText = GetAscIIStringFromScreenCodeBytes(GetCurrentScreenLineTextBytes(0, 6)); // First 6 characters should be enough for Basic line number?
            var foundBasicLineNumber = TryGetBasicLineNumberFromScreenText(currentScreenLineText, out int basicLineNumber, out _);
            if (foundBasicLineNumber)
                _lastSuggestionBasicLineNumber = basicLineNumber;
        }

        // If key that triggers suggestion is pressed, start delay timer for new suggestion
        if (_keysTriggeringSuggestion.Intersect(keysPressed).Any())
        {
            _triggerScreenCursorColumn = _c64.Mem.FetchByte(0xd3);
            _triggerScreenCursorRow = _c64.Mem.FetchByte(0xd6);

            _delayAfterKeyPress.Start();
        }
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
        //_logger.LogInformation($"Active suggestion: {suggestion}");

        WriteSuggestionOnScreen(_activeSuggestion);
    }

    private void WriteSuggestionOnScreen(string suggestion)
    {
        _suggestionStartColumn = _c64.Mem.FetchByte(0xd3);
        _suggestionScreenLineTextStartAddress = _c64.Mem.FetchWord(0xd1);
        _suggestionScreenLineColorStartAddress = _c64.Mem.FetchWord(0xf3);

        var cursorIsShowingReverseCharacter = _c64.Mem.FetchByte(0xcf) == 1; // 1 = cursor on, showing inverted character

        var suggestionLength = suggestion.Length;

        // Save original text that will be overriten by suggestion
        _originalTextBehindSuggestion = GetCurrentScreenLineTextBytes(_suggestionStartColumn, suggestionLength);

        // First copied character is the one under cursor. Remove reverse mode on that character if cursor currently is in reverse mode.
        var screenCodeUnderCursor = _originalTextBehindSuggestion[0];
        if (cursorIsShowingReverseCharacter && screenCodeUnderCursor > 128)
            _originalTextBehindSuggestion[0] = (byte)(screenCodeUnderCursor - 128);

        // Save original color that will be overriten by suggestion
        _originalTextColorBehindSuggestion = GetCurrentScreenLineColorBytes(_suggestionStartColumn, suggestionLength);

        // Write suggestion text directly on screen (without moving the cursor)
        WriteAscIIStringOnCurrentScreenLine(suggestion, _suggestionStartColumn);
        // Set color for temporary suggestion (at cursor position + length)
        for (var i = 0; i < suggestionLength; i++)
        {
            WriteCurrentScreenLineCharacterColor(_suggestionStartColumn + i, (byte)SuggestionTextColor);
        }
    }

    private void RestoreTextBehindSuggestionOnScreen()
    {
        // Restore original text that previously was overwriten by suggestion
        _c64.Mem.StoreData((ushort)(_suggestionScreenLineTextStartAddress + _suggestionStartColumn), _originalTextBehindSuggestion);
        _c64.Mem[0xce] = _originalTextBehindSuggestion[0]; // 0xce = Screen code of character under cursor.
        _c64.Mem[0xcf] = 0; // 0xcf = Cursor phase switch. 0 = off (original character), 1 = on (reverse character)
    }

    private void RestoreTextColorBehindSuggestionOnScreen()
    {
        // Restore original color that previously was overwriten by suggestion
        for (var i = 0; i < _originalTextColorBehindSuggestion.Length; i++)
        {
            _c64.WriteIOStorage((ushort)(_suggestionScreenLineColorStartAddress + _suggestionStartColumn + i), _originalTextColorBehindSuggestion[i]);
        }

        _c64.Mem[0x0287] = _originalTextColorBehindSuggestion[0]; // 0x0287 = Color of character under cursor. 
    }

    private async void DelayAfterKeyPress_Elapsed(object? sender, ElapsedEventArgs e)
    {
        //_logger.LogInformation("Delay reached after key press elapsed.");
        _delayAfterKeyPress.Stop();

        // Check so user haven't moved cursor since the delay timer started and we queried AI
        var currentScreenCursorColumn = _c64.Mem.FetchByte(0xd3);
        var currentScreenCursorRow = _c64.Mem.FetchByte(0xd6);
        if (currentScreenCursorRow != _triggerScreenCursorRow || currentScreenCursorColumn != _triggerScreenCursorColumn)
        {
            _logger.LogDebug("Cursor moved between timer start and end.");
            return;
        }

        var aiCallScreenCursorColumn = _c64.Mem.FetchByte(0xd3);
        var aiCallScreenCursorRow = _c64.Mem.FetchByte(0xd6);

        var suggestion = await GetAISuggestion();
        if (string.IsNullOrEmpty(suggestion))
            return;

        // Check so user haven't moved cursor since query was sent to AI code completion endpoint
        currentScreenCursorColumn = _c64.Mem.FetchByte(0xd3);
        currentScreenCursorRow = _c64.Mem.FetchByte(0xd6);
        if (currentScreenCursorRow != aiCallScreenCursorRow || currentScreenCursorColumn != aiCallScreenCursorColumn)
        {
            _logger.LogDebug("Cursor moved between AI call and response");
            return;
        }
        SetActiveSugestion(suggestion);
    }

    private async Task<string> GetAISuggestion()
    {
        bool performAISuggestion = GetText(out string textBeforeCursor, out string textAfterCursor);
        if (!performAISuggestion)
            return string.Empty;

        _logger.LogInformation($"AI Query: text before: {textBeforeCursor}");
        _logger.LogInformation($"AI Query: text after:  {textAfterCursor}");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await _codeSuggestion.GetInsertionSuggestionAsync(textBeforeCursor, textAfterCursor);
        sw.Stop();
        _logger.LogInformation($"AI response: {result}");
        _logger.LogInformation($"AI response time: {sw.ElapsedMilliseconds} ms");
        return result;
    }

    private bool GetText(out string textBeforeCursor, out string textAfterCursor)
    {
        textBeforeCursor = string.Empty;
        textAfterCursor = string.Empty;

        // Check if current screen line has any text before the cursor
        string currentScreenLineTextUpTilCursor;
        int findBasicLinesUpTilLineNumber = 0;
        var cursorColumnPos = _c64.Mem.FetchByte(0xd3);
        if (cursorColumnPos > 0)
        {
            var screenLineBytes = GetCurrentScreenLineTextBytes(0, cursorColumnPos);
            currentScreenLineTextUpTilCursor = GetAscIIStringFromScreenCodeBytes(screenLineBytes);

            // If current line does not contain any text, skip doing suggestion.
            if (currentScreenLineTextUpTilCursor == string.Empty || currentScreenLineTextUpTilCursor.Trim() == string.Empty)
            {
                return false;
            }

            // Try to find Basic line number at start of line
            var foundBasicLineNumber = TryGetBasicLineNumberFromScreenText(currentScreenLineTextUpTilCursor, out int currentScreenLineBasicLineNumber, out string currentScreenLineWithoutLineNumber);
            if (foundBasicLineNumber)
            {
                findBasicLinesUpTilLineNumber = currentScreenLineBasicLineNumber - 1;
                _lastSuggestionBasicLineNumber = currentScreenLineBasicLineNumber;
            }
            else
            {
                // No Basic line number was found on current screen line, skip doing any suggestion.
                return false;
            }

            var isRemark = IsBasicLineARemarkStatement(currentScreenLineWithoutLineNumber);
            if (isRemark)
            {
                // Skip doing suggestion on remark lines
                return false;
            }
        }
        else
        {
            // Cursor at first column, nothing to suggest
            return false;
        }

        // Get entire Basic program from memory up til before the detected basicLineNumber we are currently on
        // (or if no basic line number is found, to til the last suggestion line number)
        var basicProgram = _c64.BasicTokenParser.GetBasicText(spaceAfterLineNumber: true);
        var basicLines = basicProgram.Split(Environment.NewLine);

        StringBuilder textBeforeCursorSb = new();
        for (var i = 0; i < basicLines.Length; i++)
        {
            if (!string.IsNullOrEmpty(basicLines[i]))
            {
                var foundBasicProgramLineNumber = TryGetBasicLineNumberFromScreenText(basicLines[i], out int basicLineNumber, out _);
                if (foundBasicProgramLineNumber && basicLineNumber > findBasicLinesUpTilLineNumber)
                    break;
                textBeforeCursorSb.AppendLine(basicLines[i]);
            }
        }

        // Append the current screen line to total source code before cursor
        textBeforeCursorSb.Append(currentScreenLineTextUpTilCursor);

        // Add text after current position
        StringBuilder textAfterCursorSb = new();
        // Note: Adding the text after the cursor position current screen line may not be optimal, as it may not be relevant.
        // Add rest of text on current screen line after the curosr
        //for (var i = cursorColumnPos; i < 40; i++)
        //{
        //    var petsciiCode = Petscii.C64ScreenCodeToPetscII(screenLineBytes[i]);
        //    var asciiCode = Petscii.PetscIIToAscII(petsciiCode);
        //    textAfterCursorSb.Append((char)asciiCode);
        //}

        // Hack: If there is nothing after cursor, add a newline to make sure there is something to query AI with
        if (textAfterCursorSb.Length == 0)
        {
            textBeforeCursorSb.AppendLine();
        }
        //  TODO: Build textAfterCursor to include rest of basic lines

        // Set out parameters
        textBeforeCursor = textBeforeCursorSb.ToString();
        textAfterCursor = textAfterCursorSb.ToString().TrimEnd();

        return true;
    }

    private bool IsBasicLineARemarkStatement(string currentScreenLineTextUpTilCursor)
    {
        return currentScreenLineTextUpTilCursor.TrimStart().StartsWith("REM", StringComparison.OrdinalIgnoreCase);
    }

    private void WriteAscIIStringOnCurrentScreenLine(string text, int col)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var asciiCode = text[i];
            if (!Petscii.CharToPetscii.ContainsKey(asciiCode))
                continue;
            var petsciiCode = Petscii.CharToPetscii[asciiCode];
            var screenCode = Petscii.PetscIIToC64ScreenCode(petsciiCode);
            WriteCurrentScreenLineCharacter(col + i, screenCode);
        }
    }

    private bool TryGetBasicLineNumberFromScreenText(string currentScreenLine, out int basicLineNumber, out string lineWithoutLineNumber)
    {
        currentScreenLine = currentScreenLine.TrimStart();
        var parts = currentScreenLine.Split(' ');
        var parseOk = int.TryParse(parts[0].Trim(), out basicLineNumber);

        if (!parseOk)
        {
            basicLineNumber = -1;
            lineWithoutLineNumber = currentScreenLine;
        }
        else
        {
            lineWithoutLineNumber = string.Join(' ', parts.Skip(1));
        }
        return parseOk;
    }

    private string GetAscIIStringFromScreenCodeBytes(byte[] screenCodeBytes, int start = 0, int length = -1)
    {
        if (length == -1)
            length = screenCodeBytes.Length;

        if (start < 0 || start >= screenCodeBytes.Length)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (length < 0 || start + length > screenCodeBytes.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        var sb = new StringBuilder();

        for (var i = start; i < start + length; i++)
        {
            var petsciiCode = Petscii.C64ScreenCodeToPetscII(screenCodeBytes[i]);
            var asciiCode = Petscii.PetscIIToAscII(petsciiCode);
            sb.Append((char)asciiCode);
        }
        return sb.ToString();
    }

    private byte[] GetCurrentScreenLineTextBytes(int startCol, int length)
    {
        var screenMemLineStart = _c64.Mem.FetchWord(0xd1);
        return GetRAMBytes((ushort)(screenMemLineStart + startCol), (ushort)length);
    }
    private byte[] GetRAMBytes(ushort start, ushort length = 40)
    {
        return _c64.Mem.ReadData(start, length);
    }

    private void WriteCurrentScreenLineCharacter(int col, byte screenCode)
    {
        var screenMemLineStart = _c64.Mem.FetchWord(0xd1);
        WriteRAMByte((ushort)(screenMemLineStart + col), screenCode);
    }

    private void WriteRAMByte(ushort address, byte data)
    {
        _c64.Mem[address] = data;
    }


    private byte[] GetCurrentScreenLineColorBytes(int startCol = 0, int length = 40)
    {
        var colorRamLineStart = _c64.Mem.FetchWord(0xf3);
        return GetIOBytes((ushort)(colorRamLineStart + startCol), length);
    }
    private byte[] GetIOBytes(ushort start = 0, int length = 40)
    {
        var data = new byte[length];
        for (var i = 0; i < length; i++)
        {
            data[i] = _c64.ReadIOStorage(start);
        }
        return data;
    }
    private void WriteCurrentScreenLineCharacterColor(int col, byte color)
    {
        var colorRamLineStart = _c64.Mem.FetchWord(0xf3);
        WriteIOByte((ushort)(colorRamLineStart + col), color);
    }
    private void WriteIOByte(ushort address, byte data)
    {
        _c64.WriteIOStorage(address, data);
    }
}
