using System.Diagnostics;
using System.Numerics;
using Highbyte.DotNet6502.App.SilkNetNative.SystemSetup;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.App.SilkNetNative.ConfigUI;

public class SilkNetImGuiGenericComputerConfig
{
    private readonly SilkNetHostApp _silkNetHostApp;
    private readonly SilkNetImGuiMenu _mainMenu;

    private GenericComputerConfig _exampleProgramConfig;
    private GenericComputerHostConfig _hostConfig;

    private bool _open;

    public bool _isValidConfig = true;
    private List<string> _validationErrors = new();
    //private static Vector4 s_informationColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
    private static Vector4 s_errorColor = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    //private static Vector4 s_warningColor = new Vector4(0.5f, 0.8f, 0.8f, 1);
    private static Vector4 s_okButtonColor = new Vector4(0.0f, 0.6f, 0.0f, 1.0f);

    public SilkNetImGuiGenericComputerConfig(SilkNetHostApp silkNetHostApp, SilkNetImGuiMenu mainMenu)
    {
        _silkNetHostApp = silkNetHostApp;
        _mainMenu = mainMenu;
    }

    internal void Init(GenericComputerHostConfig genericHostConfig, string selectedSystemConfigurationVariant)
    {
        _hostConfig = genericHostConfig;
        // Note: the example program config is currently read only.
        _exampleProgramConfig = GenericComputerExampleConfigs.GetExampleConfig(selectedSystemConfigurationVariant, genericHostConfig.SystemConfig);
    }

    public void PostOnRender(string dialogLabel)
    {
        _open = true;
        if (ImGui.BeginPopupModal(dialogLabel, ref _open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("ProgramBinaryFile:");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.ProgramBinaryFile);

            ImGui.Text("StopAtBRK:                ");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.StopAtBRK.ToString());

            ImGui.Text("CPUCyclesPerFrame:        ");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.CPUCyclesPerFrame.ToString());

            ImGui.Text("ScreenRefreshFrequencyHz: ");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.ScreenRefreshFrequencyHz.ToString());

            // Memory - Screen
            ImGui.Text("Memory - Screen");

            ImGui.Text("  Cols:        ");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.Memory.Screen.Cols.ToString());

            ImGui.Text("  Rows:        ");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.Memory.Screen.Rows.ToString());

            ImGui.Text("  BorderCols:  ");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.Memory.Screen.BorderCols.ToString());

            ImGui.Text("  BorderRows:  ");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.Memory.Screen.BorderRows.ToString());

            ImGui.Text("  ScreenStartAddress:           ");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.Memory.Screen.ScreenStartAddress.ToHex());

            ImGui.Text("  ScreenColorStartAddress:      ");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.Memory.Screen.ScreenColorStartAddress.ToHex());

            ImGui.Text("  ScreenBackgroundColorAddress: ");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.Memory.Screen.ScreenBackgroundColorAddress.ToHex());

            ImGui.Text("  ScreenBorderColorAddress:     ");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.Memory.Screen.ScreenBorderColorAddress.ToHex());

            ImGui.Text("  DefaultFgColor:     ");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.Memory.Screen.DefaultFgColor.ToString());

            ImGui.Text("  DefaultBgColor:     ");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.Memory.Screen.DefaultBgColor.ToString());

            ImGui.Text("  DefaultBorderColor: ");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.Memory.Screen.DefaultBorderColor.ToString());

            // Memory - Input
            ImGui.Text("Memory - Input");

            ImGui.Text("  KeyDownAddress:     ");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.Memory.Input.KeyDownAddress.ToHex());

            ImGui.Text("  KeyPressedAddress:  ");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.Memory.Input.KeyPressedAddress.ToHex());

            ImGui.Text("  KeyReleasedAddress: ");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.Memory.Input.KeyReleasedAddress.ToHex());

            ImGui.Text("  RandomValueAddress: ");
            ImGui.SameLine();
            ImGui.Text(_exampleProgramConfig!.Memory.Input.RandomValueAddress.ToHex());

            if (_hostConfig!.IsDirty)
            {
                _hostConfig.ClearDirty();
                _isValidConfig = _hostConfig.IsValid(out _validationErrors);
            }
            // Close buttons
            ImGui.BeginDisabled(disabled: !_isValidConfig);
            ImGui.PushStyleColor(ImGuiCol.Button, s_okButtonColor);
            if (ImGui.Button("Ok"))
            {
                Debug.WriteLine("Ok pressed");
                _silkNetHostApp.UpdateHostSystemConfig(_hostConfig);
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
