using System;
using System.Linq;
using System.Numerics;
using Highbyte.DotNet6502.App.SilkNetNative.Core;
using Highbyte.DotNet6502.App.SilkNetNative.Shell.Commodore64.ConfigUI;
using Highbyte.DotNet6502.Impl.SilkNet.Commodore64;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using NativeFileDialogSharp;
using TextCopy;

namespace Highbyte.DotNet6502.App.SilkNetNative.Shell.Commodore64;

/// <summary>
/// Per-frame ImGui drawer for the C64-specific section of the SilkNet host's main menu.
/// Encapsulates everything that used to live in <c>SilkNetImGuiMenu.DrawC64Config</c> /
/// <c>InitC64ImGuiWorkingVariables</c> / <c>IsDiskImageAttached</c>.
/// </summary>
public sealed class C64SilkNetImGuiMenu : IImGuiMenuContributor
{
    private static readonly Vector4 s_informationColor = new(1.0f, 1.0f, 1.0f, 1.0f);
    private static readonly Vector4 s_errorColor = new(1.0f, 0.0f, 0.0f, 1.0f);

    private readonly SilkNetHostApp _host;
    private readonly ISilkNetMenuHost _menuHost;
    private readonly ILogger _logger;

    private bool _keyboardJoystickEnabled;
    private int _keyboardJoystickIndex;
    private int _selectedJoystickIndex;
    private string[] _availableJoysticks = Array.Empty<string>();

    private SilkNetImGuiC64Config? _configUI;

    private EmulatorState EmulatorState => _host.EmulatorState;

    public C64SilkNetImGuiMenu(SilkNetHostApp host, ISilkNetMenuHost menuHost, ILoggerFactory loggerFactory)
    {
        _host = host;
        _menuHost = menuHost;
        _logger = loggerFactory.CreateLogger(nameof(C64SilkNetImGuiMenu));
    }

    public void OnSelected()
    {
        if (_host.CurrentHostSystemConfig is not C64HostConfig c64HostSystemConfig)
            return;
        var c64SystemConfig = c64HostSystemConfig.SystemConfig;
        _keyboardJoystickEnabled = c64SystemConfig.KeyboardJoystickEnabled;
        _keyboardJoystickIndex = c64SystemConfig.KeyboardJoystick - 1;
        _selectedJoystickIndex = c64HostSystemConfig.InputConfig.CurrentJoystick - 1;
        _availableJoysticks = c64HostSystemConfig.InputConfig.AvailableJoysticks
            .Select(x => x.ToString()).ToArray();
    }

    public void Draw()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
        ImGui.PushItemWidth(40);

        // --- Joystick + keyboard joystick ---
        ImGui.Text("Joystick:");
        ImGui.SameLine();
        ImGui.PushItemWidth(35);
        if (ImGui.Combo("##joystick", ref _selectedJoystickIndex, _availableJoysticks, _availableJoysticks.Length))
        {
            var c64HostConfig = (C64HostConfig)_host.CurrentHostSystemConfig;
            c64HostConfig.InputConfig.CurrentJoystick = _selectedJoystickIndex + 1;
        }
        ImGui.PopItemWidth();

