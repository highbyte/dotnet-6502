using System.Numerics;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Plugins;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using NativeFileDialogSharp;

namespace Highbyte.DotNet6502.App.SilkNetNative.Core;

public class SilkNetImGuiMenu : ISilkNetImGuiWindow, ISilkNetMenuHost
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
    private readonly ILogger _logger;

    private string SelectedSystemName => _silkNetHostApp.AvailableSystemNames.ToArray()[_selectedSystemItem];
    private string SelectedSystemConfigurationVariant => _silkNetHostApp.AllSelectedSystemConfigurationVariants.ToArray()[_selectedSystemConfigurationVariantItem];

    /// <summary>Active plug-in menu contributor for the currently-selected system. Null when no plug-in matches.</summary>
    private IImGuiMenuContributor? _activeMenuContributor;

    // ISilkNetMenuHost — exposed to per-system plug-in contributors so they can drive shared
    // error/state widgets that the outer menu owns.
    public string LastFileError { get; set; } = "";
    public bool DeferredCollapseWindow { get; set; }

    /// <param name="resolveMenuContributor">
    /// Looks up the per-system menu contributor by <c>SystemName</c>. The factory is invoked
    /// each time the user selects a system in the dropdown, so plug-ins can be resolved
    /// lazily from the DI scope.
    /// </param>
    public SilkNetImGuiMenu(
        SilkNetHostApp silkNetHostApp,
        string defaultSystemName,
        bool defaultAudioEnabled,
        float defaultAudioVolumePercent,
        ILoggerFactory loggerFactory,
        Func<string, IImGuiMenuContributor?> resolveMenuContributor)
    {
        _silkNetHostApp = silkNetHostApp;
        _screenScaleString = _silkNetHostApp.Scale.ToString();

        _silkNetHostApp.SelectSystem(defaultSystemName).Wait();

        _selectedSystemItem = _silkNetHostApp.AvailableSystemNames.ToList().IndexOf(defaultSystemName);
        _selectedSystemConfigurationVariantItem = 0;

        _audioEnabled = defaultAudioEnabled;
        _audioVolumePercent = defaultAudioVolumePercent;

        _logger = loggerFactory.CreateLogger(nameof(SilkNetImGuiMenu));

        _resolveMenuContributor = resolveMenuContributor;
        // Don't activate the contributor here — the plug-in's contributor may need to resolve
        // ISilkNetMenuHost from DI (which is this menu instance). Resolution happens lazily on
        // the first PostOnRender, by which time DI has the host registered.
    }

    private readonly Func<string, IImGuiMenuContributor?> _resolveMenuContributor;
    private bool _contributorActivatedOnce;

    private void ActivateContributorForCurrentSystem()
    {
        _activeMenuContributor = _resolveMenuContributor(SelectedSystemName);
        _activeMenuContributor?.OnSelected();
    }

    private ISystem GetCurrentRunningSystemOrThrow()
    {
        return _silkNetHostApp.CurrentRunningSystem ?? throw new InvalidOperationException("No system is currently running.");
    }

    public void PostOnRender()
    {
        // First-frame activation of the contributor — see ctor comment.
        if (!_contributorActivatedOnce)
        {
            _contributorActivatedOnce = true;
            ActivateContributorForCurrentSystem();
        }

        ImGui.SetNextWindowSize(new Vector2(WIDTH, HEIGHT), ImGuiCond.Once);
        ImGui.SetNextWindowPos(new Vector2(POS_X, POS_Y), ImGuiCond.Once);
        ImGui.SetNextWindowCollapsed(false, ImGuiCond.Appearing);

        //ImGui.Begin($"DotNet 6502 Emulator", ImGuiWindowFlags.NoResize);
        ImGui.Begin($"DotNet 6502 Emulator");

        // Handle deferred window state changes from previous frame
        if (DeferredCollapseWindow)
        {
            ImGui.SetWindowFocus(null);
            ImGui.SetWindowCollapsed(true);
            DeferredCollapseWindow = false;
        }

        ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
        ImGui.Text("System: ");
        ImGui.SameLine();
        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Uninitialized));
        ImGui.PushItemWidth(120);
        if (ImGui.Combo("##systemName", ref _selectedSystemItem, _silkNetHostApp.AvailableSystemNames.ToArray(), _silkNetHostApp.AvailableSystemNames.Count))
        {
            _silkNetHostApp.SelectSystem(SelectedSystemName).Wait();
            _selectedSystemConfigurationVariantItem = 0;
            ActivateContributorForCurrentSystem();
        }
        ;
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
            _activeMenuContributor?.OnSelected();
        }
        ;
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
            DeferredCollapseWindow = true;
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
                if (ImGui.Checkbox("Audio enabled", ref _audioEnabled))
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

                LastFileError = "";
                var dialogResult = Dialog.FileOpen(@"prg;*");
                if (dialogResult.IsOk)
                {
                    try
                    {
                        var currentRunningSystem = GetCurrentRunningSystemOrThrow();
                        var fileName = dialogResult.Path;
                        BinaryLoader.Load(
                            currentRunningSystem.Mem,
                            fileName,
                            out ushort loadedAtAddress,
                            out ushort fileLength);

                        currentRunningSystem.CPU.PC = loadedAtAddress;

                        _silkNetHostApp.Start();
                        DeferredCollapseWindow = true;
                    }
                    catch (Exception ex)
                    {
                        LastFileError = ex.Message;
                    }
                }
                else
                {
                    if (wasRunning)
                        _silkNetHostApp.Start();
                }
            }
            ImGui.EndDisabled();

            // System-specific settings come from the active shell plug-in (App.SilkNetNative.Shell.*).
            _activeMenuContributor?.Draw();
        }

        if (!string.IsNullOrEmpty(LastFileError))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, s_errorColor);
            ImGui.Text($"File error: {LastFileError}");
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

}
