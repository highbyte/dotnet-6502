using System.Diagnostics;
using System.Numerics;
using Highbyte.DotNet6502.App.SilkNetNative.SystemSetup;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.App.SilkNetNative.ConfigUI;

public class SilkNetImGuiC64Config
{
    private readonly SilkNetHostApp _silkNetHostApp;
    private readonly SilkNetImGuiMenu _mainMenu;
    private C64Config _config;
    private C64HostConfig _hostConfig;

    private int _selectedJoystickIndex;
    private string[] _availableJoysticks = [];

    private bool _keyboardJoystickEnabled;
    private int _keyboardJoystickIndex;

    private string? _romDirectory;
    private string? _kernalRomFile;
    private string? _basicRomFile;
    private string? _chargenRomFile;

    private int _selectedRenderer = 0;
    private readonly string[] _availableRenderers = Enum.GetNames<C64HostRenderer>();
    private bool _openGLFineScrollPerRasterLineEnabled;

    private bool _open;

    private bool _isValidConfig;

    private List<string> _validationErrors = new();

    //private static Vector4 s_informationColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
    private static Vector4 s_errorColor = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    //private static Vector4 s_warningColor = new Vector4(0.5f, 0.8f, 0.8f, 1);
    private static Vector4 s_okButtonColor = new Vector4(0.0f, 0.6f, 0.0f, 1.0f);

    public SilkNetImGuiC64Config(SilkNetHostApp silkNetHostApp, SilkNetImGuiMenu mainMenu)
    {
        _silkNetHostApp = silkNetHostApp;
        _mainMenu = mainMenu;
    }

    internal void Init(C64Config c64Config, C64HostConfig c64HostConfig)
    {
        _config = c64Config;
        _hostConfig = c64HostConfig;

        // Init ImGui variables bound to UI
        _selectedJoystickIndex = _hostConfig.InputConfig.CurrentJoystick - 1;
        _availableJoysticks = _hostConfig.InputConfig.AvailableJoysticks.Select(x => x.ToString()).ToArray();

        _keyboardJoystickEnabled = _config.KeyboardJoystickEnabled;
        _keyboardJoystickIndex = _config.KeyboardJoystick - 1;

        _romDirectory = _config.ROMDirectory;
        _kernalRomFile = _config.HasROM(C64Config.KERNAL_ROM_NAME) ? _config.GetROM(C64Config.KERNAL_ROM_NAME).File! : "";
        _basicRomFile = _config.HasROM(C64Config.KERNAL_ROM_NAME) ? _config.GetROM(C64Config.BASIC_ROM_NAME).File! : "";
        _chargenRomFile = _config.HasROM(C64Config.KERNAL_ROM_NAME) ? _config.GetROM(C64Config.CHARGEN_ROM_NAME).File! : "";

        _selectedRenderer = _availableRenderers.ToList().IndexOf(_hostConfig.Renderer.ToString());
        _openGLFineScrollPerRasterLineEnabled = _hostConfig.SilkNetOpenGlRendererConfig.UseFineScrollPerRasterLine;
    }

