using System.Numerics;
using Highbyte.DotNet6502.App.SilkNetNative.ConfigUI;
using Highbyte.DotNet6502.App.SilkNetNative.SystemSetup;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using NativeFileDialogSharp;
using TextCopy;

namespace Highbyte.DotNet6502.App.SilkNetNative;

public class SilkNetImGuiMenu : ISilkNetImGuiWindow
{
    private readonly SilkNetHostApp _silkNetHostApp;
    private EmulatorState EmulatorState => _silkNetHostApp.EmulatorState;

    public bool Visible { get; private set; } = true;
    public bool WindowIsFocused { get; private set; }

    private const int POS_X = 10;
    private const int POS_Y = 10;
    private const int WIDTH = 400;
    private const int HEIGHT = 450;
    private static Vector4 s_informationColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
    private static Vector4 s_errorColor = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    private static Vector4 s_warningColor = new Vector4(0.5f, 0.8f, 0.8f, 1);

    private string _screenScaleString = "";
    private int _selectedSystemItem = 0;
    private int _selectedSystemConfigurationVariantItem = 0;

    private bool _audioEnabled;
    private float _audioVolumePercent;
    private readonly ILogger<SilkNetImGuiMenu> _logger;

    private bool _c64KeyboardJoystickEnabled;
    private int _c64KeyboardJoystickIndex;
    private int _c64SelectedJoystickIndex;
    private string[] _c64AvailableJoysticks = [];

    private string SelectedSystemName => _silkNetHostApp.AvailableSystemNames.ToArray()[_selectedSystemItem];
    private string SelectedSystemConfigurationVariant => _silkNetHostApp.AllSelectedSystemConfigurationVariants.ToArray()[_selectedSystemConfigurationVariantItem];

    private SilkNetImGuiC64Config? _c64ConfigUI;
    private SilkNetImGuiGenericComputerConfig? _genericComputerConfigUI;

    private string _lastFileError = "";

    public SilkNetImGuiMenu(SilkNetHostApp silkNetHostApp, string defaultSystemName, bool defaultAudioEnabled, float defaultAudioVolumePercent, ILoggerFactory loggerFactory)
    {
        _silkNetHostApp = silkNetHostApp;
        _screenScaleString = _silkNetHostApp.Scale.ToString();

        _silkNetHostApp.SelectSystem(defaultSystemName).Wait();

        _selectedSystemItem = _silkNetHostApp.AvailableSystemNames.ToList().IndexOf(defaultSystemName);
        _selectedSystemConfigurationVariantItem = 0;

        _audioEnabled = defaultAudioEnabled;
        _audioVolumePercent = defaultAudioVolumePercent;

        _logger = loggerFactory.CreateLogger<SilkNetImGuiMenu>();

        if (_silkNetHostApp.CurrentHostSystemConfig is C64HostConfig)
        {
            InitC64ImGuiWorkingVariables();
        }
    }

