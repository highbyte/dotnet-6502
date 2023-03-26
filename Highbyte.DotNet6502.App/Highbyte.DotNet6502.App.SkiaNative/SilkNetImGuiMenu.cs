using System.Diagnostics;
using System.Numerics;
using Highbyte.DotNet6502.App.SkiaNative.ConfigUI;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.App.SkiaNative;

public class SilkNetImGuiMenu
{
    private readonly SilkNetWindow _silkNetWindow;
    private EmulatorState EmulatorState => _silkNetWindow.EmulatorState;

    public bool Visible = true;

    private const int POS_X = 10;
    private const int POS_Y = 10;
    private const int WIDTH = 400;
    private const int HEIGHT = 300;
    static Vector4 s_InformationColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
    static Vector4 s_ErrorColor = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    static Vector4 s_WarningColor = new Vector4(0.5f, 0.8f, 0.8f, 1);

    private string _screenScaleString = "";
    private int _selectedSystemItem = 0;
    private string SelectedSystemName => _silkNetWindow.SystemList.Systems.ToArray()[_selectedSystemItem];

    private SilkNetImGuiC64Config _c64ConfigUI;
    private SilkNetImGuiGenericComputerConfig _genericComputerConfigUI;

    public SilkNetImGuiMenu(SilkNetWindow silkNetWindow, string defaultSystemName)
    {
        _silkNetWindow = silkNetWindow;
        _screenScaleString = silkNetWindow.CanvasScale.ToString();

        _selectedSystemItem = _silkNetWindow.SystemList.Systems.ToList().IndexOf(defaultSystemName);
    }

