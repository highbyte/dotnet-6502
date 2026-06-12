using Highbyte.DotNet6502.App.Terminal;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TextCopy;
using Vic20System = Highbyte.DotNet6502.Systems.Vic20.Vic20;

// The terminal host keeps the static Application API (Run for the modal file dialog); it is obsolete
// in Terminal.Gui 2.4.5 but fully functional.
#pragma warning disable CS0618

namespace Highbyte.DotNet6502.App.Terminal.Shell.Vic20;

/// <summary>
/// VIC-20-specific menu control shown in the terminal host's controls column (below the standard
/// controls). Contributed by <see cref="Vic20TerminalShellPlugin"/>. Offers copy/paste of the BASIC
/// listing, loading a BASIC <c>.prg</c>, and a ROM configuration dialog. A trimmed version of the C64
/// menu (the VIC-20 has no SwiftLink, keyboard joystick, or disk image support here).
/// </summary>
public sealed class Vic20TerminalMenuView : View, ITerminalMenuContribution
{
    private readonly TuiHostApp _host;
    private readonly ILogger _logger;
    private readonly Button _copyButton;
    private readonly Button _pasteButton;
    private readonly Button _loadButton;
    private readonly Button _configButton;

    public string MenuTitle => "VIC-20";

    public int MenuRowCount => 3;

    public View View => this;

    public Vic20TerminalMenuView(TuiHostApp host, ILoggerFactory loggerFactory)
    {
        _host = host;
        _logger = loggerFactory.CreateLogger(nameof(Vic20TerminalMenuView));

        _copyButton = new Button { X = 0, Y = 0, Text = "Copy", ShadowStyle = ShadowStyles.None };
        _copyButton.Accepting += (_, e) => { e.Handled = true; CopyBasicSourceCode(); };

        _pasteButton = new Button { X = 12, Y = 0, Text = "Paste", ShadowStyle = ShadowStyles.None };
        _pasteButton.Accepting += (_, e) => { e.Handled = true; PasteText(); };

        _loadButton = new Button { X = 0, Y = 1, Text = "Load .prg…", ShadowStyle = ShadowStyles.None };
        _loadButton.Accepting += (_, e) => { e.Handled = true; LoadBasicPrg(); };

        _configButton = new Button { X = 0, Y = 2, Text = "Config…", ShadowStyle = ShadowStyles.None };
        _configButton.Accepting += (_, e) => { e.Handled = true; Vic20ConfigDialog.Show(_host, _logger); };

        Add(_copyButton, _pasteButton, _loadButton, _configButton);
    }

    /// <summary>
    /// Enable/disable the contributed controls to match the current emulator state, mirroring the C64
    /// menu's rules (a trimmed set: no disk/joystick here). Disabled controls render dimmed via the
    /// host's UI scheme.
    /// </summary>
    public void RefreshControlStates()
    {
        var state = _host.EmulatorState;
        var running = state == EmulatorState.Running;
        var uninitialized = state == EmulatorState.Uninitialized;

        // Copy/Paste act on a live BASIC session — only meaningful while running.
        _copyButton.Enabled = running;
        _pasteButton.Enabled = running;

        // BASIC .prg load acts on the built VIC-20 instance — running or paused.
        _loadButton.Enabled = !uninitialized;

        // ROM config can only be changed while the emulator is fully stopped.
        _configButton.Enabled = uninitialized;
    }

    private void CopyBasicSourceCode()
    {
        if (_host.CurrentRunningSystem is not Vic20System vic20 || _host.EmulatorState == EmulatorState.Uninitialized)
        {
            _logger.LogInformation("Start the VIC-20 before copying BASIC source.");
            return;
        }

        try
        {
            var basicSourceCode = vic20.BasicTokenParser.GetBasicText();
            ClipboardService.SetText(basicSourceCode.ToLower());
            _logger.LogInformation("Copied BASIC source to clipboard.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error copying BASIC source: {Message}", ex.Message);
        }
    }

    private void PasteText()
    {
        if (_host.CurrentRunningSystem is not Vic20System vic20 || _host.EmulatorState == EmulatorState.Uninitialized)
        {
            _logger.LogInformation("Start the VIC-20 before pasting text.");
            return;
        }

        try
        {
            var text = ClipboardService.GetText();
            if (string.IsNullOrEmpty(text))
                return;

            vic20.TextPaste.Paste(text);
            _logger.LogInformation("Queued clipboard text for VIC-20 paste.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error pasting text: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Open a file picker and load the selected BASIC <c>.prg</c> into the running VIC-20's memory,
    /// initialising the BASIC pointers so it can be RUN. The emulator must be running (so a VIC-20
    /// instance exists); it is paused for the load and resumed afterwards.
    /// </summary>
    private void LoadBasicPrg()
    {
        if (_host.CurrentRunningSystem is not Vic20System vic20)
        {
            _logger.LogInformation("Start the VIC-20 before loading a .prg.");
            return;
        }

        var wasRunning = _host.EmulatorState == EmulatorState.Running;
        if (wasRunning)
            _host.Pause();

        try
        {
            using var dialog = new OpenDialog
            {
                Title = "Load BASIC .prg",
                AllowsMultipleSelection = false,
            };
            Application.Run(dialog);

            if (dialog.Canceled || dialog.FilePaths.Count == 0)
                return;

            var path = dialog.FilePaths[0];
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            vic20.Mem.Load(path, out var loadedAtAddress, out var fileLength);

            if (loadedAtAddress != Vic20System.BASIC_LOAD_ADDRESS)
            {
                _logger.LogWarning(
                    "Loaded program is not a BASIC program: expected load address {Expected} but was {Actual}.",
                    Vic20System.BASIC_LOAD_ADDRESS.ToHex(), loadedAtAddress.ToHex());
            }
            else
            {
                vic20.InitBasicMemoryVariables(loadedAtAddress, fileLength);
            }

            _logger.LogInformation("Loaded {File} at {Address} ({Length} bytes).",
                Path.GetFileName(path), loadedAtAddress.ToHex(), fileLength);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error loading .prg: {Message}", ex.Message);
        }
        finally
        {
            if (wasRunning)
                _host.Start().Wait();
        }
    }
}
