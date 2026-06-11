using Highbyte.DotNet6502.App.Terminal;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

// The terminal host keeps the static Application API (Run for the modal file dialog); it is obsolete
// in Terminal.Gui 2.4.5 but fully functional. See TuiHostApp for the same suppression.
#pragma warning disable CS0618

namespace Highbyte.DotNet6502.App.Terminal.Shell.Commodore64;

/// <summary>
/// C64-specific menu control shown in the terminal host's controls column (below the standard
/// controls). Contributed by <see cref="C64TerminalShellPlugin"/>. Currently offers loading a BASIC
/// <c>.prg</c> into the running C64 — a system-specific action that has no place in the generic host.
/// </summary>
public sealed class C64TerminalMenuView : View, ITerminalMenuContribution
{
    private readonly TuiHostApp _host;
    private readonly ILogger _logger;

    public string MenuTitle => "C64";

    public int MenuRowCount => 2;

    public View View => this;

    public C64TerminalMenuView(TuiHostApp host, ILoggerFactory loggerFactory)
    {
        _host = host;
        _logger = loggerFactory.CreateLogger(nameof(C64TerminalMenuView));

        var loadButton = new Button { X = 0, Y = 0, Text = "Load .prg…", ShadowStyle = ShadowStyles.None };
        loadButton.Accepting += (_, e) => { e.Handled = true; LoadBasicPrg(); };

        var configButton = new Button { X = 0, Y = 1, Text = "Config…", ShadowStyle = ShadowStyles.None };
        configButton.Accepting += (_, e) => { e.Handled = true; C64ConfigDialog.Show(_host, _logger); };

        Add(loadButton, configButton);
    }

    /// <summary>
    /// Open a file picker and load the selected BASIC <c>.prg</c> into the running C64's memory,
    /// initialising the BASIC pointers so it can be RUN. The emulator must be running (so a C64
    /// instance exists); it is paused for the load and resumed afterwards.
    /// </summary>
    private void LoadBasicPrg()
    {
        if (_host.CurrentRunningSystem is not C64 c64)
        {
            _logger.LogInformation("Start the C64 before loading a .prg.");
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

            c64.Mem.Load(path, out var loadedAtAddress, out var fileLength);

            if (loadedAtAddress != C64.BASIC_LOAD_ADDRESS)
            {
                _logger.LogWarning(
                    "Loaded program is not a BASIC program: expected load address {Expected} but was {Actual}.",
                    C64.BASIC_LOAD_ADDRESS.ToHex(), loadedAtAddress.ToHex());
            }
            else
            {
                c64.InitBasicMemoryVariables(loadedAtAddress, fileLength);
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