    public void PostOnRender()
    {
        ImGui.SetNextWindowSize(new Vector2(WIDTH, HEIGHT), ImGuiCond.Once);
        ImGui.SetNextWindowPos(new Vector2(POS_X, POS_Y), ImGuiCond.Once);

        //ImGui.Begin($"DotNet 6502 Emulator", ImGuiWindowFlags.NoResize);
        ImGui.Begin($"DotNet 6502 Emulator");

        ImGui.PushStyleColor(ImGuiCol.Text, s_InformationColor);
        ImGui.Text("System: ");
        ImGui.SameLine();
        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Uninitialized));
        ImGui.PushItemWidth(120);
        ImGui.Combo("", ref _selectedSystemItem, _silkNetWindow.SystemList.Systems.ToArray(), _silkNetWindow.SystemList.Systems.Count);
        ImGui.PopItemWidth();
        ImGui.EndDisabled();
        ImGui.PopStyleColor();

        ImGui.PushStyleColor(ImGuiCol.Text, s_InformationColor);
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
        if (ImGui.Button("Stats"))
        {
            _silkNetWindow.ToggleStatsPanel();
        }
        ImGui.EndDisabled();

        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Uninitialized));
        ImGui.PushStyleColor(ImGuiCol.Text, s_InformationColor);
        //ImGui.SetKeyboardFocusHere(0);
        ImGui.PushItemWidth(40);
        if (ImGui.InputText("Scale", ref _screenScaleString, 4))
        {
            if (float.TryParse(_screenScaleString, out float scale))
                _silkNetWindow.CanvasScale = scale;
        }
        ImGui.PopStyleColor();
        ImGui.PopItemWidth();
        ImGui.EndDisabled();

        ImGui.PushStyleColor(ImGuiCol.Text, s_WarningColor);
        ImGui.Text("Toggle menu with F6");
        ImGui.Text("Toggle monitor with F12");
        ImGui.Text("Toggle stats with F11");
        ImGui.PopStyleColor();


        DrawC64Config();

        DrawGenericComputerConfig();

        ImGui.End();
    }

    private void DrawC64Config()
    {
        if (SelectedSystemName == "C64")
        {
            ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Uninitialized));

            if (_c64ConfigUI == null)
            {
                _c64ConfigUI = new SilkNetImGuiC64Config();
                ISystemConfig systemConfig = _silkNetWindow.SystemList.GetCurrentSystemConfig(SelectedSystemName).Result;
                var c64Config = (C64Config)systemConfig;
                _c64ConfigUI.Reset(c64Config);
            }

            if (ImGui.Button("C64 config"))
            {
                if (!_c64ConfigUI.Visible)
                {
                    ISystemConfig systemConfig = _silkNetWindow.SystemList.GetCurrentSystemConfig(SelectedSystemName).Result;
                    var c64Config = (C64Config)systemConfig;
                    _c64ConfigUI.Init(c64Config);
                }
            }
            ImGui.EndDisabled();

            if (!_c64ConfigUI.IsValidConfig)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, s_ErrorColor);
                ImGui.TextWrapped($"Config has errors. Press C64 Config button.");
                ImGui.PopStyleColor();
            }

            if (_c64ConfigUI.Visible)
            {
                _c64ConfigUI.PostOnRender();
                if (_c64ConfigUI.Ok)
                {
                    Debug.WriteLine("Ok pressed");
                    C64Config c64Config = _c64ConfigUI.UpdatedConfig;
                    var systemConfig = (ISystemConfig)c64Config;
                    _silkNetWindow.SystemList.ChangeCurrentSystemConfig(SelectedSystemName, systemConfig);
                    _c64ConfigUI.Reset(c64Config);
                }
                else if (_c64ConfigUI.Cancel)
                {
                    Debug.WriteLine("Cancel pressed");
                    ISystemConfig systemConfig = _silkNetWindow.SystemList.GetCurrentSystemConfig(SelectedSystemName).Result;
                    var c64Config = (C64Config)systemConfig;
                    _c64ConfigUI.Reset(c64Config);
                }
            }
        }

    }

    private void DrawGenericComputerConfig()
    {
        if (SelectedSystemName == "Generic")
        {
            ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Uninitialized));

            if (_genericComputerConfigUI == null)
            {
                _genericComputerConfigUI = new SilkNetImGuiGenericComputerConfig();
                ISystemConfig systemConfig = _silkNetWindow.SystemList.GetCurrentSystemConfig(SelectedSystemName).Result;
                var genericComputerConfig = (GenericComputerConfig)systemConfig;
                _genericComputerConfigUI.Reset(genericComputerConfig);
            }

            if (ImGui.Button("GenericComputer config"))
            {
                if (!_genericComputerConfigUI.Visible)
                {
                    ISystemConfig systemConfig = _silkNetWindow.SystemList.GetCurrentSystemConfig(SelectedSystemName).Result;
                    var genericComputerConfig = (GenericComputerConfig)systemConfig;
                    _genericComputerConfigUI.Init(genericComputerConfig);
                }
            }
            ImGui.EndDisabled();

            if (!_genericComputerConfigUI.IsValidConfig)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, s_ErrorColor);
                ImGui.TextWrapped($"Config has errors. Press GenericComputerConfig button.");
                ImGui.PopStyleColor();
            }

            if (_genericComputerConfigUI.Visible)
            {
                _genericComputerConfigUI.PostOnRender();
                if (_genericComputerConfigUI.Ok)
                {
                    Debug.WriteLine("Ok pressed");
                    GenericComputerConfig genericComputerConfig = _genericComputerConfigUI.UpdatedConfig;
                    var systemConfig = (ISystemConfig)genericComputerConfig;
                    _silkNetWindow.SystemList.ChangeCurrentSystemConfig(SelectedSystemName, systemConfig);
                    _genericComputerConfigUI.Reset(genericComputerConfig);
                }
                else if (_genericComputerConfigUI.Cancel)
                {
                    Debug.WriteLine("Cancel pressed");
                    ISystemConfig systemConfig = _silkNetWindow.SystemList.GetCurrentSystemConfig(SelectedSystemName).Result;
                    var genericComputerConfig = (GenericComputerConfig)systemConfig;
                    _genericComputerConfigUI.Reset(genericComputerConfig);
                }
            }
        }
    }

    private bool SelectedSystemConfigIsValid()
    {
        return _silkNetWindow.SystemList.IsValidConfig(SelectedSystemName).Result;
    }

    public void Run()
    {
        _silkNetWindow.EmulatorState = EmulatorState.Running;
    }

    public void Stop()
    {
        _silkNetWindow.EmulatorState = EmulatorState.Paused;
    }
}