    public void PostOnRender()
    {
        ImGui.SetNextWindowSize(new Vector2(WIDTH, HEIGHT), ImGuiCond.Once);
        ImGui.SetNextWindowPos(new Vector2(POS_X, POS_Y), ImGuiCond.Once);
        ImGui.SetNextWindowCollapsed(false, ImGuiCond.Appearing);

        //ImGui.Begin($"DotNet 6502 Emulator", ImGuiWindowFlags.NoResize);
        ImGui.Begin($"DotNet 6502 Emulator");

        ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
        ImGui.Text("System: ");
        ImGui.SameLine();
        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Uninitialized));
        ImGui.PushItemWidth(120);
        if (ImGui.Combo("##systemName", ref _selectedSystemItem, _silkNetHostApp.AvailableSystemNames.ToArray(), _silkNetHostApp.AvailableSystemNames.Count))
        {
            _silkNetHostApp.SelectSystem(SelectedSystemName).Wait();
            _selectedSystemConfigurationVariantItem = 0;
            if (SelectedSystemName == "C64")
            {
                InitC64ImGuiWorkingVariables();
            }
        };
        ImGui.PopItemWidth();
        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.Text("Variant: ");
        ImGui.SameLine();
        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Uninitialized));
        ImGui.PushItemWidth(120);
        if (ImGui.Combo("##configVariant", ref _selectedSystemConfigurationVariantItem, _silkNetHostApp.AllSelectedSystemConfigurationVariants.ToArray(), _silkNetHostApp.AllSelectedSystemConfigurationVariants.Count))
        {
            _silkNetHostApp.SelectSystemConfigurationVariant(SelectedSystemConfigurationVariant).Wait();
            if (SelectedSystemName == "C64")
            {
                InitC64ImGuiWorkingVariables();
            }
        };
        ImGui.PopItemWidth();
        ImGui.EndDisabled();
        ImGui.PopStyleColor();


        ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
        ImGui.Text("Status: ");
        ImGui.SameLine();
        ImGui.Text(EmulatorState.ToString());
        ImGui.PopStyleColor();

        ImGui.BeginDisabled(disabled: !(EmulatorState != EmulatorState.Running && SelectedSystemConfigIsValid()));
        if (ImGui.Button("Start"))
        {
            _silkNetHostApp.Start();
            ImGui.SetWindowFocus(null);
            ImGui.SetWindowCollapsed(true);
            return;
        }
        ImGui.EndDisabled();

        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Running));
        ImGui.SameLine();
        if (ImGui.Button("Pause"))
        {
            _silkNetHostApp.Pause();
        }
        ImGui.EndDisabled();

        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused));
        ImGui.SameLine();
        if (ImGui.Button("Reset"))
        {
            _silkNetHostApp.Reset();
            return;
        }
        ImGui.EndDisabled();

        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused));
        ImGui.SameLine();
        if (ImGui.Button("Stop"))
        {
            _silkNetHostApp.Stop();
            return;
        }
        ImGui.EndDisabled();

        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused));
        if (ImGui.Button("Monitor"))
        {
            _silkNetHostApp.ToggleMonitor();
        }
        ImGui.EndDisabled();

        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused));
        ImGui.SameLine();
        if (ImGui.Button("Stats"))
        {
            _silkNetHostApp.ToggleStatsPanel();
        }
        ImGui.EndDisabled();

        //ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused));
        ImGui.SameLine();
        if (ImGui.Button("Logs"))
        {
            _silkNetHostApp.ToggleLogsPanel();
        }
        //ImGui.EndDisabled();

        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Uninitialized));
        ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
        //ImGui.SetKeyboardFocusHere(0);
        ImGui.PushItemWidth(40);
        if (ImGui.InputText("Scale", ref _screenScaleString, 4))
        {
            if (float.TryParse(_screenScaleString, out float scale))
                _silkNetHostApp.Scale = scale;
        }
        ImGui.PopItemWidth();
        ImGui.PopStyleColor();
        ImGui.EndDisabled();

        // System settings
        if (!string.IsNullOrEmpty(SelectedSystemName))
        {
            // Common audio settings

            ImGui.BeginDisabled(disabled: !(_silkNetHostApp.IsAudioSupported().Result && EmulatorState == EmulatorState.Uninitialized));
            ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
            //ImGui.SetKeyboardFocusHere(0);
            ImGui.PushItemWidth(40);
            if (_silkNetHostApp.IsAudioSupported().Result)
            {
                if (ImGui.Checkbox("Audio enabled (experimental)", ref _audioEnabled))
                {
                    _silkNetHostApp.SetAudioEnabled(_audioEnabled).Wait();
                }
            }
            ImGui.PopStyleColor();
            ImGui.PopItemWidth();
            ImGui.EndDisabled();

            ImGui.BeginDisabled(disabled: !(_silkNetHostApp.IsAudioSupported().Result));
            ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
            //ImGui.SetKeyboardFocusHere(0);
            ImGui.PushItemWidth(40);
            if (_silkNetHostApp.IsAudioSupported().Result && _silkNetHostApp.IsAudioEnabled().Result)
            {
                if (ImGui.SliderFloat("Volume", ref _audioVolumePercent, 0f, 100f, ""))
                {
                    _silkNetHostApp.SetVolumePercent(_audioVolumePercent);
                }
            }
            ImGui.PopStyleColor();
            ImGui.PopItemWidth();
            ImGui.EndDisabled();

            // Common load/save commands
            ImGui.BeginDisabled(disabled: EmulatorState == EmulatorState.Uninitialized);
            if (ImGui.Button("Load & start binary PRG file"))
            {
                bool wasRunning = false;
                if (_silkNetHostApp.EmulatorState == EmulatorState.Running)
                {
                    wasRunning = true;
                    _silkNetHostApp.Pause();
                }

                _lastFileError = "";
                var dialogResult = Dialog.FileOpen(@"prg;*");
                if (dialogResult.IsOk)
                {
                    try
                    {
                        var fileName = dialogResult.Path;
                        BinaryLoader.Load(
                            _silkNetHostApp.CurrentRunningSystem.Mem,
                            fileName,
                            out ushort loadedAtAddress,
                            out ushort fileLength);

                        _silkNetHostApp.CurrentRunningSystem.CPU.PC = loadedAtAddress;

                        _silkNetHostApp.Start();
                        ImGui.SetWindowFocus(null);
                        ImGui.SetWindowCollapsed(true);
                    }
                    catch (Exception ex)
                    {
                        _lastFileError = ex.Message;
                    }
                }
                else
                {
                    if (wasRunning)
                        _silkNetHostApp.Start();
                }
            }
            ImGui.EndDisabled();

            // System specific settings
            switch (SelectedSystemName)
            {
                case "C64":
                    DrawC64Config();
                    break;
                case "Generic":
                    DrawGenericComputerConfig();
                    break;
                default:
                    break;
            }
        }

        if (!string.IsNullOrEmpty(_lastFileError))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, s_errorColor);
            ImGui.Text($"File error: {_lastFileError}");
            ImGui.PopStyleColor();
        }

        ImGui.PushStyleColor(ImGuiCol.Text, s_warningColor);
        ImGui.Text("Toggle menu with F6");
        ImGui.Text("Toggle monitor with F12");
        ImGui.Text("Toggle stats with F11");
        ImGui.Text("Toggle logs with F10");
        ImGui.PopStyleColor();

        WindowIsFocused = ImGui.IsWindowFocused();

        ImGui.End();
    }

    internal void InitC64ImGuiWorkingVariables()
    {
        // One-time init of C64 config working variables for use with ImGui binding.
        var c64HostSystemConfig = (C64HostConfig)_silkNetHostApp.CurrentHostSystemConfig;
        var c64SystemConfig = c64HostSystemConfig.SystemConfig;
        _c64KeyboardJoystickEnabled = c64SystemConfig.KeyboardJoystickEnabled;
        _c64KeyboardJoystickIndex = c64SystemConfig.KeyboardJoystick - 1;

        _c64SelectedJoystickIndex = c64HostSystemConfig.InputConfig.CurrentJoystick - 1;
        _c64AvailableJoysticks = c64HostSystemConfig.InputConfig.AvailableJoysticks.Select(x => x.ToString()).ToArray();
    }

    private void DrawC64Config()
    {

        // Joystick input with keyboard
        //ImGui.BeginDisabled(disabled: EmulatorState != EmulatorState.Uninitialized);
        ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
        //ImGui.SetKeyboardFocusHere(0);
        ImGui.PushItemWidth(40);

        ImGui.Text("Joystick:");
        ImGui.SameLine();
        ImGui.PushItemWidth(35);
        if (ImGui.Combo("##joystick", ref _c64SelectedJoystickIndex, _c64AvailableJoysticks, _c64AvailableJoysticks.Length))
        {
            var c64HostConfig = (C64HostConfig)_silkNetHostApp.CurrentHostSystemConfig;
            c64HostConfig.InputConfig.CurrentJoystick = _c64SelectedJoystickIndex + 1;
        }
        ImGui.PopItemWidth();

        if (ImGui.Checkbox("Keyboard Joystick", ref _c64KeyboardJoystickEnabled))
        {
            var c64SystemConfig = (C64SystemConfig)_silkNetHostApp.CurrentHostSystemConfig.SystemConfig;
            c64SystemConfig.KeyboardJoystickEnabled = _c64KeyboardJoystickEnabled;

            if (EmulatorState != EmulatorState.Uninitialized)
            {
                // System is running, also update the system directly
                C64 c64 = (C64)_silkNetHostApp.CurrentRunningSystem;
                c64.Cia1.Joystick.KeyboardJoystickEnabled = _c64KeyboardJoystickEnabled;
            }
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(!_c64KeyboardJoystickEnabled);
        ImGui.PushItemWidth(35);
        if (ImGui.Combo("##keyboardJoystick", ref _c64KeyboardJoystickIndex, _c64AvailableJoysticks, _c64AvailableJoysticks.Length))
        {
            var c64SystemConfig = (C64SystemConfig)_silkNetHostApp.CurrentHostSystemConfig.SystemConfig;
            c64SystemConfig.KeyboardJoystick = _c64KeyboardJoystickIndex + 1;
            if (EmulatorState != EmulatorState.Uninitialized)
            {
                // System is running, also update the system directly
                C64 c64 = (C64)_silkNetHostApp.CurrentRunningSystem;
                c64.Cia1.Joystick.KeyboardJoystick = _c64KeyboardJoystickIndex + 1;
            }
        }
        ImGui.PopItemWidth();
        ImGui.EndDisabled();
        ImGui.PopStyleColor();
        ImGui.PopItemWidth();

        // Basic load/save commands
        ImGui.BeginDisabled(disabled: EmulatorState == EmulatorState.Uninitialized);
        if (ImGui.Button("Load Basic PRG file"))
        {
            bool wasRunning = false;
            if (_silkNetHostApp.EmulatorState == EmulatorState.Running)
            {
                wasRunning = true;
                _silkNetHostApp.Pause();
            }
            _silkNetHostApp.Pause();
            _lastFileError = "";
            var dialogResult = Dialog.FileOpen(@"prg;*");
            if (dialogResult.IsOk)
            {
                try
                {
                    var fileName = dialogResult.Path;
                    BinaryLoader.Load(
                        _silkNetHostApp.CurrentRunningSystem.Mem,
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
                        ((C64)_silkNetHostApp.CurrentRunningSystem).InitBasicMemoryVariables(loadedAtAddress, fileLength);
                    }
                    ImGui.SetWindowFocus(null);
                    ImGui.SetWindowCollapsed(true);
                }
                catch (Exception ex)
                {
                    _lastFileError = ex.Message;
                }
            }

            if (wasRunning)
                _silkNetHostApp.Start();
        }
        ImGui.EndDisabled();

        ImGui.BeginDisabled(disabled: EmulatorState == EmulatorState.Uninitialized);
        if (ImGui.Button("Save Basic PRG file"))
        {
            bool wasRunning = false;
            if (_silkNetHostApp.EmulatorState == EmulatorState.Running)
            {
                wasRunning = true;
                _silkNetHostApp.Pause();
            }
            _silkNetHostApp.Pause();
            _lastFileError = "";
            var dialogResult = Dialog.FileSave(@"prg;*");
            if (dialogResult.IsOk)
            {
                try
                {
                    var fileName = dialogResult.Path;
                    ushort startAddressValue = C64.BASIC_LOAD_ADDRESS;
                    var endAddressValue = ((C64)_silkNetHostApp.CurrentRunningSystem).GetBasicProgramEndAddress();
                    BinarySaver.Save(
                        _silkNetHostApp.CurrentRunningSystem.Mem,
                        fileName,
                        startAddressValue,
                        endAddressValue,
                        addFileHeaderWithLoadAddress: true);
                    ImGui.SetWindowFocus(null);
                    ImGui.SetWindowCollapsed(true);
                }
                catch (Exception ex)
                {
                    _lastFileError = ex.Message;
                }
            }

            if (wasRunning)
                _silkNetHostApp.Start();
        }
        ImGui.EndDisabled();

        // Toggle D64 disk image (attach/detach)
        ImGui.BeginDisabled(disabled: EmulatorState == EmulatorState.Uninitialized);
        var diskToggleButtonText = IsDiskImageAttached() ? "Detach D64 disk image" : "Attach D64 disk image";
        if (ImGui.Button(diskToggleButtonText))
        {
            if (IsDiskImageAttached())
            {
                // Detach current disk image
                try
                {
                    if (_silkNetHostApp.CurrentRunningSystem is C64 c64)
                    {
                        if (c64.IECBus.GetDeviceByNumber(8) is Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.DiskDrive1541 diskDrive)
                        {
                            diskDrive.RemoveD64DiskImage();
                            _lastFileError = "";
                        }
                        else
                        {
                            _lastFileError = "DiskDrive1541 (device 8) not found.";
                        }
                    }
                    else
                    {
                        _lastFileError = "C64 system not running.";
                    }
                }
                catch (Exception ex)
                {
                    _lastFileError = ex.Message;
                }
            }
            else
            {
                // Attach new disk image
                bool wasRunning = false;
                if (_silkNetHostApp.EmulatorState == EmulatorState.Running)
                {
                    wasRunning = true;
                    _silkNetHostApp.Pause();
                }
                _lastFileError = "";
                var dialogResult = Dialog.FileOpen("d64;*");
                if (dialogResult.IsOk)
                {
                    try
                    {
                        var fileName = dialogResult.Path;
                        // Parse D64 file
                        var d64Image = Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64.D64Parser.ParseD64File(fileName);
                        // Get C64 and DiskDrive1541
                        if (_silkNetHostApp.CurrentRunningSystem is C64 c64)
                        {
                            if (c64.IECBus.GetDeviceByNumber(8) is Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.DiskDrive1541 diskDrive)
                            {
                                diskDrive.SetD64DiskImage(d64Image);
                                // Minimize window after attaching
                                ImGui.SetWindowFocus(null);
                                ImGui.SetWindowCollapsed(true);
                            }
                            else
                            {
                                _lastFileError = "DiskDrive1541 (device 8) not found.";
                            }
                        }
                        else
                        {
                            _lastFileError = "C64 system not running.";
                        }
                    }
                    catch (Exception ex)
                    {
                        _lastFileError = ex.Message;
                    }
                }
                if (wasRunning)
                    _silkNetHostApp.Start();
            }
        }
        ImGui.EndDisabled();

        // C64 copy basic source code
        ImGui.BeginDisabled(disabled: EmulatorState == EmulatorState.Uninitialized);
        if (ImGui.Button("Copy"))
        {
            var c64 = (C64)_silkNetHostApp.CurrentRunningSystem!;
            var sourceCode = c64.BasicTokenParser.GetBasicText();
            ClipboardService.SetText(sourceCode.ToLower());
        }
        ImGui.EndDisabled();

        // C64 paste text
        ImGui.SameLine();
        ImGui.BeginDisabled(disabled: EmulatorState == EmulatorState.Uninitialized);
        if (ImGui.Button("Paste"))
        {
            var c64 = (C64)_silkNetHostApp.CurrentRunningSystem!;
            var text = ClipboardService.GetText();
            if (string.IsNullOrEmpty(text))
                return;
            c64.TextPaste.Paste(text);
        }
        ImGui.EndDisabled();

        // C64 config
        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Uninitialized));
        if (_c64ConfigUI == null)
        {
            _c64ConfigUI = new SilkNetImGuiC64Config(_silkNetHostApp, this);
        }
        if (ImGui.Button("C64 config"))
        {
            _c64ConfigUI.Init((C64HostConfig)_silkNetHostApp.CurrentHostSystemConfig.Clone());
            ImGui.OpenPopup("C64 config");
        }
        ImGui.EndDisabled();
        _c64ConfigUI.PostOnRender("C64 config");

        if (!SelectedSystemConfigIsValid())
        {
            ImGui.PushStyleColor(ImGuiCol.Text, s_errorColor);
            ImGui.TextWrapped($"Config has errors. Press C64 Config button.");
            ImGui.PopStyleColor();
        }
    }

    private void DrawGenericComputerConfig()
    {
        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Uninitialized));

        if (_genericComputerConfigUI == null)
        {
            _genericComputerConfigUI = new SilkNetImGuiGenericComputerConfig(_silkNetHostApp, this);
        }
        if (ImGui.Button("GenericComputer config"))
        {
            _genericComputerConfigUI.Init((GenericComputerHostConfig)_silkNetHostApp.CurrentHostSystemConfig.Clone(), _silkNetHostApp.SelectedSystemConfigurationVariant);
            ImGui.OpenPopup("GenericComputer config");
        }
        ImGui.EndDisabled();
        _genericComputerConfigUI.PostOnRender("GenericComputer config");

        if (!SelectedSystemConfigIsValid())
        {
            ImGui.PushStyleColor(ImGuiCol.Text, s_errorColor);
            ImGui.TextWrapped($"Config has errors. Press GenericComputerConfig button.");
            ImGui.PopStyleColor();
        }
    }

    private bool SelectedSystemConfigIsValid()
    {
        return _silkNetHostApp.IsSystemConfigValid().Result;
    }

    public void Enable()
    {
        Visible = true;
    }

    public void Disable()
    {
        Visible = false;
    }

    private bool IsDiskImageAttached()
    {
        if (EmulatorState == EmulatorState.Uninitialized)
            return false;
        
        if (_silkNetHostApp.CurrentRunningSystem is C64 c64)
        {
            if (c64.IECBus.GetDeviceByNumber(8) is Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.DiskDrive1541 diskDrive)
            {
                return diskDrive.IsDisketteInserted;
            }
        }
        return false;
    }
}
