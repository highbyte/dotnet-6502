using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.SadConsole.ConfigUI;
public class C64MenuConsole : ControlsConsole
{
    public const int CONSOLE_WIDTH = USABLE_WIDTH + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    public const int CONSOLE_HEIGHT = USABLE_HEIGHT + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    private const int USABLE_WIDTH = 21;
    private const int USABLE_HEIGHT = 12;

    private readonly SadConsoleHostApp _sadConsoleHostApp;
    private readonly ILogger _logger;

    public C64MenuConsole(SadConsoleHostApp sadConsoleHostApp, ILoggerFactory loggerFactory) : base(CONSOLE_WIDTH, CONSOLE_HEIGHT)
    {
        _sadConsoleHostApp = sadConsoleHostApp;
        _logger = loggerFactory.CreateLogger(typeof(C64MenuConsole).Name);

        Controls.ThemeColors = SadConsoleUISettings.ThemeColors;
        Surface.DefaultBackground = Controls.ThemeColors.ControlHostBackground;
        Surface.DefaultForeground = Controls.ThemeColors.ControlHostForeground;
        Surface.Clear();

        FocusedMode = FocusBehavior.None;

        UseMouse = true;
        UseKeyboard = true;

        DrawUIItems();

        if (SadConsoleUISettings.UI_USE_CONSOLE_BORDER)
            Surface.DrawBox(new Rectangle(0, 0, Width, Height), SadConsoleUISettings.ConsoleDrawBoxBorderParameters);
    }


    private void DrawUIItems()
    {
        // Load Basic
        var c64LoadBasicButton = new Button("Load Basic .prg")
        {
            Name = "c64LoadBasicButton",
            Position = (1, 1),
        };
        c64LoadBasicButton.Click += C64LoadBasicButton_Click;
        Controls.Add(c64LoadBasicButton);

        // Save Basic
        var c64SaveBasicButton = new Button("Save Basic .prg")
        {
            Name = "c64SaveBasicButton",
            Position = (1, c64LoadBasicButton.Bounds.MaxExtentY + 1),
        };
        c64SaveBasicButton.Click += C64SaveBasicButton_Click;
        Controls.Add(c64SaveBasicButton);

        // Load Binary
        var c64LoadBinaryButton = new Button("Load Binary .prg")
        {
            Name = "c64LoadBinaryButton",
            Position = (1, c64SaveBasicButton.Bounds.MaxExtentY + 1),
        };
        c64LoadBinaryButton.Click += C64LoadBinaryButton_Click;
        Controls.Add(c64LoadBinaryButton);


        // Config
        var c64ConfigButton = new Button("C64 Config")
        {
            Name = "c64ConfigButton",
            Position = (1, c64LoadBinaryButton.Bounds.MaxExtentY + 2),
        };
        c64ConfigButton.Click += C64ConfigButton_Click;
        Controls.Add(c64ConfigButton);

        var validationMessageValueLabel = CreateLabelValue(new string(' ', 20), 1, c64ConfigButton.Bounds.MaxExtentY + 2, "validationMessageValueLabel");
        validationMessageValueLabel.TextColor = Controls.GetThemeColors().Red;

        // Helper function to create a label and add it to the console
        Label CreateLabel(string text, int col, int row, string? name = null)
        {
            var labelTemp = new Label(text) { Position = new Point(col, row), Name = name };
            Controls.Add(labelTemp);
            return labelTemp;
        }
        Label CreateLabelValue(string text, int col, int row, string? name = null)
        {
            var labelTemp = new Label(text) { Position = new Point(col, row), TextColor = Controls.GetThemeColors().Title, Name = name };
            Controls.Add(labelTemp);
            return labelTemp;
        }

        // Force OnIsDirtyChanged event which will set control states (see SetControlStates)
        OnIsDirtyChanged();
    }

