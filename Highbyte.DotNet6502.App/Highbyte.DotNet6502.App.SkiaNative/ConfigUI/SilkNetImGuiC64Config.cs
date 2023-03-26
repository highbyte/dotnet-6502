using System.Numerics;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.App.SkiaNative.ConfigUI;

public class SilkNetImGuiC64Config
{
    private C64Config? _config;
    public C64Config UpdatedConfig => _config!;

    private string _romDirectory;
    private string _kernalRomFile;
    private string _basicRomFile;
    private string _chargenRomFile;

    public bool Visible { get; set; }
    public bool Ok { get; set; }
    public bool Cancel { get; set; }

    private bool _isValidConfig = true;
    public bool IsValidConfig => _isValidConfig;
    private List<string> _validationErrors;


    private const int POS_X = 50;
    private const int POS_Y = 50;
    private const int WIDTH = 400;
    private const int HEIGHT = 300;
    static Vector4 s_InformationColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
    static Vector4 s_ErrorColor = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    static Vector4 s_WarningColor = new Vector4(0.5f, 0.8f, 0.8f, 1);
    static Vector4 s_OkButtonColor = new Vector4(0.0f, 0.6f, 0.0f, 1.0f);

    public SilkNetImGuiC64Config()
    {
    }

    internal void Init(C64Config c64Config)
    {
        _config = c64Config.Clone();
        _isValidConfig = _config.IsValid(out _validationErrors);

        _romDirectory = _config.ROMDirectory;
        _kernalRomFile = _config.HasROM(C64Config.KERNAL_ROM_NAME) ? _config.GetROM(C64Config.KERNAL_ROM_NAME).File : "";
        _basicRomFile = _config.HasROM(C64Config.KERNAL_ROM_NAME) ? _config.GetROM(C64Config.BASIC_ROM_NAME).File : "";
        _chargenRomFile = _config.HasROM(C64Config.KERNAL_ROM_NAME) ? _config.GetROM(C64Config.CHARGEN_ROM_NAME).File : "";

        Visible = true;

    }

    public void Reset(C64Config c64Config)
    {
        _config = c64Config;
        _isValidConfig = _config!.IsValid(out _validationErrors);

        Visible = false;
        Cancel = false;
        Ok = false;
    }

    public void PostOnRender()
    {
        ImGui.SetNextWindowSize(new Vector2(WIDTH, HEIGHT), ImGuiCond.Once);
        ImGui.SetNextWindowPos(new Vector2(POS_X, POS_Y), ImGuiCond.Once);
        ImGui.SetNextWindowFocus();

        ImGui.Begin($"C64 config");
        //ImGui.BeginPopupModal($"C64 config");

        ImGui.Text("C64 model:  ");
        ImGui.SameLine();
        ImGui.Text(_config!.C64Model);

        ImGui.Text("VIC2 model: ");
        ImGui.SameLine();
        ImGui.Text(_config!.Vic2Model);

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
        if (ImGui.InputText("Chargen file", ref _chargenRomFile, 100))
        {
            _config!.SetROM(C64Config.CHARGEN_ROM_NAME, _chargenRomFile);
        }

        if (_config!.IsDirty)
        {
            _config.ClearDirty();
            _isValidConfig = _config!.IsValid(out _validationErrors);
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
