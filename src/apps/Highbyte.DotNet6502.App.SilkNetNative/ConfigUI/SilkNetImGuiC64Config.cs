using System.Diagnostics;
using System.Numerics;
using Highbyte.DotNet6502.App.SilkNetNative.SystemSetup;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Render.CustomPayload;
using Highbyte.DotNet6502.Systems.Utils;
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


    private List<(Type renderProviderType, Type renderTargetType)> _availableRendererProviderAndRenderTargetTypeCombinations;

    private List<Type> _renderProviderTypes => _availableRendererProviderAndRenderTargetTypeCombinations.Select(t => t.renderProviderType).Distinct().ToList();
    private int _selectedRendererProviderIndex = 0;
    private Type? _selectedRendererProviderType
    {
        get
        {
            if (_selectedRendererProviderIndex >= 0 && _selectedRendererProviderIndex < _renderProviderTypes.Count)
                return _renderProviderTypes[_selectedRendererProviderIndex];
            return null;
        }
    }

    private int _selectedRendererTargetIndex = 0;
    private List<Type> _renderTargetTypes => _availableRendererProviderAndRenderTargetTypeCombinations
        .Where(t => t.renderProviderType == _selectedRendererProviderType)
        .Select(t => t.renderTargetType)
        .ToList();
    private Type? _selectedRendererTargetType
    {
        get
        {
            // Fix bounds check to use _renderTargetTypes.Count
            if (_selectedRendererTargetIndex >= 0 && _selectedRendererTargetIndex < _renderTargetTypes.Count)
                return _renderTargetTypes[_selectedRendererTargetIndex];
            return null;
        }
    }

    private void UpdateSelectedRenderProvider()
    {
        if (_selectedRendererProviderType is not null)
            _hostConfig.SystemConfig.SetRenderProviderType(_selectedRendererProviderType);
        if (_renderTargetTypes.Count > 0)
        {
            _selectedRendererTargetIndex = 0;
            UpdateSelectedRenderTarget();
        }
    }

    private void UpdateSelectedRenderTarget()
    {
        if (_selectedRendererTargetType is not null)
            _hostConfig.SystemConfig.SetRenderTargetType(_selectedRendererTargetType);
    }

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
    // Tooltip background color to ensure contrast against black main window
    private static Vector4 s_tooltipBgColor = new Vector4(0.22f, 0.22f, 0.22f, 0.98f);

    // Preferred max width for tooltip window
    private const float HelpTooltipMaxWidth = 700f;
    // Font scale to use inside tooltip window to make text larger
    private const float HelpTooltipFontScale = 1.2f;

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

        //_availableRenderers = _silkNetHostApp.GetAvailableSystemRenderProviderTypes().Select(t => t.Name).ToArray();
        _availableRendererProviderAndRenderTargetTypeCombinations = _silkNetHostApp.GetAvailableSystemRenderProviderTypesAndRenderTargetTypeCombinations();
        var currentRenderProviderType = _hostConfig.SystemConfig.RenderProviderType;
        if (currentRenderProviderType != null)
            _selectedRendererProviderIndex = _renderProviderTypes.ToList().IndexOf(currentRenderProviderType);
        else
            _selectedRendererProviderIndex = -1;

        var currentRenderTargetType = _hostConfig.SystemConfig.RenderTargetType;
        if (currentRenderTargetType != null)
            _selectedRendererTargetIndex = _renderTargetTypes.ToList().IndexOf(currentRenderTargetType);
        else
            _selectedRendererTargetIndex = _renderTargetTypes.Count > 0 ? 0 : -1;


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

            ImGui.Separator();

            // Render provider
            ImGui.Text("Render provider:");
            ImGui.PushItemWidth(160);
            if (ImGui.Combo("##renderprovider", ref _selectedRendererProviderIndex, _renderProviderTypes.Select(t => TypeDisplayHelper.GetDisplayName(t)).ToArray(), _renderProviderTypes.Count()))
            {
                UpdateSelectedRenderProvider();
            }
            ImGui.PopItemWidth();
            ImGui.SameLine();
            DrawWrappedHelpTextWithTooltip(_selectedRendererProviderType != null ? TypeDisplayHelper.GetHelpText(_selectedRendererProviderType) : string.Empty);

            // Render target
            ImGui.Text("Render target:");
            ImGui.PushItemWidth(160);
            if (ImGui.Combo("##rendertarget", ref _selectedRendererTargetIndex, _renderTargetTypes.Select(t => TypeDisplayHelper.GetDisplayName(t)).ToArray(), _renderTargetTypes.Count()))
            {
                UpdateSelectedRenderTarget();
            }
            ImGui.PopItemWidth();
            ImGui.SameLine();
            DrawWrappedHelpTextWithTooltip(_selectedRendererTargetType != null ? TypeDisplayHelper.GetHelpText(_selectedRendererTargetType) : string.Empty);

            // Renderer: OpenGL options
            if (_hostConfig.SystemConfig.RenderProviderType == typeof(C64GpuProvider))
            {
                ImGui.Separator();
                ImGui.Text("OpenGL renderer options:");
                if (ImGui.Checkbox("Fine scroll per raster line (experimental)", ref _openGLFineScrollPerRasterLineEnabled))
                {
                    _hostConfig.SilkNetOpenGlRendererConfig.UseFineScrollPerRasterLine = _openGLFineScrollPerRasterLineEnabled;
                }
            }

            ImGui.Separator();

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
                    c64.Cia1.Joystick.KeyboardJoystickEnabled = _keyboardJoystickEnabled;
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
                ImGui.Separator();

                ImGui.PushStyleColor(ImGuiCol.Text, s_errorColor);
                foreach (var error in _validationErrors!)
                {
                    ImGui.TextWrapped($"Error: {error}");
                }
                ImGui.PopStyleColor();
            }

            ImGui.Separator();

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

    // Draw an info icon that shows help text in a tooltip on hover.
    // Only displays the icon if help text is provided.
    private static void DrawWrappedHelpTextWithTooltip(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return; // Don't draw anything if no help text

        // Draw a simple info icon using text
        ImGui.TextDisabled("(?)");

        if (ImGui.IsItemHovered())
        {
            // Tooltip: allow this to be wider than the inline text limit.
            // Use up to 80% of the viewport width, capped by HelpTooltipMaxWidth.
            float viewportWidth = ImGui.GetIO().DisplaySize.X;
            float tooltipWidth = MathF.Min(HelpTooltipMaxWidth, viewportWidth * 0.8f);

            ImGui.PushStyleColor(ImGuiCol.PopupBg, s_tooltipBgColor);
            ImGui.SetNextWindowSize(new Vector2(tooltipWidth, 0f), ImGuiCond.Always);
            ImGui.BeginTooltip();
            ImGui.SetWindowFontScale(HelpTooltipFontScale);
            ImGui.PushTextWrapPos(0); // wrap to tooltip window width
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
            ImGui.SetWindowFontScale(1.0f);
            ImGui.EndTooltip();
            ImGui.PopStyleColor();
        }
    }
}