    private void C64LoadBasicButton_Click(object? sender, EventArgs e)
    {
        bool wasRunning = false;
        if (_sadConsoleHostApp.EmulatorState == EmulatorState.Running)
        {
            wasRunning = true;
            _sadConsoleHostApp.Pause();
        }

        var window = new FilePickerConsole(FilePickerMode.OpenFile, Environment.CurrentDirectory, filter: "*.*");
        window.Center();
        window.Closed += (s2, e2) =>
        {
            if (window.DialogResult)
            {
                try
                {
                    var fileName = window.SelectedFile.FullName;
                    BinaryLoader.Load(
                        _sadConsoleHostApp.CurrentRunningSystem.Mem,
                        fileName,
                        out ushort loadedAtAddress,
                        out ushort fileLength);

                    if (loadedAtAddress != C64.BASIC_LOAD_ADDRESS)
                    {
                        // Probably not a Basic program that was loaded. Don't init BASIC memory variables.
                        _logger.LogWarning($"Warning: Loaded program is not a Basic program, it's expected to load at {C64.BASIC_LOAD_ADDRESS.ToHex()} but was loaded at {loadedAtAddress.ToHex()}");
                    }
                    else
                    {
                        // Init C64 BASIC memory variables
                        ((C64)_sadConsoleHostApp.CurrentRunningSystem).InitBasicMemoryVariables(loadedAtAddress, fileLength);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error loading Basic .prg: {ex.Message}");
                }

                IsDirty = true;
            }

            if (wasRunning)
                _sadConsoleHostApp.Start();
        };
        window.Show(true);
    }

    private void C64SaveBasicButton_Click(object? sender, EventArgs e)
    {
        bool wasRunning = false;
        if (_sadConsoleHostApp.EmulatorState == EmulatorState.Running)
        {
            wasRunning = true;
            _sadConsoleHostApp.Pause();
        }

        var window = new FilePickerConsole(FilePickerMode.SaveFile, Environment.CurrentDirectory);
        window.Center();
        window.Closed += (s2, e2) =>
        {
            if (window.DialogResult)
            {
                try
                {
                    var fileName = window.SelectedFile.FullName;
                    // TODO: Does FilePickerConsole check if file already exists and ask for overwrite? Or do this here?
                    ushort startAddressValue = C64.BASIC_LOAD_ADDRESS;
                    var endAddressValue = ((C64)_sadConsoleHostApp.CurrentRunningSystem).GetBasicProgramEndAddress();
                    BinarySaver.Save(
                        _sadConsoleHostApp.CurrentRunningSystem.Mem,
                        fileName,
                        startAddressValue,
                        endAddressValue,
                        addFileHeaderWithLoadAddress: true);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error saving Basic .prg: {ex.Message}");
                }
            }

            if (wasRunning)
                _sadConsoleHostApp.Start();
        };
        window.Show(true);
    }

    private void C64LoadBinaryButton_Click(object? sender, EventArgs e)
    {
        bool wasRunning = false;
        if (_sadConsoleHostApp.EmulatorState == EmulatorState.Running)
        {
            wasRunning = true;
            _sadConsoleHostApp.Pause();
        }

        var window = new FilePickerConsole(FilePickerMode.OpenFile, Environment.CurrentDirectory, filter: "*.prg");
        window.Center();
        window.Closed += (s2, e2) =>
        {
            if (window.DialogResult)
            {
                try
                {
                    var fileName = window.SelectedFile.FullName;
                    BinaryLoader.Load(
                        _sadConsoleHostApp.CurrentRunningSystem.Mem,
                        fileName,
                        out ushort loadedAtAddress,
                        out ushort fileLength);

                    _sadConsoleHostApp.CurrentRunningSystem.CPU.PC = loadedAtAddress;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error loading Binary .prg: {ex.Message}");
                }

                IsDirty = true;
            }

            if (wasRunning)
                _sadConsoleHostApp.Start();
        };
        window.Show(true);
    }

    private void C64ConfigButton_Click(object sender, EventArgs e)
    {
        var window = new C64ConfigUIConsole(_sadConsoleHostApp);

        window.Center();
        window.Closed += (s2, e2) =>
        {
            if (window.DialogResult)
            {
                IsDirty = true;
                _sadConsoleHostApp.MenuConsole.IsDirty = true;
            }
        };
        window.Show(true);
    }

    protected override void OnIsDirtyChanged()
    {
        if (IsDirty)
            SetControlStates();
    }

    private void SetControlStates()
    {
        var c64LoadBasicButton = Controls["c64LoadBasicButton"];
        c64LoadBasicButton.IsEnabled = _sadConsoleHostApp.EmulatorState != Systems.EmulatorState.Uninitialized;

        var c64SaveBasicButton = Controls["c64SaveBasicButton"];
        c64SaveBasicButton.IsEnabled = _sadConsoleHostApp.EmulatorState != Systems.EmulatorState.Uninitialized;

        var c64LoadBinaryButton = Controls["c64LoadBinaryButton"];
        c64LoadBinaryButton.IsEnabled = _sadConsoleHostApp.EmulatorState != Systems.EmulatorState.Uninitialized;

        var systemComboBox = Controls["c64ConfigButton"];
        systemComboBox.IsEnabled = _sadConsoleHostApp.EmulatorState == Systems.EmulatorState.Uninitialized;

        var validationMessageValueLabel = Controls["validationMessageValueLabel"] as Label;
        (var isOk, var validationErrors) = _sadConsoleHostApp.IsValidConfigWithDetails().Result;
        //validationMessageValueLabel!.DisplayText = isOk ? "" : string.Join(",", validationErrors!);
        validationMessageValueLabel!.DisplayText = isOk ? "" : "Config errors.";
        validationMessageValueLabel!.IsVisible = !isOk;
    }

}
