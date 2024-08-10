using System.Numerics;
using AutoMapper;
using Highbyte.DotNet6502.App.SilkNetNative.ConfigUI;
using Highbyte.DotNet6502.App.SilkNetNative.SystemSetup;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using NativeFileDialogSharp;

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
    private const int HEIGHT = 430;
    private static Vector4 s_informationColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
    private static Vector4 s_errorColor = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    private static Vector4 s_warningColor = new Vector4(0.5f, 0.8f, 0.8f, 1);

    private string _screenScaleString = "";
    private int _selectedSystemItem = 0;

    private bool _audioEnabled;
    private float _audioVolumePercent;
    private readonly IMapper _mapper;
    private readonly ILogger<SilkNetImGuiMenu> _logger;
    public bool C64KeyboardJoystickEnabled;
    public int C64KeyboardJoystick;
    public int C64SelectedJoystick;
    public string[] C64AvailableJoysticks = [];

    private string SelectedSystemName => _silkNetHostApp.AvailableSystemNames.ToArray()[_selectedSystemItem];

    private ISystemConfig _originalSystemConfig = default!;
    private IHostSystemConfig _originalHostSystemConfig = default!;

    private SilkNetImGuiC64Config? _c64ConfigUI;
    private SilkNetImGuiGenericComputerConfig? _genericComputerConfigUI;

    private string _lastFileError = "";

    public SilkNetImGuiMenu(SilkNetHostApp silkNetHostApp, string defaultSystemName, bool defaultAudioEnabled, float defaultAudioVolumePercent, IMapper mapper, ILoggerFactory loggerFactory)
    {
        _silkNetHostApp = silkNetHostApp;
        _screenScaleString = _silkNetHostApp.Scale.ToString();

        _selectedSystemItem = _silkNetHostApp.AvailableSystemNames.ToList().IndexOf(defaultSystemName);

        _audioEnabled = defaultAudioEnabled;
        _audioVolumePercent = defaultAudioVolumePercent;

        _mapper = mapper;
        _logger = loggerFactory.CreateLogger<SilkNetImGuiMenu>();

        ISystemConfig systemConfig = GetSelectedSystemConfigClone();
        if (systemConfig is C64Config c64Config)
        {
            C64KeyboardJoystickEnabled = c64Config.KeyboardJoystickEnabled;
            C64KeyboardJoystick = c64Config.KeyboardJoystick - 1;

            var c64HostSystemConfig = (C64HostConfig)GetSelectedSystemHostConfig();
            C64SelectedJoystick = c64HostSystemConfig.InputConfig.CurrentJoystick - 1;
            C64AvailableJoysticks = c64HostSystemConfig.InputConfig.AvailableJoysticks.Select(x => x.ToString()).ToArray();
        }
    }

    public void PostOnRender()
    {
        ImGui.SetNextWindowSize(new Vector2(WIDTH, HEIGHT), ImGuiCond.Once);
        ImGui.SetNextWindowPos(new Vector2(POS_X, POS_Y), ImGuiCond.Once);

        //ImGui.Begin($"DotNet 6502 Emulator", ImGuiWindowFlags.NoResize);
        ImGui.Begin($"DotNet 6502 Emulator");

        ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
        ImGui.Text("System: ");
        ImGui.SameLine();
        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Uninitialized));
        ImGui.PushItemWidth(120);
        if (ImGui.Combo("", ref _selectedSystemItem, _silkNetHostApp.AvailableSystemNames.ToArray(), _silkNetHostApp.AvailableSystemNames.Count))
        {
            _silkNetHostApp.SelectSystem(SelectedSystemName);
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

            ImGui.BeginDisabled(disabled: !(_silkNetHostApp.IsAudioSupported && EmulatorState == EmulatorState.Uninitialized));
            ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
            //ImGui.SetKeyboardFocusHere(0);
            ImGui.PushItemWidth(40);
            if (_silkNetHostApp.IsAudioSupported)
            {
                if (ImGui.Checkbox("Audio enabled (experimental)", ref _audioEnabled))
                {
                    _silkNetHostApp.IsAudioEnabled = _audioEnabled;
                }
            }
            ImGui.PopStyleColor();
            ImGui.PopItemWidth();
            ImGui.EndDisabled();

            ImGui.BeginDisabled(disabled: !(_silkNetHostApp.IsAudioSupported));
            ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
            //ImGui.SetKeyboardFocusHere(0);
            ImGui.PushItemWidth(40);
            if (_silkNetHostApp.IsAudioSupported && _silkNetHostApp.IsAudioEnabled)
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
        if (ImGui.Combo("##joystick", ref C64SelectedJoystick, C64AvailableJoysticks, C64AvailableJoysticks.Length))
        {
            var c64HostConfig = (C64HostConfig)GetSelectedSystemHostConfig();
            c64HostConfig.InputConfig.CurrentJoystick = C64SelectedJoystick + 1;
            UpdateCurrentHostSystemConfig(c64HostConfig);
        }
        ImGui.PopItemWidth();

        if (ImGui.Checkbox("Keyboard Joystick", ref C64KeyboardJoystickEnabled))
        {
            var c64Config = (C64Config)GetSelectedSystemConfigClone();
            c64Config.KeyboardJoystickEnabled = C64KeyboardJoystickEnabled;
            UpdateCurrentSystemConfig(c64Config);

            if (EmulatorState != EmulatorState.Uninitialized)
            {
                // System is running, also update the system directly
                C64 c64 = (C64)_silkNetHostApp.CurrentRunningSystem;
                c64.Cia.Joystick.KeyboardJoystickEnabled = C64KeyboardJoystickEnabled;
            }
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(!C64KeyboardJoystickEnabled);
        ImGui.PushItemWidth(35);
        if (ImGui.Combo("##keyboardJoystick", ref C64KeyboardJoystick, C64AvailableJoysticks, C64AvailableJoysticks.Length))
        {
            var c64Config = (C64Config)GetSelectedSystemConfigClone();
            c64Config.KeyboardJoystick = C64KeyboardJoystick + 1;
            UpdateCurrentSystemConfig(c64Config);
            if (EmulatorState != EmulatorState.Uninitialized)
            {
                // System is running, also update the system directly
                C64 c64 = (C64)_silkNetHostApp.CurrentRunningSystem;
                c64.Cia.Joystick.KeyboardJoystick = C64KeyboardJoystick + 1;
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

        // C64 config
        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Uninitialized));
        if (_c64ConfigUI == null)
        {
            _c64ConfigUI = new SilkNetImGuiC64Config(this);
        }
        if (ImGui.Button("C64 config"))
        {
            RememberOriginalHostConfig();
            // TODO: Clone result of GetSelectedSystemHostConfig() here?
            _c64ConfigUI.Init((C64Config)GetSelectedSystemConfigClone(), (C64HostConfig)GetSelectedSystemHostConfig());
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
            _genericComputerConfigUI = new SilkNetImGuiGenericComputerConfig(this);
        }
        if (ImGui.Button("GenericComputer config"))
        {
            RememberOriginalHostConfig();
            _genericComputerConfigUI.Init((GenericComputerConfig)GetSelectedSystemConfigClone(), (GenericComputerHostConfig)GetSelectedSystemHostConfig());
            ImGui.OpenPopup("GenericComputer config");
        }
        ImGui.EndDisabled();
        _genericComputerConfigUI.PostOnRender("GenericComputer config");

        if (!_genericComputerConfigUI.IsValidConfig)
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
    internal ISystemConfig GetSelectedSystemConfigClone()
    {
        return _silkNetHostApp.GetSystemConfigClone().Result;
    }
    internal IHostSystemConfig GetSelectedSystemHostConfig()
    {
        return _silkNetHostApp.GetHostSystemConfig();
    }

    internal void RememberOriginalHostConfig()
    {
        _originalHostSystemConfig = (IHostSystemConfig)GetSelectedSystemHostConfig().Clone();
    }
    internal void RestoreOriginalHostConfig()
    {
        // Update the existing host system config, it is referenced from different objects (thus we cannot replace it with a new one).
        var orgHostSystemConfig = _silkNetHostApp.GetHostSystemConfig();
        _mapper.Map(_originalHostSystemConfig, orgHostSystemConfig);
    }

    internal void UpdateCurrentSystemConfig(ISystemConfig config)
    {
        if (config is C64Config c64Config)
        {
            C64KeyboardJoystick = c64Config.KeyboardJoystick - 1;
        }

        // Update the system config
        _silkNetHostApp.UpdateSystemConfig(config);
    }

    internal void UpdateCurrentHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        if (hostSystemConfig is C64HostConfig c64HostConfig)
        {
            C64SelectedJoystick = c64HostConfig.InputConfig.CurrentJoystick - 1;
        }

        // Update the existing host system config, it is referenced from different objects (thus we cannot replace it with a new one).
        var orgHostSystemConfig = _silkNetHostApp.GetHostSystemConfig();
        _mapper.Map(hostSystemConfig, orgHostSystemConfig);
    }

    public void Enable()
    {
        Visible = true;
    }

    public void Disable()
    {
        Visible = false;
    }
}