    public void PostOnRender(string dialogLabel)
    {
        _open = true;
        if (ImGui.BeginPopupModal(dialogLabel, ref _open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("C64 model");
            ImGui.LabelText("C64 model", $"{_config!.C64Model}");
            ImGui.LabelText("VIC2 model", $"{_config!.Vic2Model}");

            ImGui.Text("ROMs");
            if (ImGui.InputText("Directory", ref _romDirectory, 255))
            {
                _config!.ROMDirectory = _romDirectory;
            }
            if (ImGui.InputText("Kernal file", ref _kernalRomFile, 100))
            {
                _config!.SetROM(C64Config.KERNAL_ROM_NAME, _kernalRomFile);
            }
            if (ImGui.InputText("Basic file", ref _basicRomFile, 100))
            {
                _config!.SetROM(C64Config.BASIC_ROM_NAME, _basicRomFile);
            }
            if (ImGui.InputText("CharGen file", ref _chargenRomFile, 100))
            {
                _config!.SetROM(C64Config.CHARGEN_ROM_NAME, _chargenRomFile);
            }

            // Renderer
            ImGui.Text("Renderer:");
            ImGui.SameLine();
            ImGui.PushItemWidth(140);
            if (ImGui.Combo("##renderer", ref _selectedRenderer, _availableRenderers, _availableRenderers.Length))
            {
                _hostConfig.Renderer = Enum.Parse<C64HostRenderer>(_availableRenderers[_selectedRenderer]);
            }
            ImGui.PopItemWidth();

            // Renderer: OpenGL options
            if (_hostConfig.Renderer == C64HostRenderer.SilkNetOpenGl)
            {
                if (ImGui.Checkbox("Fine scroll per raster line (experimental)", ref _openGLFineScrollPerRasterLineEnabled))
                {
                    _hostConfig.SilkNetOpenGlRendererConfig.UseFineScrollPerRasterLine = _openGLFineScrollPerRasterLineEnabled;
                }
            }

            // Joystick
            ImGui.Text("Joystick:");
            ImGui.SameLine();
            ImGui.PushItemWidth(35);
            if (ImGui.Combo("##joystick", ref _selectedJoystickIndex, _availableJoysticks, _availableJoysticks.Length))
            {
                _hostConfig.InputConfig.CurrentJoystick = _selectedJoystickIndex + 1;
            }
            ImGui.PopItemWidth();

            ImGui.BeginDisabled(disabled: true);
            foreach (var mapKey in _hostConfig.InputConfig.GamePadToC64JoystickMap[_hostConfig.InputConfig.CurrentJoystick])
            {
                ImGui.LabelText($"{string.Join(",", mapKey.Key)}", $"{string.Join(",", mapKey.Value)}");
            }
            ImGui.EndDisabled();

            // Keyboard joystick
            if (ImGui.Checkbox("Keyboard Joystick", ref _keyboardJoystickEnabled))
            {
                _config.KeyboardJoystickEnabled = _keyboardJoystickEnabled;

                if (_silkNetHostApp.EmulatorState != EmulatorState.Uninitialized)
                {
                    // System is running, also update the system directly
                    C64 c64 = (C64)_silkNetHostApp.CurrentRunningSystem;
                    c64.Cia.Joystick.KeyboardJoystickEnabled = _keyboardJoystickEnabled;
                }
            }

            ImGui.SameLine();
            ImGui.BeginDisabled(!_keyboardJoystickEnabled);
            ImGui.PushItemWidth(35);
            if (ImGui.Combo("##keyboardJoystick", ref _keyboardJoystickIndex, _availableJoysticks, _availableJoysticks.Length))
            {
                _config.KeyboardJoystick = _keyboardJoystickIndex + 1;
            }
            ImGui.PopItemWidth();
            ImGui.EndDisabled();

            var keyToJoystickMap = _config!.KeyboardJoystickMap;
            ImGui.BeginDisabled(disabled: true);
            foreach (var mapKey in keyToJoystickMap.GetMap(_config.KeyboardJoystick))
            {
                ImGui.LabelText($"{string.Join(",", mapKey.Key)}", $"{string.Join(",", mapKey.Value)}");
            }
            ImGui.EndDisabled();

            // Update validation fields
            if (_config!.IsDirty)
            {
                _config.ClearDirty();
                _isValidConfig = _config.IsValid(out _validationErrors);
            }
            if (!_isValidConfig)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, s_errorColor);
                foreach (var error in _validationErrors!)
                {
                    ImGui.TextWrapped($"Error: {error}");
                }
                ImGui.PopStyleColor();
            }

            // Close buttons
            ImGui.BeginDisabled(disabled: !_isValidConfig);
            ImGui.PushStyleColor(ImGuiCol.Button, s_okButtonColor);
            if (ImGui.Button("Ok"))
            {
                Debug.WriteLine("Ok pressed");
                _silkNetHostApp.UpdateSystemConfig(_config);
                _silkNetHostApp.UpdateHostSystemConfig(_hostConfig);
                _mainMenu.InitC64ImGuiWorkingVariables();
                ImGui.CloseCurrentPopup();
            }
            ImGui.PopStyleColor();
            ImGui.EndDisabled();

            ImGui.SameLine();

            if (ImGui.Button("Cancel"))
            {
                Debug.WriteLine("Cancel pressed");
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }
}
