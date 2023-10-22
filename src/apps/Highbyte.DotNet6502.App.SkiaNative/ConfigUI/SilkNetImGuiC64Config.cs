using System.Diagnostics;
using System.Numerics;
using Highbyte.DotNet6502.App.SkiaNative.SystemSetup;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.App.SkiaNative.ConfigUI;

public class SilkNetImGuiC64Config
{
    private readonly SilkNetImGuiMenu _mainMenu;
    C64Config _config => (C64Config)_mainMenu.GetSelectedSystemConfig();
    C64HostConfig _hostConfig => (C64HostConfig)_mainMenu.GetSelectedSystemHostConfig();


    private string? _romDirectory;
    private string? _kernalRomFile;
    private string? _basicRomFile;
    private string? _chargenRomFile;

    private bool _open;

    public bool IsValidConfig
    {
        get
        {
            if (_config == null)
            {
                _validationErrors.Clear();
                return true;
            }
            else
            {
                return _config.IsValid(out _validationErrors);
            }
        }
    }
    private List<string> _validationErrors = new();

    //private static Vector4 s_informationColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
    private static Vector4 s_errorColor = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    //private static Vector4 s_warningColor = new Vector4(0.5f, 0.8f, 0.8f, 1);
    private static Vector4 s_okButtonColor = new Vector4(0.0f, 0.6f, 0.0f, 1.0f);

    public SilkNetImGuiC64Config(SilkNetImGuiMenu mainMenu)
    {
        _mainMenu = mainMenu;
    }

    internal void Init()
    {
        _romDirectory = _config.ROMDirectory;
        _kernalRomFile = _config.HasROM(C64Config.KERNAL_ROM_NAME) ? _config.GetROM(C64Config.KERNAL_ROM_NAME).File! : "";
        _basicRomFile = _config.HasROM(C64Config.KERNAL_ROM_NAME) ? _config.GetROM(C64Config.BASIC_ROM_NAME).File! : "";
        _chargenRomFile = _config.HasROM(C64Config.KERNAL_ROM_NAME) ? _config.GetROM(C64Config.CHARGEN_ROM_NAME).File! : "";
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

            // Joystick
            ImGui.Text("Joystick:");
            ImGui.SameLine();
            ImGui.PushItemWidth(35);
            if (ImGui.Combo("##joystick", ref _mainMenu.C64SelectedJoystick, _mainMenu.C64AvailableJoysticks, _mainMenu.C64AvailableJoysticks.Length))
            {
                _hostConfig.InputConfig.CurrentJoystick = _mainMenu.C64SelectedJoystick + 1;
            }
            ImGui.PopItemWidth();

            ImGui.BeginDisabled(disabled: true);
            foreach (var mapKey in _hostConfig.InputConfig.GamePadToC64JoystickMap[_hostConfig.InputConfig.CurrentJoystick])
            {
                ImGui.LabelText($"{string.Join(",", mapKey.Key)}", $"{string.Join(",", mapKey.Value)}");
            }
            ImGui.EndDisabled();

            // Keyboard joystick
            ImGui.Text($"Keyboard Joystick");
            ImGui.SameLine();
            ImGui.PushItemWidth(35);
            if (ImGui.Combo("##keyboardJoystick", ref _mainMenu.C64KeyboardJoystick, _mainMenu.C64AvailableJoysticks, _mainMenu.C64AvailableJoysticks.Length))
            {
                _config.KeyboardJoystick = _mainMenu.C64KeyboardJoystick + 1;
            }
            ImGui.PopItemWidth();
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
            }
            if (!IsValidConfig)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, s_errorColor);
                foreach (var error in _validationErrors!)
                {
                    ImGui.TextWrapped($"Error: {error}");
                }
                ImGui.PopStyleColor();
            }

            // Close buttons
            if (ImGui.Button("Cancel"))
            {
                Debug.WriteLine("Cancel pressed");
                ImGui.CloseCurrentPopup();
                _mainMenu.RestoreOriginalConfigs();
            }

            ImGui.SameLine();
            ImGui.BeginDisabled(disabled: !IsValidConfig);
            ImGui.PushStyleColor(ImGuiCol.Button, s_okButtonColor);
            if (ImGui.Button("Ok"))
            {
                Debug.WriteLine("Ok pressed");
                ImGui.CloseCurrentPopup();
                _mainMenu.UpdateCurrentSystemConfig(_config, _hostConfig);
            }
            ImGui.PopStyleColor();
            ImGui.EndDisabled();

            ImGui.EndPopup();
        }
    }
}
