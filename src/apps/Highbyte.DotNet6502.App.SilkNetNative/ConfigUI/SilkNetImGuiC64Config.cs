using System.Diagnostics;
using System.Numerics;
using Highbyte.DotNet6502.App.SilkNetNative.SystemSetup;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.App.SilkNetNative.ConfigUI;

public class SilkNetImGuiC64Config
{
    private readonly SilkNetHostApp _silkNetHostApp;
    private readonly SilkNetImGuiMenu _mainMenu;
    private C64SystemConfig _systemConfig;
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

    // ROM download state
    private bool _isDownloadingRoms = false;
    private string _downloadStatusMessage = "";
    private bool _downloadSuccess = false;

    //private static Vector4 s_informationColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
    private static Vector4 s_errorColor = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    private static Vector4 s_successColor = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
    //private static Vector4 s_warningColor = new Vector4(0.5f, 0.8f, 0.8f, 1);
    private static Vector4 s_okButtonColor = new Vector4(0.0f, 0.6f, 0.0f, 1.0f);

    public SilkNetImGuiC64Config(SilkNetHostApp silkNetHostApp, SilkNetImGuiMenu mainMenu)
    {
        _silkNetHostApp = silkNetHostApp;
        _mainMenu = mainMenu;
    }

    internal void Init(C64HostConfig c64HostConfig)
    {
        _systemConfig = c64HostConfig.SystemConfig;
        _hostConfig = c64HostConfig;

        // Init ImGui variables bound to UI
        _selectedJoystickIndex = _hostConfig.InputConfig.CurrentJoystick - 1;
        _availableJoysticks = _hostConfig.InputConfig.AvailableJoysticks.Select(x => x.ToString()).ToArray();

        _keyboardJoystickEnabled = _systemConfig.KeyboardJoystickEnabled;
        _keyboardJoystickIndex = _systemConfig.KeyboardJoystick - 1;

        _romDirectory = _systemConfig.ROMDirectory;
        _kernalRomFile = _systemConfig.HasROM(C64SystemConfig.KERNAL_ROM_NAME) ? _systemConfig.GetROM(C64SystemConfig.KERNAL_ROM_NAME).File! : "";
        _basicRomFile = _systemConfig.HasROM(C64SystemConfig.BASIC_ROM_NAME) ? _systemConfig.GetROM(C64SystemConfig.BASIC_ROM_NAME).File! : "";
        _chargenRomFile = _systemConfig.HasROM(C64SystemConfig.CHARGEN_ROM_NAME) ? _systemConfig.GetROM(C64SystemConfig.CHARGEN_ROM_NAME).File! : "";

        _selectedRenderer = _availableRenderers.ToList().IndexOf(_hostConfig.Renderer.ToString());
        _openGLFineScrollPerRasterLineEnabled = _hostConfig.SilkNetOpenGlRendererConfig.UseFineScrollPerRasterLine;

        // Reset download status
        _isDownloadingRoms = false;
        _downloadStatusMessage = "";
        _downloadSuccess = false;
    }

