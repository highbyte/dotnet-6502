using System.Numerics;
using AutoMapper;
using Highbyte.DotNet6502.App.SilkNetNative;
using Highbyte.DotNet6502.App.SilkNetNative.ConfigUI;
using Highbyte.DotNet6502.App.SilkNetNative.SystemSetup;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Microsoft.Extensions.Logging;
using NativeFileDialogSharp;

namespace Highbyte.DotNet6502.App.SilkNetNative;

public class SilkNetImGuiMenu : ISilkNetImGuiWindow
{
    private readonly SilkNetWindow _silkNetWindow;
    private EmulatorState EmulatorState => _silkNetWindow.EmulatorState;

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
    public string[] C64AvailableJoysticks;

    private string SelectedSystemName => _silkNetWindow.SystemList.Systems.ToArray()[_selectedSystemItem];

    private ISystemConfig? _originalSystemConfig;
    private IHostSystemConfig? _originalHostSystemConfig;

    private SilkNetImGuiC64Config? _c64ConfigUI;
    private SilkNetImGuiGenericComputerConfig? _genericComputerConfigUI;

    private string _lastFileError = "";

    public SilkNetImGuiMenu(SilkNetWindow silkNetWindow, string defaultSystemName, bool defaultAudioEnabled, float defaultAudioVolumePercent, IMapper mapper, ILoggerFactory loggerFactory)
    {
        _silkNetWindow = silkNetWindow;
        _screenScaleString = silkNetWindow.CanvasScale.ToString();

        _selectedSystemItem = _silkNetWindow.SystemList.Systems.ToList().IndexOf(defaultSystemName);

        _audioEnabled = defaultAudioEnabled;
        _audioVolumePercent = defaultAudioVolumePercent;

        _mapper = mapper;
        _logger = loggerFactory.CreateLogger<SilkNetImGuiMenu>();


        ISystemConfig systemConfig = GetSelectedSystemConfig();
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
        ImGui.Combo("", ref _selectedSystemItem, _silkNetWindow.SystemList.Systems.ToArray(), _silkNetWindow.SystemList.Systems.Count);
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
            if (_silkNetWindow.EmulatorState == EmulatorState.Uninitialized)
                _silkNetWindow.SetCurrentSystem(SelectedSystemName);
            _silkNetWindow.Start();
            return;
        }
        ImGui.EndDisabled();

        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Running));
        ImGui.SameLine();
        if (ImGui.Button("Pause"))
        {
            _silkNetWindow.Pause();
        }
        ImGui.EndDisabled();

        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused));
        ImGui.SameLine();
        if (ImGui.Button("Reset"))
        {
            _silkNetWindow.Reset();
            return;
        }
        ImGui.EndDisabled();

        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused));
        ImGui.SameLine();
        if (ImGui.Button("Stop"))
        {
            _silkNetWindow.Stop();
            return;
        }
        ImGui.EndDisabled();

        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused));
        if (ImGui.Button("Monitor"))
        {
            _silkNetWindow.ToggleMonitor();
        }
        ImGui.EndDisabled();

        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused));
        ImGui.SameLine();
        if (ImGui.Button("Instrumentations"))
        {
            _silkNetWindow.ToggleStatsPanel();
        }
        ImGui.EndDisabled();

        //ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused));
        ImGui.SameLine();
        if (ImGui.Button("Logs"))
        {
            _silkNetWindow.ToggleLogsPanel();
        }
        //ImGui.EndDisabled();

        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Uninitialized));
        ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
        //ImGui.SetKeyboardFocusHere(0);
        ImGui.PushItemWidth(40);
        if (ImGui.InputText("Scale", ref _screenScaleString, 4))
        {
            if (float.TryParse(_screenScaleString, out float scale))
                _silkNetWindow.CanvasScale = scale;
        }
        ImGui.PopItemWidth();
        ImGui.PopStyleColor();
        ImGui.EndDisabled();

        // System settings
        if (!string.IsNullOrEmpty(SelectedSystemName))
        {
            // Common audio settings
            ISystemConfig systemConfig = GetSelectedSystemConfig();

            ImGui.BeginDisabled(disabled: !(systemConfig.AudioSupported && EmulatorState == EmulatorState.Uninitialized));
            ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
            //ImGui.SetKeyboardFocusHere(0);
            ImGui.PushItemWidth(40);
            if (systemConfig.AudioSupported)
            {
                if (ImGui.Checkbox("Audio enabled (experimental)", ref _audioEnabled))
                {
                    systemConfig.AudioEnabled = _audioEnabled;
                }
            }
            ImGui.PopStyleColor();
            ImGui.PopItemWidth();
            ImGui.EndDisabled();

            ImGui.BeginDisabled(disabled: !(systemConfig.AudioSupported));
            ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
            //ImGui.SetKeyboardFocusHere(0);
            ImGui.PushItemWidth(40);
            if (systemConfig.AudioSupported)
            {
                if (ImGui.SliderFloat("Volume", ref _audioVolumePercent, 0f, 100f, ""))
                {
                    _silkNetWindow.SetVolumePercent(_audioVolumePercent);
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
                if (_silkNetWindow.EmulatorState == EmulatorState.Running)
                {
                    wasRunning = true;
                    _silkNetWindow.Pause();
                }

                _lastFileError = "";
                var dialogResult = Dialog.FileOpen(@"prg;*");
                if (dialogResult.IsOk)
                {
                    try
                    {
                        var fileName = dialogResult.Path;
                        BinaryLoader.Load(
                            _silkNetWindow.SystemRunner.System.Mem,
                            fileName,
                            out ushort loadedAtAddress,
                            out ushort fileLength);

                        _silkNetWindow.SystemRunner.System.CPU.PC = loadedAtAddress;

                        _silkNetWindow.Start();
                    }
                    catch (Exception ex)
                    {
                        _lastFileError = ex.Message;
                    }
                }
                else
                {
                    if (wasRunning)
                        _silkNetWindow.Start();
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
        var c64Config = (C64Config)GetSelectedSystemConfig();
        var c64HostConfig = (C64HostConfig)GetSelectedSystemHostConfig();

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
            c64HostConfig.InputConfig.CurrentJoystick = C64SelectedJoystick + 1;
        }
        ImGui.PopItemWidth();

        if (ImGui.Checkbox("Keyboard Joystick", ref C64KeyboardJoystickEnabled))
        {
            if (EmulatorState == EmulatorState.Uninitialized)
            {
                c64Config.KeyboardJoystickEnabled = C64KeyboardJoystickEnabled;
            }
            else
            {
                C64 c64 = (C64)_silkNetWindow.SystemList.GetSystem(SelectedSystemName).Result;
                c64.Cia.Joystick.KeyboardJoystickEnabled = C64KeyboardJoystickEnabled;
            }
        }
        ImGui.SameLine();
        ImGui.BeginDisabled(!C64KeyboardJoystickEnabled);
        ImGui.PushItemWidth(35);
        if (ImGui.Combo("##keyboardJoystick", ref C64KeyboardJoystick, C64AvailableJoysticks, C64AvailableJoysticks.Length))
        {
            if (EmulatorState == EmulatorState.Uninitialized)
            {
                c64Config.KeyboardJoystick = C64KeyboardJoystick + 1;
            }
            else
            {
                C64 c64 = (C64)_silkNetWindow.SystemList.GetSystem(SelectedSystemName).Result;
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
            if (_silkNetWindow.EmulatorState == EmulatorState.Running)
            {
                wasRunning = true;
                _silkNetWindow.Pause();
            }
            _silkNetWindow.Pause();
            _lastFileError = "";
            var dialogResult = Dialog.FileOpen(@"prg;*");
            if (dialogResult.IsOk)
            {
                try
                {
                    var fileName = dialogResult.Path;
                    BinaryLoader.Load(
                        _silkNetWindow.SystemRunner.System.Mem,
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
                        ((C64)_silkNetWindow.SystemRunner.System).InitBasicMemoryVariables(loadedAtAddress, fileLength);
                    }
                }
                catch (Exception ex)
                {
                    _lastFileError = ex.Message;
                }
            }

            if (wasRunning)
                _silkNetWindow.Start();
        }
        ImGui.EndDisabled();

        ImGui.BeginDisabled(disabled: EmulatorState == EmulatorState.Uninitialized);
        if (ImGui.Button("Save Basic PRG file"))
        {
            bool wasRunning = false;
            if (_silkNetWindow.EmulatorState == EmulatorState.Running)
            {
                wasRunning = true;
                _silkNetWindow.Pause();
            }
            _silkNetWindow.Pause();
            _lastFileError = "";
            var dialogResult = Dialog.FileSave(@"prg;*");
            if (dialogResult.IsOk)
            {
                try
                {
                    var fileName = dialogResult.Path;
                    ushort startAddressValue = C64.BASIC_LOAD_ADDRESS;
                    var endAddressValue = ((C64)_silkNetWindow.SystemRunner.System).GetBasicProgramEndAddress();
                    BinarySaver.Save(
                        _silkNetWindow.SystemRunner.System.Mem,
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
                _silkNetWindow.Start();
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
            RememberOriginalConfigs();
            _c64ConfigUI.Init();
            ImGui.OpenPopup("C64 config");
        }
        ImGui.EndDisabled();
        _c64ConfigUI.PostOnRender("C64 config");

        if (!_c64ConfigUI.IsValidConfig)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, s_errorColor);
            ImGui.TextWrapped($"Config has errors. Press C64 Config button.");
            ImGui.PopStyleColor();
        }
    }

    private void DrawGenericComputerConfig()
    {
        var genericComputerConfig = (GenericComputerConfig)GetSelectedSystemConfig();

        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Uninitialized));

        if (_genericComputerConfigUI == null)
        {
            _genericComputerConfigUI = new SilkNetImGuiGenericComputerConfig(this);
        }
        if (ImGui.Button("GenericComputer config"))
        {
            RememberOriginalConfigs();
            _genericComputerConfigUI.Init();
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
        return _silkNetWindow.SystemList.IsValidConfig(SelectedSystemName).Result;
    }
    internal ISystemConfig GetSelectedSystemConfig()
    {
        return _silkNetWindow.SystemList.GetCurrentSystemConfig(SelectedSystemName).Result;
    }
    internal IHostSystemConfig GetSelectedSystemHostConfig()
    {
        if (!_silkNetWindow.EmulatorConfig.HostSystemConfigs.ContainsKey(SelectedSystemName))
            return null;
        return _silkNetWindow.EmulatorConfig.HostSystemConfigs[SelectedSystemName];
    }

    internal void RememberOriginalConfigs()
    {
        _originalSystemConfig = (ISystemConfig)GetSelectedSystemConfig().Clone();

        var hostSystemConfig = (IHostSystemConfig)GetSelectedSystemHostConfig();
        if (hostSystemConfig != null)
            _originalHostSystemConfig = (IHostSystemConfig)GetSelectedSystemHostConfig().Clone();
    }
    internal void RestoreOriginalConfigs()
    {
        UpdateCurrentSystemConfig(_originalSystemConfig, _originalHostSystemConfig);
    }

    internal void UpdateCurrentSystemConfig(ISystemConfig config, IHostSystemConfig? hostSystemConfig)
    {
        // Update the system config
        _silkNetWindow.SystemList.ChangeCurrentSystemConfig(SelectedSystemName, config);

        // Update the existing host system config, it is referenced from different objects (thus we cannot replace it with a new one).
        if (hostSystemConfig != null)
        {
            var org = _silkNetWindow.EmulatorConfig.HostSystemConfigs[SelectedSystemName];
            if (org != null && hostSystemConfig != null)
                _mapper.Map(hostSystemConfig, org);
        }
    }
    public void Run()
    {
        _silkNetWindow.EmulatorState = EmulatorState.Running;
    }

    public void Stop()
    {
        _silkNetWindow.EmulatorState = EmulatorState.Paused;
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
