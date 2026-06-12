using System.Collections.ObjectModel;
using Highbyte.DotNet6502.App.Terminal;
using Highbyte.DotNet6502.Impl.Terminal.Commodore64;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.Download;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TextCopy;

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
    private static readonly IReadOnlyDictionary<string, C64DownloadProgramInfo> PreloadedPrograms =
        new Dictionary<string, C64DownloadProgramInfo>
        {
            ["Digiloi"] = new(
                "Digiloi",
                "https://csdb.dk/release/download.php?id=213381",
                keyboardJoystickEnabled: true,
                keyboardJoystickNumber: 2,
                directLoadPRGName: "*"),

            ["Compunet Reborn"] = new(
                "Compunet Reborn",
                "https://compunet.live/static/compunet-reborn-live.prg",
                downloadType: C64DownloadProgramType.Prg,
                availableInBrowser: true,
                c64Variant: "C64PAL",
                swiftLinkEnabled: true),
            ["Mini Zork"] = new(
                "Mini Zork",
                "https://csdb.dk/release/download.php?id=42919",
                audioEnabled: false,
                directLoadPRGName: "*"),
        };

    private readonly TuiHostApp _host;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly HttpClient _httpClient = new();
    private readonly DropDownList _programDropDown;
    private readonly Button _copyButton;
    private readonly Button _pasteButton;
    private readonly Button _d64Button;
    private readonly Button _loadProgramButton;
    private readonly Button _loadButton;
    private readonly Button _configButton;
    private readonly CheckBox _joystickCheck;
    private readonly Button _joyPortButton;
    private C64AutoLoadAndRun? _c64AutoLoadAndRun;
    private bool _isLoadingPreloadedProgram;

    public string MenuTitle => "C64";

    public int MenuRowCount => 6;

    public View View => this;

    public C64TerminalMenuView(TuiHostApp host, ILoggerFactory loggerFactory)
    {
        _host = host;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(nameof(C64TerminalMenuView));

        _copyButton = new Button { X = 0, Y = 0, Text = "Copy", ShadowStyle = ShadowStyles.None };
        _copyButton.Accepting += (_, e) => { e.Handled = true; CopyBasicSourceCode(); };

        _pasteButton = new Button { X = 12, Y = 0, Text = "Paste", ShadowStyle = ShadowStyles.None };
        _pasteButton.Accepting += (_, e) => { e.Handled = true; PasteText(); };

        _d64Button = new Button { X = 0, Y = 1, Text = "Attach .D64", ShadowStyle = ShadowStyles.None };
        _d64Button.Accepting += (_, e) => { e.Handled = true; ToggleD64Image(); };

        _programDropDown = new DropDownList
        {
            X = 0,
            Y = 2,
            Width = 17,
            Source = new ListWrapper<string>(new ObservableCollection<string>(PreloadedPrograms.Keys.OrderBy(x => x))),
            ReadOnly = true,
            Text = PreloadedPrograms.Keys.OrderBy(x => x).First(),
        };

        _loadProgramButton = new Button { X = 18, Y = 2, Text = "Load", ShadowStyle = ShadowStyles.None };
        _loadProgramButton.Accepting += async (_, e) =>
        {
            e.Handled = true;
            await LoadSelectedProgram();
        };

        _loadButton = new Button { X = 0, Y = 3, Text = "Load .prg…", ShadowStyle = ShadowStyles.None };
        _loadButton.Accepting += (_, e) => { e.Handled = true; LoadBasicPrg(); };

        _configButton = new Button { X = 0, Y = 4, Text = "Config…", ShadowStyle = ShadowStyles.None };
        _configButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            C64ConfigDialog.Show(_host, _logger);
            // The dialog (modal) may have changed the keyboard joystick config on OK; reflect it here.
            SyncJoystickControls();
        };

        // Keyboard joystick toggle + port selector. Mirrors the C64 Config dialog, but applies live to
        // the running C64 (and updates the config so the next (re)build keeps the setting).
        var joyEnabled = CurrentC64Config?.KeyboardJoystickEnabled ?? false;
        var joyPort = CurrentC64Config?.KeyboardJoystick == 1 ? 1 : 2;
        _joystickCheck = new CheckBox
        {
            X = 0,
            Y = 5,
            Text = "Joystick",
            Value = joyEnabled ? CheckState.Checked : CheckState.UnChecked,
        };
        _joyPortButton = new Button
        {
            X = Pos.Right(_joystickCheck) + 1,
            Y = 5,
            Text = $"Port {joyPort}",
            ShadowStyle = ShadowStyles.None,
            Enabled = joyEnabled,
        };
        _joystickCheck.ValueChanged += (_, e) => SetKeyboardJoystickEnabled(e.NewValue == CheckState.Checked);
        _joyPortButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            var current = CurrentC64Config?.KeyboardJoystick == 1 ? 1 : 2;
            SetKeyboardJoystickPort(current == 1 ? 2 : 1);
        };

        Add(_copyButton, _pasteButton, _d64Button, _programDropDown, _loadProgramButton, _loadButton, _configButton,
            _joystickCheck, _joyPortButton);
        UpdateD64ButtonText();
    }

    /// <summary>
    /// Enable/disable the contributed controls to match the current emulator state, mirroring the
    /// rules the other host apps' C64 menus use (e.g. SadConsole's SetControlStates). Disabled
    /// controls render dimmed via the host's UI scheme.
    /// </summary>
    public void RefreshControlStates()
    {
        var state = _host.EmulatorState;
        var running = state == EmulatorState.Running;
        var uninitialized = state == EmulatorState.Uninitialized;

        // Copy/Paste act on a live BASIC session — only meaningful while running.
        _copyButton.Enabled = running;
        _pasteButton.Enabled = running;

        // Disk image + BASIC .prg load act on the built C64 instance — running or paused.
        _d64Button.Enabled = !uninitialized;
        _loadButton.Enabled = !uninitialized;

        // C64 config (ROM paths etc.) can only be changed while the emulator is fully stopped.
        _configButton.Enabled = uninitialized;

        // The preloaded-program download starts/replaces the system itself, so it stays available in
        // any state; it is only blocked while a download is already in flight.
        _programDropDown.Enabled = !_isLoadingPreloadedProgram;
        _loadProgramButton.Enabled = !_isLoadingPreloadedProgram;

        // Keyboard joystick can be configured in any state (applied live while running, stored
        // otherwise); the port button follows the checkbox (see SetKeyboardJoystickEnabled).
    }

    private C64SystemConfig? CurrentC64Config =>
        (_host.CurrentHostSystemConfig as C64TerminalHostConfig)?.SystemConfig;

    /// <summary>
    /// Enable/disable the keyboard joystick. Applies live to a running C64; otherwise updates the
    /// stored config so it takes effect on the next start.
    /// </summary>
    private void SetKeyboardJoystickEnabled(bool enabled)
    {
        if (_host.CurrentHostSystemConfig is not C64TerminalHostConfig hostConfig)
            return;

        hostConfig.SystemConfig.KeyboardJoystickEnabled = enabled;
        if (_host.EmulatorState != EmulatorState.Uninitialized && _host.CurrentRunningSystem is C64 c64)
            c64.Cia1.Joystick.KeyboardJoystickEnabled = enabled;
        else
            _host.UpdateHostSystemConfig(hostConfig);

        _joyPortButton.Enabled = enabled;
        _logger.LogInformation("Keyboard joystick {State}.", enabled ? "enabled" : "disabled");
    }

    /// <summary>
    /// Select the keyboard joystick port (1 or 2). Applies live to a running C64; otherwise updates
    /// the stored config so it takes effect on the next start.
    /// </summary>
    private void SetKeyboardJoystickPort(int port)
    {
        if (_host.CurrentHostSystemConfig is not C64TerminalHostConfig hostConfig)
            return;

        hostConfig.SystemConfig.KeyboardJoystick = port;
        if (_host.EmulatorState != EmulatorState.Uninitialized && _host.CurrentRunningSystem is C64 c64)
            c64.Cia1.Joystick.KeyboardJoystick = port;
        else
            _host.UpdateHostSystemConfig(hostConfig);

        _joyPortButton.Text = $"Port {port}";
        _logger.LogInformation("Keyboard joystick port {Port}.", port);
    }

    private void CopyBasicSourceCode()
    {
        if (_host.CurrentRunningSystem is not C64 c64 || _host.EmulatorState == EmulatorState.Uninitialized)
        {
            _logger.LogInformation("Start the C64 before copying BASIC source.");
            return;
        }

        try
        {
            var basicSourceCode = c64.BasicTokenParser.GetBasicText();
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
        if (_host.CurrentRunningSystem is not C64 c64 || _host.EmulatorState == EmulatorState.Uninitialized)
        {
            _logger.LogInformation("Start the C64 before pasting text.");
            return;
        }

        try
        {
            var text = ClipboardService.GetText();
            if (string.IsNullOrEmpty(text))
                return;

            c64.TextPaste.Paste(text);
            _logger.LogInformation("Queued clipboard text for C64 paste.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error pasting text: {Message}", ex.Message);
        }
    }

    private void ToggleD64Image()
    {
        if (_host.CurrentRunningSystem is not C64 c64 || _host.EmulatorState == EmulatorState.Uninitialized)
        {
            _logger.LogInformation("Start the C64 before attaching a .D64 image.");
            return;
        }

        try
        {
            var diskDrive = GetDiskDrive(c64);
            if (diskDrive == null)
            {
                _logger.LogError("DiskDrive1541 not found on IEC bus.");
                return;
            }

            if (diskDrive.IsDisketteInserted)
            {
                diskDrive.RemoveD64DiskImage();
                _logger.LogInformation("D64 disk image detached.");
                UpdateD64ButtonText();
                return;
            }

            using var dialog = new OpenDialog
            {
                Title = "Attach .D64 image",
                AllowsMultipleSelection = false,
            };
            Application.Run(dialog);

            if (dialog.Canceled || dialog.FilePaths.Count == 0)
                return;

            var path = dialog.FilePaths[0];
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            var d64DiskImage = D64Parser.ParseD64File(path);
            diskDrive.SetD64DiskImage(d64DiskImage);
            _logger.LogInformation("Attached D64 disk image: {File}", Path.GetFileName(path));
            UpdateD64ButtonText();
        }
        catch (Exception ex)
        {
            _logger.LogError("Error toggling D64 disk image: {Message}", ex.Message);
        }
    }

    private async Task LoadSelectedProgram()
    {
        if (_isLoadingPreloadedProgram)
            return;

        var selectedProgram = _programDropDown.Text.ToString() ?? string.Empty;
        if (!PreloadedPrograms.TryGetValue(selectedProgram, out var programInfo))
            return;

        _isLoadingPreloadedProgram = true;
        RefreshControlStates(); // dim the dropdown + Load button while the download is in flight
        _logger.LogInformation("Loading {DisplayName}.", programInfo.DisplayName);

        try
        {
            _c64AutoLoadAndRun ??= CreateAutoLoadAndRun();
            await _c64AutoLoadAndRun.DownloadAndRunProgram(programInfo, ApplyPreloadedProgramConfig);
            UpdateD64ButtonText();
            SyncJoystickControls(); // a preloaded program may have changed the keyboard joystick config
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading {DisplayName}: {Message}", programInfo.DisplayName, ex.Message);
        }
        finally
        {
            _isLoadingPreloadedProgram = false;
            RefreshControlStates(); // re-enable the dropdown + Load button now the download has finished
        }
    }

    private C64AutoLoadAndRun CreateAutoLoadAndRun()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        return new C64AutoLoadAndRun(_loggerFactory, _httpClient, _host);
    }

    private async Task ApplyPreloadedProgramConfig(C64DownloadProgramInfo programInfo)
    {
        if (_host.CurrentHostSystemConfig is not C64TerminalHostConfig currentConfig)
            return;

        var hostConfig = (C64TerminalHostConfig)currentConfig.Clone();
        var systemConfig = hostConfig.SystemConfig;

        systemConfig.KeyboardJoystickEnabled = programInfo.KeyboardJoystickEnabled;
        systemConfig.KeyboardJoystick = programInfo.KeyboardJoystickNumber;
        systemConfig.AudioEnabled = false; // Terminal host has no audio output.
        systemConfig.SwiftLink.Enabled = programInfo.SwiftLinkEnabled;

        await _host.SelectSystemConfigurationVariant(programInfo.C64Variant);
        _host.UpdateHostSystemConfig(hostConfig);
    }

    /// <summary>Refresh the joystick checkbox/port button from the current config (e.g. after a
    /// preloaded program changed it).</summary>
    private void SyncJoystickControls()
    {
        var cfg = CurrentC64Config;
        if (cfg == null)
            return;
        _joystickCheck.Value = cfg.KeyboardJoystickEnabled ? CheckState.Checked : CheckState.UnChecked;
        _joyPortButton.Text = $"Port {(cfg.KeyboardJoystick == 1 ? 1 : 2)}";
        _joyPortButton.Enabled = cfg.KeyboardJoystickEnabled;
    }

    private void UpdateD64ButtonText()
    {
        if (_host.CurrentRunningSystem is C64 c64 && GetDiskDrive(c64)?.IsDisketteInserted == true)
            _d64Button.Text = "Detach D64";
        else
            _d64Button.Text = "Attach .D64";
    }

    private static DiskDrive1541? GetDiskDrive(C64 c64)
        => c64.IECBus.GetDeviceByNumber(8) as DiskDrive1541;

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
