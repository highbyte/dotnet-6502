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

    private GenericComputerConfig _config;
    private GenericComputerHostConfig _hostConfig;

    private bool _open;

    private string _programBinaryFile = default!;

    public bool _isValidConfig;

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

    internal void Init(GenericComputerConfig genericConfig, GenericComputerHostConfig genericHostConfig)
    {
        _config = genericConfig;
        _hostConfig = genericHostConfig;

        _programBinaryFile = _config.ProgramBinaryFile;
    }

    public void PostOnRender(string dialogLabel)
    {
        _open = true;
        if (ImGui.BeginPopupModal(dialogLabel, ref _open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("ProgramBinaryFile:");

            ImGui.PushItemWidth(800);
            if (ImGui.InputText("", ref _programBinaryFile, 512))
            {
                _config!.ProgramBinaryFile = _programBinaryFile;
            }
            ImGui.PopItemWidth();

            //ImGui.Text("ProgramBinary:  "); // Byte array, used if ProgramBinaryFile is not set.
            //ImGui.SameLine();
            //ImGui.Text(Config!.ProgramBinary);

            ImGui.Text("StopAtBRK:                ");
            ImGui.SameLine();
            ImGui.Text(_config!.StopAtBRK.ToString());

            ImGui.Text("CPUCyclesPerFrame:        ");
            ImGui.SameLine();
            ImGui.Text(_config!.CPUCyclesPerFrame.ToString());

            ImGui.Text("ScreenRefreshFrequencyHz: ");
            ImGui.SameLine();
            ImGui.Text(_config!.ScreenRefreshFrequencyHz.ToString());

            // Memory - Screen
            ImGui.Text("Memory - Screen");

            ImGui.Text("  Cols:        ");
            ImGui.SameLine();
            ImGui.Text(_config!.Memory.Screen.Cols.ToString());

            ImGui.Text("  Rows:        ");
            ImGui.SameLine();
            ImGui.Text(_config!.Memory.Screen.Rows.ToString());

            ImGui.Text("  BorderCols:  ");
            ImGui.SameLine();
            ImGui.Text(_config!.Memory.Screen.BorderCols.ToString());

            ImGui.Text("  BorderRows:  ");
            ImGui.SameLine();
            ImGui.Text(_config!.Memory.Screen.BorderRows.ToString());

            ImGui.Text("  ScreenStartAddress:           ");
            ImGui.SameLine();
            ImGui.Text(_config!.Memory.Screen.ScreenStartAddress.ToHex());

            ImGui.Text("  ScreenColorStartAddress:      ");
            ImGui.SameLine();
            ImGui.Text(_config!.Memory.Screen.ScreenColorStartAddress.ToHex());

            ImGui.Text("  ScreenBackgroundColorAddress: ");
            ImGui.SameLine();
            ImGui.Text(_config!.Memory.Screen.ScreenBackgroundColorAddress.ToHex());

            ImGui.Text("  ScreenBorderColorAddress:     ");
            ImGui.SameLine();
            ImGui.Text(_config!.Memory.Screen.ScreenBorderColorAddress.ToHex());

            ImGui.Text("  DefaultFgColor:     ");
            ImGui.SameLine();
            ImGui.Text(_config!.Memory.Screen.DefaultFgColor.ToString());

            ImGui.Text("  DefaultBgColor:     ");
            ImGui.SameLine();
            ImGui.Text(_config!.Memory.Screen.DefaultBgColor.ToString());

            ImGui.Text("  DefaultBorderColor: ");
            ImGui.SameLine();
            ImGui.Text(_config!.Memory.Screen.DefaultBorderColor.ToString());

            // Memory - Input
            ImGui.Text("Memory - Input");

            ImGui.Text("  KeyDownAddress:     ");
            ImGui.SameLine();
            ImGui.Text(_config!.Memory.Input.KeyDownAddress.ToHex());

            ImGui.Text("  KeyPressedAddress:  ");
            ImGui.SameLine();
            ImGui.Text(_config!.Memory.Input.KeyPressedAddress.ToHex());

            ImGui.Text("  KeyReleasedAddress: ");
            ImGui.SameLine();
            ImGui.Text(_config!.Memory.Input.KeyReleasedAddress.ToHex());

            ImGui.Text("  RandomValueAddress: ");
            ImGui.SameLine();
            ImGui.Text(_config!.Memory.Input.RandomValueAddress.ToHex());

            if (_config!.IsDirty)
            {
                _config.ClearDirty();
                _isValidConfig = _config.IsValid(out _validationErrors);
            }
            if (!_isValidConfig)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, s_errorColor);
                foreach (var error in _validationErrors)
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
