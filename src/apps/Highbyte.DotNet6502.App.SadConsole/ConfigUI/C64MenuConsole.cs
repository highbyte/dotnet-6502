using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64;
using Highbyte.DotNet6502.Systems;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;
using Microsoft.Extensions.Logging;
using Highbyte.DotNet6502.Utils;
using TextCopy;
using Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Input;
using Highbyte.DotNet6502.App.SadConsole.SystemSetup;
using Microsoft.Extensions.Configuration;

namespace Highbyte.DotNet6502.App.SadConsole.ConfigUI;
public class C64MenuConsole : ControlsConsole
{
    public const int CONSOLE_WIDTH = USABLE_WIDTH + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    public const int CONSOLE_HEIGHT = USABLE_HEIGHT + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    private const int USABLE_WIDTH = MenuConsole.USABLE_WIDTH;
    private const int USABLE_HEIGHT = 10;

    private readonly SadConsoleHostApp _sadConsoleHostApp;
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;

    private C64SadConsoleInputHandler C64SadConsoleInputHandler => (C64SadConsoleInputHandler)_sadConsoleHostApp.CurrentSystemRunner.InputHandler;

    public C64MenuConsole(
        SadConsoleHostApp sadConsoleHostApp,
        ILoggerFactory loggerFactory,
        IConfiguration configuration
        ) : base(CONSOLE_WIDTH, CONSOLE_HEIGHT)
    {
        _sadConsoleHostApp = sadConsoleHostApp;
        _configuration = configuration;
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
            Surface.DrawBox(new Rectangle(0, 0, Width, Height), SadConsoleUISettings.UIConsoleDrawBoxBorderParameters);
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

        // Load D64 Disk Image
        var c64AttachD64Button = new Button("Attach .D64 disk")
        {
            Name = "c64AttachD64Button",
            Position = (1, c64SaveBasicButton.Bounds.MaxExtentY + 1),
        };
        c64AttachD64Button.Click += C64AttachD64Button_Click;
        Controls.Add(c64AttachD64Button);

        // Copy Basic Source Code
        var c64CopyBasicSourceCodeButton = new Button("Copy")
        {
            Name = "c64CopyBasicSourceCodeButton",
            Position = (1, c64AttachD64Button.Bounds.MaxExtentY + 1),
        };
        c64CopyBasicSourceCodeButton.Click += C64CopyBasicSourceCodeButton_Click!;
        Controls.Add(c64CopyBasicSourceCodeButton);

        // Paste
        var c64PasteTextButton = new Button("Paste")
        {
            Name = "c64PasteTextButton",
            Position = (c64CopyBasicSourceCodeButton.Bounds.MaxExtentX + 9, c64CopyBasicSourceCodeButton.Position.Y),
        };
        c64PasteTextButton.Click += C64PasteTextButton_Click!;
        Controls.Add(c64PasteTextButton);

        // Paste
        var c64aiBasicAssistantCheckbox = new CheckBox("AI Basic (F9)")
        {
            Name = "c64aiBasicAssistantCheckbox",
            Position = (1, c64CopyBasicSourceCodeButton.Bounds.MaxExtentY + 1),
        };
        c64aiBasicAssistantCheckbox.IsSelectedChanged += async (s, e) =>
        {
            await SetBasicAIAssistant(c64aiBasicAssistantCheckbox.IsSelected);
        };
        Controls.Add(c64aiBasicAssistantCheckbox);

        // Config
        var c64ConfigButton = new Button("C64 Config")
        {
            Name = "c64ConfigButton",
            Position = (1, c64aiBasicAssistantCheckbox.Bounds.MaxExtentY + 2),
        };
        c64ConfigButton.Click += C64ConfigButton_Click!;
        Controls.Add(c64ConfigButton);


        var validationMessageValueLabel = CreateLabelValue(new string(' ', 20), 1, c64ConfigButton.Bounds.MaxExtentY + 2, "validationMessageValueLabel");
        validationMessageValueLabel.TextColor = Controls.GetThemeColors().Red;

        // Helper function to create a label and add it to the console
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
                    var fileName = window.SelectedFile!.FullName;
                    BinaryLoader.Load(
                        _sadConsoleHostApp.CurrentRunningSystem!.Mem,
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
            }

            if (wasRunning)
                _sadConsoleHostApp.Start().Wait();

            IsDirty = true;
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
                    var endAddressValue = ((C64)_sadConsoleHostApp.CurrentRunningSystem!).GetBasicProgramEndAddress();
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
                _sadConsoleHostApp.Start().Wait();
        };
        window.Show(true);
    }

    private void C64AttachD64Button_Click(object? sender, EventArgs e)
    {
        bool wasRunning = false;
        if (_sadConsoleHostApp.EmulatorState == EmulatorState.Running)
        {
            wasRunning = true;
            _sadConsoleHostApp.Pause();
        }

        var window = new FilePickerConsole(FilePickerMode.OpenFile, Environment.CurrentDirectory, filter: "*.d64");
        window.Center();
        window.Closed += (s2, e2) =>
        {
            if (window.DialogResult)
            {
                try
                {
                    var fileName = window.SelectedFile!.FullName;

                    // Parse the D64 file
                    var d64DiskImage = D64Parser.ParseD64File(fileName);

                    // Get the C64 system and access the DiskDrive1541
                    var c64 = (C64)_sadConsoleHostApp.CurrentRunningSystem!;
                    if (c64.IECBus.GetDeviceByNumber(8) is DiskDrive1541 diskDrive1541)
                    {
                        // Set the D64 disk image on the disk drive
                        diskDrive1541.SetD64DiskImage(d64DiskImage);
                        _logger.LogInformation($"Loaded D64 disk image: {fileName}");
                    }
                    else
                    {
                        _logger.LogError("DiskDrive1541 not found on IEC bus");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error loading D64 disk image: {ex.Message}");
                }
            }

            if (wasRunning)
                _sadConsoleHostApp.Start().Wait();

            IsDirty = true;
        };
        window.Show(true);
    }

    private void C64ConfigButton_Click(object sender, EventArgs e)
    {
        var window = new C64ConfigUIConsole(_sadConsoleHostApp, _configuration);

        window.Center();
        window.Closed += (s2, e2) =>
        {
            if (window.DialogResult)
            {
                // Update the system config
                _sadConsoleHostApp.UpdateHostSystemConfig(window.C64HostConfig);

                IsDirty = true;
                SetControlStates(); // Setting IsDirty here above does not trigger OnIsDirtyChanged? Call SetControlStates directly here to make sure controls are updated.

                _sadConsoleHostApp.MenuConsole.IsDirty = true;
            }
        };
        window.Show(true);
    }

    private void C64CopyBasicSourceCodeButton_Click(object sender, EventArgs e)
    {
        var c64 = (C64)_sadConsoleHostApp.CurrentRunningSystem!;
        var basicSourceCode = c64.BasicTokenParser.GetBasicText();
        ClipboardService.SetText(basicSourceCode.ToLower());
    }

    private void C64PasteTextButton_Click(object sender, EventArgs e)
    {
        var c64 = (C64)_sadConsoleHostApp.CurrentRunningSystem!;
        var text = ClipboardService.GetText();
        if (string.IsNullOrEmpty(text))
            return;
        c64.TextPaste.Paste(text);
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

        var c64AttachD64Button = Controls["c64AttachD64Button"];
        c64AttachD64Button.IsEnabled = _sadConsoleHostApp.EmulatorState != Systems.EmulatorState.Uninitialized;

        var c64ConfigButton = Controls["c64ConfigButton"];
        c64ConfigButton.IsEnabled = _sadConsoleHostApp.EmulatorState == Systems.EmulatorState.Uninitialized;

        var c64CopyBasicSourceCodeButton = Controls["c64CopyBasicSourceCodeButton"];
        c64CopyBasicSourceCodeButton.IsEnabled = _sadConsoleHostApp.EmulatorState == Systems.EmulatorState.Running;

        var c64PasteTextButton = Controls["c64PasteTextButton"];
        c64PasteTextButton.IsEnabled = _sadConsoleHostApp.EmulatorState == Systems.EmulatorState.Running;

        var c64aiBasicAssistantCheckbox = Controls["c64aiBasicAssistantCheckbox"] as CheckBox;
        if (_sadConsoleHostApp.EmulatorState == Systems.EmulatorState.Running && C64SadConsoleInputHandler.CodingAssistantAvailable)
        {
            c64aiBasicAssistantCheckbox.IsEnabled = true;
            c64aiBasicAssistantCheckbox.IsSelected = C64SadConsoleInputHandler.CodingAssistantEnabled;
        }
        else
        {
            c64aiBasicAssistantCheckbox.IsEnabled = false;
            c64aiBasicAssistantCheckbox.IsSelected = false;
        }


        var validationMessageValueLabel = Controls["validationMessageValueLabel"] as Label;
        (var isOk, var validationErrors) = _sadConsoleHostApp.IsValidConfigWithDetails().Result;
        //validationMessageValueLabel!.DisplayText = isOk ? "" : string.Join(",", validationErrors!);
        validationMessageValueLabel!.DisplayText = isOk ? "" : "Config errors.";
        validationMessageValueLabel!.IsVisible = !isOk;
    }

    public Task ToggleBasicAIAssistant()
    {
        var c64aiBasicAssistantCheckbox = Controls["c64aiBasicAssistantCheckbox"] as CheckBox;
        c64aiBasicAssistantCheckbox.IsSelected = !c64aiBasicAssistantCheckbox.IsSelected;
        return Task.CompletedTask;
    }

    private async Task SetBasicAIAssistant(bool enabled)
    {
        if (_sadConsoleHostApp.EmulatorState != EmulatorState.Running)
            return;
        C64SadConsoleInputHandler.CodingAssistantEnabled = enabled;
        if (enabled)
        {
            await C64SadConsoleInputHandler.CheckCodingAssistantAvailability();
            var test = C64SadConsoleInputHandler.CodingAssistantAvailable;
        }
        ((C64HostConfig)_sadConsoleHostApp.CurrentHostSystemConfig).BasicAIAssistantDefaultEnabled = C64SadConsoleInputHandler.CodingAssistantEnabled;
        IsDirty = true;
    }
}