        if (ImGui.Checkbox("Keyboard Joystick", ref _keyboardJoystickEnabled))
        {
            var c64SystemConfig = (C64SystemConfig)_host.CurrentHostSystemConfig.SystemConfig;
            c64SystemConfig.KeyboardJoystickEnabled = _keyboardJoystickEnabled;
            if (EmulatorState != EmulatorState.Uninitialized && _host.CurrentRunningSystem is C64 c64)
                c64.Cia1.Joystick.KeyboardJoystickEnabled = _keyboardJoystickEnabled;
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(!_keyboardJoystickEnabled);
        ImGui.PushItemWidth(35);
        if (ImGui.Combo("##keyboardJoystick", ref _keyboardJoystickIndex, _availableJoysticks, _availableJoysticks.Length))
        {
            var c64SystemConfig = (C64SystemConfig)_host.CurrentHostSystemConfig.SystemConfig;
            c64SystemConfig.KeyboardJoystick = _keyboardJoystickIndex + 1;
            if (EmulatorState != EmulatorState.Uninitialized && _host.CurrentRunningSystem is C64 c64)
                c64.Cia1.Joystick.KeyboardJoystick = _keyboardJoystickIndex + 1;
        }
        ImGui.PopItemWidth();
        ImGui.EndDisabled();
        ImGui.PopStyleColor();
        ImGui.PopItemWidth();

        // --- Load BASIC PRG ---
        ImGui.BeginDisabled(disabled: EmulatorState == EmulatorState.Uninitialized);
        if (ImGui.Button("Load Basic PRG file"))
        {
            bool wasRunning = false;
            if (_host.EmulatorState == EmulatorState.Running)
            {
                wasRunning = true;
                _host.Pause();
            }
            _host.Pause();
            _menuHost.LastFileError = "";
            var dialogResult = Dialog.FileOpen(@"prg;*");
            if (dialogResult.IsOk)
            {
                try
                {
                    var c64 = GetCurrentRunningC64OrThrow();
                    BinaryLoader.Load(c64.Mem, dialogResult.Path, out ushort loadedAtAddress, out ushort fileLength);
                    if (loadedAtAddress != C64.BASIC_LOAD_ADDRESS)
                        _logger.LogWarning($"Warning: Loaded program is not a Basic program, it's expected to load at {C64.BASIC_LOAD_ADDRESS.ToHex()} but was loaded at {loadedAtAddress.ToHex()}");
                    else
                        c64.InitBasicMemoryVariables(loadedAtAddress, fileLength);
                    _menuHost.DeferredCollapseWindow = true;
                }
                catch (Exception ex) { _menuHost.LastFileError = ex.Message; }
            }
            if (wasRunning) _host.Start();
        }
        ImGui.EndDisabled();

        // --- Save BASIC PRG ---
        ImGui.BeginDisabled(disabled: EmulatorState == EmulatorState.Uninitialized);
        if (ImGui.Button("Save Basic PRG file"))
        {
            bool wasRunning = false;
            if (_host.EmulatorState == EmulatorState.Running) { wasRunning = true; _host.Pause(); }
            _host.Pause();
            _menuHost.LastFileError = "";
            var dialogResult = Dialog.FileSave(@"prg;*");
            if (dialogResult.IsOk)
            {
                try
                {
                    if (_host.CurrentRunningSystem is not C64 c64) return;
                    BinarySaver.Save(
                        _host.CurrentRunningSystem.Mem, dialogResult.Path,
                        C64.BASIC_LOAD_ADDRESS, c64.GetBasicProgramEndAddress(),
                        addFileHeaderWithLoadAddress: true);
                    _menuHost.DeferredCollapseWindow = true;
                }
                catch (Exception ex) { _menuHost.LastFileError = ex.Message; }
            }
            if (wasRunning) _host.Start();
        }
        ImGui.EndDisabled();

        // --- Toggle D64 disk image ---
        ImGui.BeginDisabled(disabled: EmulatorState == EmulatorState.Uninitialized);
        var diskToggleButtonText = IsDiskImageAttached() ? "Detach D64 disk image" : "Attach D64 disk image";
        if (ImGui.Button(diskToggleButtonText))
            ToggleDiskImage();
        ImGui.EndDisabled();

        // --- Copy BASIC source ---
        ImGui.BeginDisabled(disabled: EmulatorState == EmulatorState.Uninitialized);
        if (ImGui.Button("Copy"))
        {
            var c64 = (C64)_host.CurrentRunningSystem!;
            ClipboardService.SetText(c64.BasicTokenParser.GetBasicText().ToLower());
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.BeginDisabled(disabled: EmulatorState == EmulatorState.Uninitialized);
        if (ImGui.Button("Paste"))
        {
            var c64 = (C64)_host.CurrentRunningSystem!;
            var text = ClipboardService.GetText();
            if (!string.IsNullOrEmpty(text))
                c64.TextPaste.Paste(text);
        }
        ImGui.EndDisabled();

        // --- C64 config popup ---
        ImGui.BeginDisabled(disabled: !(EmulatorState == EmulatorState.Uninitialized));
        _configUI ??= new SilkNetImGuiC64Config(_host, OnSelected);
        if (ImGui.Button("C64 config"))
        {
            _configUI.Init((C64HostConfig)_host.CurrentHostSystemConfig.Clone());
            ImGui.OpenPopup("C64 config");
        }
        ImGui.EndDisabled();
        _configUI.PostOnRender("C64 config");

        if (!_host.IsSystemConfigValid().Result)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, s_errorColor);
            ImGui.TextWrapped("Config has errors. Press C64 Config button.");
            ImGui.PopStyleColor();
        }
    }

    private C64 GetCurrentRunningC64OrThrow()
        => _host.CurrentRunningSystem as C64
           ?? throw new InvalidOperationException("Current running system is not a C64.");

    private bool IsDiskImageAttached()
    {
        if (EmulatorState == EmulatorState.Uninitialized) return false;
        if (_host.CurrentRunningSystem is C64 c64
            && c64.IECBus.GetDeviceByNumber(8) is DiskDrive1541 drive)
            return drive.IsDisketteInserted;
        return false;
    }

    private void ToggleDiskImage()
    {
        if (IsDiskImageAttached())
        {
            try
            {
                if (_host.CurrentRunningSystem is C64 c64
                    && c64.IECBus.GetDeviceByNumber(8) is DiskDrive1541 drive)
                {
                    drive.RemoveD64DiskImage();
                    _menuHost.LastFileError = "";
                }
                else
                {
                    _menuHost.LastFileError = "DiskDrive1541 (device 8) not found or C64 not running.";
                }
            }
            catch (Exception ex) { _menuHost.LastFileError = ex.Message; }
            return;
        }

        bool wasRunning = false;
        if (_host.EmulatorState == EmulatorState.Running) { wasRunning = true; _host.Pause(); }
        _menuHost.LastFileError = "";
        var dialogResult = Dialog.FileOpen("d64;*");
        if (dialogResult.IsOk)
        {
            try
            {
                var d64Image = D64Parser.ParseD64File(dialogResult.Path);
                if (_host.CurrentRunningSystem is C64 c64
                    && c64.IECBus.GetDeviceByNumber(8) is DiskDrive1541 drive)
                {
                    drive.SetD64DiskImage(d64Image);
                    _menuHost.DeferredCollapseWindow = true;
                }
                else
                {
                    _menuHost.LastFileError = "DiskDrive1541 (device 8) not found or C64 not running.";
                }
            }
            catch (Exception ex) { _menuHost.LastFileError = ex.Message; }
        }
        if (wasRunning) _host.Start();
    }
}
