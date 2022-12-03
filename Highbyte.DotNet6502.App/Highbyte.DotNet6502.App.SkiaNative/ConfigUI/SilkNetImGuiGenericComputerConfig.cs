using System.Numerics;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.App.SkiaNative.ConfigUI;

public class SilkNetImGuiGenericComputerConfig
{
    private GenericComputerConfig? _config;
    public GenericComputerConfig UpdatedConfig => _config!;

    public bool Visible { get; set; }
    public bool Ok { get; set; }
    public bool Cancel { get; set; }

    private bool _isValidConfig = true;

    private string _programBinaryFile;

    public bool IsValidConfig => _isValidConfig;
    private List<string> _validationErrors;


    private const int POS_X = 50;
    private const int POS_Y = 50;
    private const int WIDTH = 850;
    private const int HEIGHT = 500;
    static Vector4 s_InformationColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
    static Vector4 s_ErrorColor = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    static Vector4 s_WarningColor = new Vector4(0.5f, 0.8f, 0.8f, 1);
    static Vector4 s_OkButtonColor = new Vector4(0.0f, 0.6f, 0.0f, 1.0f);

    public SilkNetImGuiGenericComputerConfig()
    {
    }

    internal void Init(GenericComputerConfig config)
    {
        _config = config.Clone();
        _isValidConfig = _config.Validate(out _validationErrors);

        _programBinaryFile = _config.ProgramBinaryFile;

        Visible = true;
    }

    public void Reset(GenericComputerConfig config)
    {
        _config = config;
        _isValidConfig = _config!.Validate(out _validationErrors);

        Visible = false;
        Cancel = false;
        Ok = false;
    }

    public void PostOnRender()
    {
        ImGui.SetNextWindowSize(new Vector2(WIDTH, HEIGHT), ImGuiCond.Once);
        ImGui.SetNextWindowPos(new Vector2(POS_X, POS_Y), ImGuiCond.Once);
        ImGui.SetNextWindowFocus();

        ImGui.Begin($"GenericComputer config");
        //ImGui.BeginPopupModal($"C64 config");

        ImGui.Text("ProgramBinaryFile:");

        ImGui.PushItemWidth(800);
        if (ImGui.InputText("", ref _programBinaryFile, 512))
        {
            _config!.ProgramBinaryFile = _programBinaryFile;
        }
        ImGui.PopItemWidth();

        //ImGui.Text("ProgramBinary:  "); // Byte array, used if ProgramBinaryFile is not set.
        //ImGui.SameLine();
        //ImGui.Text(_config!.ProgramBinary);

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
            _isValidConfig = _config!.Validate(out _validationErrors);
        }
        if (!_isValidConfig)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, s_ErrorColor);
            foreach (var error in _validationErrors)
            {
                ImGui.TextWrapped($"Error: {error}");
            }
            ImGui.PopStyleColor();
        }

        if (ImGui.Button("Cancel"))
        {
            Visible = false;
            Cancel = true;
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(disabled: !_isValidConfig);
        ImGui.PushStyleColor(ImGuiCol.Button, s_OkButtonColor);
        if (ImGui.Button("Ok"))
        {
            Visible = false;
            Ok = true;
        }
        ImGui.PopStyleColor();
        ImGui.EndDisabled();

        //ImGui.EndPopup();
        ImGui.End();
    }
}