    public void PostOnRender(string dialogLabel)
    {
        _open = true;
        if (ImGui.BeginPopupModal(dialogLabel, ref _open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            //ImGui.Text("C64 model");
            //ImGui.LabelText("C64 model", $"{_config!.C64Model}");
            //ImGui.LabelText("VIC2 model", $"{_config!.Vic2Model}");

            ImGui.Text("ROMs");

            // ROM auto-download button
            ImGui.BeginDisabled(_isDownloadingRoms);
            if (ImGui.Button("Auto download ROM files"))
            {
                _ = Task.Run(async () => await AutoDownloadROMs());
            }
            ImGui.EndDisabled();

            ImGui.SameLine();
            if (ImGui.Button("Manual ROM download link"))
            {
                var url = new Uri(_systemConfig.ROMDownloadUrls[C64SystemConfig.KERNAL_ROM_NAME]).GetLeftPart(UriPartial.Authority);
                OpenURL(url);
            }

            // Download status message
            if (!string.IsNullOrEmpty(_downloadStatusMessage))
            {
                var color = _downloadSuccess ? s_successColor : s_errorColor;
                ImGui.PushStyleColor(ImGuiCol.Text, color);
                ImGui.TextWrapped(_downloadStatusMessage);
                ImGui.PopStyleColor();
            }

            if (ImGui.InputText("Directory", ref _romDirectory, 255))
            {
                _systemConfig!.ROMDirectory = _romDirectory;
            }
            if (ImGui.InputText("Kernal file", ref _kernalRomFile, 100))
            {
                _systemConfig!.SetROM(C64SystemConfig.KERNAL_ROM_NAME, _kernalRomFile);
            }
            if (ImGui.InputText("Basic file", ref _basicRomFile, 100))
            {
                _systemConfig!.SetROM(C64SystemConfig.BASIC_ROM_NAME, _basicRomFile);
            }
            if (ImGui.InputText("CharGen file", ref _chargenRomFile, 100))
            {
                _systemConfig!.SetROM(C64SystemConfig.CHARGEN_ROM_NAME, _chargenRomFile);
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
                _systemConfig.KeyboardJoystickEnabled = _keyboardJoystickEnabled;

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
                _systemConfig.KeyboardJoystick = _keyboardJoystickIndex + 1;
            }
            ImGui.PopItemWidth();
            ImGui.EndDisabled();

            var keyToJoystickMap = _systemConfig!.KeyboardJoystickMap;
            ImGui.BeginDisabled(disabled: true);
            foreach (var mapKey in keyToJoystickMap.GetMap(_systemConfig.KeyboardJoystick))
            {
                ImGui.LabelText($"{string.Join(",", mapKey.Key)}", $"{string.Join(",", mapKey.Value)}");
            }
            ImGui.EndDisabled();

            // Update validation fields
            if (_hostConfig!.IsDirty)
            {
                _hostConfig.ClearDirty();
                _isValidConfig = _hostConfig.IsValid(out _validationErrors);
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

    private async Task AutoDownloadROMs()
    {
        _isDownloadingRoms = true;
        _downloadStatusMessage = "Downloading ROMs...";
        _downloadSuccess = false;

        try
        {
            var romFolder = PathHelper.ExpandOSEnvironmentVariables(_systemConfig.ROMDirectory);
            await DownloadC64RomsAsync(_systemConfig.ROMDownloadUrls, romFolder);

            // Update the system config with the downloaded ROM files
            foreach (var romDownload in _systemConfig.ROMDownloadUrls)
            {
                var romName = romDownload.Key;
                var romUrl = romDownload.Value;
                var filename = Path.GetFileName(new Uri(romUrl).LocalPath);
                _systemConfig.SetROM(romName, filename);
            }

            // Update the UI variables
            _romDirectory = _systemConfig.ROMDirectory;
            _kernalRomFile = _systemConfig.HasROM(C64SystemConfig.KERNAL_ROM_NAME) ? _systemConfig.GetROM(C64SystemConfig.KERNAL_ROM_NAME).File! : "";
            _basicRomFile = _systemConfig.HasROM(C64SystemConfig.BASIC_ROM_NAME) ? _systemConfig.GetROM(C64SystemConfig.BASIC_ROM_NAME).File! : "";
            _chargenRomFile = _systemConfig.HasROM(C64SystemConfig.CHARGEN_ROM_NAME) ? _systemConfig.GetROM(C64SystemConfig.CHARGEN_ROM_NAME).File! : "";

            _downloadStatusMessage = "ROMs downloaded successfully!";
            _downloadSuccess = true;
        }
        catch (Exception ex)
        {
            _downloadStatusMessage = $"Error downloading ROMs: {ex.Message}";
            _downloadSuccess = false;
        }
        finally
        {
            _isDownloadingRoms = false;
        }
    }

    private async Task DownloadC64RomsAsync(Dictionary<string, string> romDownloadUrls, string destinationDirectory)
    {
        if (!Directory.Exists(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");

        foreach (var romDownload in romDownloadUrls)
        {
            var romName = romDownload.Key;
            var romUrl = romDownload.Value;
            var filename = Path.GetFileName(new Uri(romUrl).LocalPath);
            var dest = Path.Combine(destinationDirectory, filename);

            try
            {
                using var response = await httpClient.GetAsync(romUrl);
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Failed to get '{romUrl}' ({(int)response.StatusCode})");

                await using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
                Console.WriteLine($"Downloaded {filename} to {dest}");
            }
            catch (Exception ex)
            {
                if (File.Exists(dest))
                    File.Delete(dest);
                throw new Exception($"Error downloading {romUrl}: {ex.Message}", ex);
            }
        }
    }

    private void OpenURL(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new Exception($"Invalid URL: {url}");
        // Launch the URL in the default browser
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}
