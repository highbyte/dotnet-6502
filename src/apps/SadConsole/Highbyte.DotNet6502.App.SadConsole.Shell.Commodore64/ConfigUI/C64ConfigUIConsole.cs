using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.App.SadConsole.Core;
using Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Utils.BasicAssistant;
using Highbyte.DotNet6502.Systems.Utils;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;
using Highbyte.DotNet6502.Impl.SadConsole.Commodore64;

namespace Highbyte.DotNet6502.App.SadConsole.Shell.Commodore64.ConfigUI;
public class C64ConfigUIConsole : Window
{
    public const int CONSOLE_WIDTH = USABLE_WIDTH;
    public const int CONSOLE_HEIGHT = USABLE_HEIGHT;
    private const int USABLE_WIDTH = 60;
    private const int USABLE_HEIGHT = 33;
    private const int ActionButtonRow = USABLE_HEIGHT - 7;
    private const int ValidationErrorLabelRow = USABLE_HEIGHT - 5;
    private const int ValidationErrorListRow = USABLE_HEIGHT - 4;
    private const int ValidationErrorHorizontalScrollBarRow = USABLE_HEIGHT - 1;
    private const int ValidationErrorListVisibleTextWidth = USABLE_WIDTH - 6;

    public C64SystemConfig C64SystemConfig => C64HostConfig.SystemConfig;
    public readonly C64HostConfig C64HostConfig;
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<(Type audioProviderType, Type audioTargetType)> _audioCombinations;
    private ComboBox? _audioTargetComboBox;
    private List<string> _validationErrors = new();
    // Mutable so the audio-target ComboBox's SelectedItemChanged closure resolves the
    // currently-visible types after RebuildAudioTargetComboBox swaps the item set.
    private List<Type> _audioTargetDisplayTypes = new();

    public C64ConfigUIConsole(SadConsoleHostApp sadConsoleHostApp, IConfiguration configuration, ILoggerFactory loggerFactory) : base(CONSOLE_WIDTH, CONSOLE_HEIGHT)
    {

        C64HostConfig = (C64HostConfig)sadConsoleHostApp.CurrentHostSystemConfig.Clone();
        _audioCombinations = sadConsoleHostApp.GetAvailableSystemAudioProviderTypesAndAudioTargetTypeCombinations()
            ?? new List<(Type, Type)>();

        Controls.ThemeColors = SadConsoleUISettings.ThemeColors;
        Surface.DefaultBackground = Controls.ThemeColors.ControlHostBackground;
        Surface.DefaultForeground = Controls.ThemeColors.ControlHostForeground;
        Surface.Clear();

        Title = "C64 Config";
        var colors = Controls.GetThemeColors();
        Cursor.PrintAppearanceMatchesHost = false;
        Cursor.DisableWordBreak = true;
        Cursor.SetPrintAppearance(colors.Title, Surface.DefaultBackground);

        UseMouse = true;
        UseKeyboard = true;

        DrawUIItems();
        _configuration = configuration;
        _loggerFactory = loggerFactory;
    }

    private void DrawUIItems()
    {
        // Automatic download of C64 ROMs
        var autoDownloadROMButton = new Button("Auto download ROM files")
        {
            Name = "autoDownloadROMButton",
            Position = (1, 2),
        };
        autoDownloadROMButton.Click += async (s, e) =>
        {
            var autoDownloadROMInfoLabel = GetControlOrThrow<Label>("autoDownloadROMInfoLabel");
            try
            {
                await AutoDownloadROMs();
                autoDownloadROMInfoLabel.DisplayText = "ROMs downloaded OK";
                autoDownloadROMInfoLabel.TextColor = Color.Green;
            }
            catch (Exception ex)
            {
                autoDownloadROMInfoLabel.DisplayText = ex.Message;
                autoDownloadROMInfoLabel.TextColor = Color.Red;
            }
            finally
            {
                IsDirty = true; // Mark as dirty to update the UI
            }
        };
        Controls.Add(autoDownloadROMButton);

        // Manual download link
        var openROMDownloadURLButton = new Button("Manual ROM download link")
        {
            Name = "openROMDownloadURLButton",
            Position = (31, autoDownloadROMButton.Position.Y),
        };
        openROMDownloadURLButton.Click += (s, e) => OpenURL(new Uri(C64SystemConfig.ROMDownloadUrls[C64SystemConfig.KERNAL_ROM_NAME]).GetLeftPart(UriPartial.Authority));
        Controls.Add(openROMDownloadURLButton);

        // Auto ROM download status label
        var autoDownloadROMInfoLabel = new Label(Width - 10)
        {
            Name = "autoDownloadROMInfoLabel",
            Position = (1, autoDownloadROMButton.Bounds.MaxExtentY + 1),
            IsEnabled = false,
            DisplayText = "",
            TextColor = Controls.GetThemeColors().Appearance_ControlDisabled.Foreground
        };
        Controls.Add(autoDownloadROMInfoLabel);


        // ROM directory
        var romDirectoryLabel = CreateLabel("ROM directory", 1, autoDownloadROMInfoLabel.Position.Y + 2);
        var romDirectoryTextBox = new TextBox(Width - 10)
        {
            Name = "romDirectoryTextBox",
            Position = (1, romDirectoryLabel.Position.Y + 1),
        };
        romDirectoryTextBox.TextChanged += (s, e) => { C64SystemConfig.ROMDirectory = romDirectoryTextBox.Text; IsDirty = true; };
        Controls.Add(romDirectoryTextBox);

        var selectROMDirectoryButton = new Button("...")
        {
            Name = "selectROMDirectoryButton",
            Position = (romDirectoryTextBox.Bounds.MaxExtentX + 2, romDirectoryTextBox.Position.Y),
        };
        selectROMDirectoryButton.Click += (s, e) => ShowROMFolderPickerDialog();
        Controls.Add(selectROMDirectoryButton);

        // Kernal ROM file
        var kernalROMLabel = CreateLabel("Kernal ROM", 1, romDirectoryTextBox.Position.Y + 2);
        var kernalROMTextBox = new TextBox(Width - 10)
        {
            Name = "kernalROMTextBox",
            Position = (1, kernalROMLabel.Position.Y + 1)
        };
        kernalROMTextBox.TextChanged += (s, e) => { C64SystemConfig.SetROM(C64SystemConfig.KERNAL_ROM_NAME, kernalROMTextBox!.Text); IsDirty = true; };
        Controls.Add(kernalROMTextBox);

        var selectKernalROMButton = new Button("...")
        {
            Name = "selectKernalROMButton",
            Position = (kernalROMTextBox.Bounds.MaxExtentX + 2, kernalROMTextBox.Position.Y),
        };
        selectKernalROMButton.Click += (s, e) => ShowROMFilePickerDialog(C64SystemConfig.KERNAL_ROM_NAME);
        Controls.Add(selectKernalROMButton);

        // Basic ROM file
        var basicROMLabel = CreateLabel("Basic ROM", 1, kernalROMTextBox.Bounds.MaxExtentY + 2);
        var basicROMTextBox = new TextBox(Width - 10)
        {
            Name = "basicROMTextBox",
            Position = (1, basicROMLabel.Position.Y + 1)
        };
        basicROMTextBox.TextChanged += (s, e) => { C64SystemConfig.SetROM(C64SystemConfig.BASIC_ROM_NAME, basicROMTextBox!.Text); IsDirty = true; };
        Controls.Add(basicROMTextBox);

        var selectBasicROMButton = new Button("...")
        {
            Name = "selectBasicROMButton",
            Position = (basicROMTextBox.Bounds.MaxExtentX + 2, basicROMTextBox.Position.Y),
        };
        selectBasicROMButton.Click += (s, e) => ShowROMFilePickerDialog(C64SystemConfig.BASIC_ROM_NAME);
        Controls.Add(selectBasicROMButton);

        // Chargen ROM file
        var chargenROMLabel = CreateLabel("Chargen ROM", 1, basicROMTextBox.Bounds.MaxExtentY + 2);
        var chargenROMTextBox = new TextBox(Width - 10)
        {
            Name = "chargenROMTextBox",
            Position = (1, chargenROMLabel.Position.Y + 1),
        };
        chargenROMTextBox.TextChanged += (s, e) => { C64SystemConfig.SetROM(C64SystemConfig.CHARGEN_ROM_NAME, chargenROMTextBox!.Text); IsDirty = true; };
        Controls.Add(chargenROMTextBox);

        var selectChargenROMButton = new Button("...")
        {
            Name = "selectChargenROMButton",
            Position = (chargenROMTextBox.Bounds.MaxExtentX + 2, chargenROMTextBox.Position.Y),
        };
        selectChargenROMButton.Click += (s, e) => ShowROMFilePickerDialog(C64SystemConfig.CHARGEN_ROM_NAME);
        Controls.Add(selectChargenROMButton);


        // AI coding assistant selection
        var codingAssistantLabel = CreateLabel("Basic AI assistant: ", 1, selectChargenROMButton.Bounds.MaxExtentY + 2);
        var codingAssistantValue = CreateLabel($"{C64HostConfig.CodeSuggestionBackendType}", codingAssistantLabel.Bounds.MaxExtentX + 1, codingAssistantLabel.Position.Y);
        codingAssistantValue.TextColor = Controls.GetThemeColors().White;

        var codingAssistantInfoLabel = new Label(Width - 10)
        {
            Name = "codingAssistantInfoLabel",
            Position = (1, codingAssistantValue.Bounds.MaxExtentY + 1),
            IsEnabled = false,
            DisplayText = "Set AI assistant in appsetting.json.",
            TextColor = Controls.GetThemeColors().Appearance_ControlDisabled.Foreground
        };
        Controls.Add(codingAssistantInfoLabel);

        var codingAssistantTestButton = new Button("Test")
        {
            Name = "codingAssistantTestButton",
            Position = (codingAssistantValue.Bounds.MaxExtentX + 2, codingAssistantValue.Position.Y),
        };
        codingAssistantTestButton.Click += async (s, e) =>
        {
            try
            {
                var codeSuggestionBackend = CodeSuggestionConfigurator.CreateCodeSuggestion(C64HostConfig.CodeSuggestionBackendType, _configuration, _loggerFactory, C64BasicCodingAssistant.CODE_COMPLETION_LANGUAGE_DESCRIPTION, C64BasicCodingAssistant.CODE_COMPLETION_ADDITIONAL_SYSTEM_INSTRUCTION);
                codingAssistantInfoLabel.DisplayText = "Testing...";
                codingAssistantInfoLabel.TextColor = Color.White;

                await codeSuggestionBackend.CheckAvailability();
                if (codeSuggestionBackend.IsAvailable)
                {
                    codingAssistantInfoLabel.DisplayText = "OK";
                    codingAssistantInfoLabel.TextColor = Color.Green;
                }
                else
                {
                    codingAssistantInfoLabel.DisplayText = codeSuggestionBackend.LastError ?? "Error";
                    codingAssistantInfoLabel.TextColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                codingAssistantInfoLabel.DisplayText = ex.Message;
                codingAssistantInfoLabel.TextColor = Color.Red;
            }
        };
        Controls.Add(codingAssistantTestButton);

        var openBasicAIHelpURLButton = new Button("Help")
        {
            Name = "openBasicAIHelpURLButton",
            Position = (codingAssistantTestButton.Bounds.MaxExtentX, codingAssistantInfoLabel.Position.Y),
        };
        openBasicAIHelpURLButton.Click += (s, e) => OpenURL("https://highbyte.github.io/dotnet-6502/docs/systems/c64/code-completion/");
        Controls.Add(openBasicAIHelpURLButton);


        //ComboBox codingAssistantComboBox = new ComboBox(codingAssistantLabel.Bounds.MaxExtentX + 1, codingAssistantLabel.Position.Y, 6, Enum.GetNames<CodeSuggestionBackendTypeEnum>().ToArray())
        //{
        //    Position = (codingAssistantLabel.Bounds.MaxExtentX + 2, codingAssistantLabel.Position.Y),
        //    Name = "codingAssistantComboBox",
        //    SelectedItem = C64HostConfig.CodeSuggestionBackendType.ToString(),
        //};
        //codingAssistantComboBox.SelectedItemChanged += (s, e) =>
        //{
        //    C64HostConfig.CodeSuggestionBackendType = (CodeSuggestionBackendTypeEnum)codingAssistantComboBox.SelectedItem;
        //    IsDirty = true;
        //};
        //Controls.Add(codingAssistantComboBox);


        // SID emulation mode (always shown, since alternate audio providers could also honour it).
        var sidModeLabel = CreateLabel("SID emulation:", 1, codingAssistantInfoLabel.Bounds.MaxExtentY + 2);
        var sidModeValues = Enum.GetValues<SidEmulationMode>();
        var sidModeComboBox = new ModalWindowComboBox(
            width: 16,
            dropdownWidth: 16,
            dropdownHeight: sidModeValues.Length + 2,
            items: sidModeValues.Cast<object>().ToArray(),
            window: this)
        {
            Name = "sidModeComboBox",
            Position = (sidModeLabel.Bounds.MaxExtentX + 2, sidModeLabel.Position.Y),
            SelectedItem = C64SystemConfig.SidEmulationMode,
        };
        sidModeComboBox.SelectedItemChanged += (s, e) =>
        {
            if (sidModeComboBox.SelectedItem is SidEmulationMode mode)
            {
                C64SystemConfig.SidEmulationMode = mode;
                IsDirty = true;
            }
        };
        Controls.Add(sidModeComboBox);

        // Audio provider — populated from the host app's compiled-in (provider, target) combinations.
        var audioProviderLabel = CreateLabel("Audio provider:", 1, sidModeLabel.Bounds.MaxExtentY + 2);
        var audioProviderTypes = _audioCombinations.Select(c => c.audioProviderType).Distinct().ToList();
        if (C64SystemConfig.AudioProviderType != null && !audioProviderTypes.Contains(C64SystemConfig.AudioProviderType))
        {
            // Config references a provider not in the available list — keep it visible so the user
            // sees what's currently configured rather than silently dropping it.
            audioProviderTypes.Add(C64SystemConfig.AudioProviderType);
        }
        var providerComboBox = new ModalWindowComboBox(
            width: 28,
            dropdownWidth: 32,
            dropdownHeight: audioProviderTypes.Count + 2,
            items: audioProviderTypes.Select(t => (object)TypeDisplayHelper.GetDisplayName(t)).ToArray(),
            window: this)
        {
            Name = "audioProviderComboBox",
            Position = (audioProviderLabel.Bounds.MaxExtentX + 2, audioProviderLabel.Position.Y),
        };
        var initialProvider = audioProviderTypes.FirstOrDefault(t => t == C64SystemConfig.AudioProviderType)
            ?? audioProviderTypes.FirstOrDefault();
        if (initialProvider != null)
            providerComboBox.SelectedItem = TypeDisplayHelper.GetDisplayName(initialProvider);
        providerComboBox.SelectedItemChanged += (s, e) =>
        {
            if (providerComboBox.SelectedItem is not string displayName)
                return;
            var selected = audioProviderTypes.FirstOrDefault(t => TypeDisplayHelper.GetDisplayName(t) == displayName);
            if (selected == null)
                return;
            C64SystemConfig.SetAudioProviderType(selected);
            RebuildAudioTargetComboBox();
            IsDirty = true;
        };
        Controls.Add(providerComboBox);

        // Audio target — recreated when provider changes so it only ever shows compatible targets.
        var audioTargetLabel = CreateLabel("Audio target:", 1, audioProviderLabel.Bounds.MaxExtentY + 2, "audioTargetLabel");
        RebuildAudioTargetComboBox();


        var okButton = new Button(6, 1)
        {
            Name = "okButton",
            Text = "OK",
            Position = (1, ActionButtonRow)
        };
        okButton.Click += (s, e) => { DialogResult = true; Hide(); };
        Controls.Add(okButton);

        var cancelButton = new Button(10, 1)
        {
            Text = "Cancel",
            Position = (okButton.Bounds.MaxExtentX + 2, okButton.Position.Y)
        };
        cancelButton.Click += (s, e) => { DialogResult = false; Hide(); };
        Controls.Add(cancelButton);

        // Validation errors
        var validationErrorsLabel = CreateLabel("Validation errors", 1, ValidationErrorLabelRow, "validationErrorsLabel");
        var validationErrorsListBox = new ListBox(Width - 3, 3)
        {
            Name = "validationErrorsListBox",
            Position = (1, ValidationErrorListRow),
            IsScrollBarVisible = true,
            IsEnabled = true,
        };
        Controls.Add(validationErrorsListBox);

        var validationErrorsHorizontalScrollBar = new ScrollBar(Orientation.Horizontal, Width - 4)
        {
            Name = "validationErrorsHorizontalScrollBar",
            Position = (1, ValidationErrorHorizontalScrollBarRow),
            MaximumValue = 0
        };
        validationErrorsHorizontalScrollBar.ValueChanged += (s, e) => RefreshValidationErrorsListBox();
        Controls.Add(validationErrorsHorizontalScrollBar);


        // Helper function to create a label and add it to the console
        Label CreateLabel(string text, int col, int row, string? name = null)
        {
            var labelTemp = new Label(text) { Position = new Point(col, row), Name = name };
            Controls.Add(labelTemp);
            return labelTemp;
        }

        // Force OnIsDirtyChanged event which will set control states (see SetControlStates)
        OnIsDirtyChanged();
    }

    private void OpenURL(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new Exception($"Invalid URL: {url}");
        // Launch the URL in the default browser
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private void ShowROMFilePickerDialog(string romName)
    {
        var currentFolder = PathHelper.ExpandOSEnvironmentVariables(C64SystemConfig.EffectiveROMDirectory);
        var window = new FilePickerConsole(FilePickerMode.OpenFile, currentFolder, C64SystemConfig.GetROM(romName).GetROMFilePath(currentFolder));
        window.Center();
        window.Closed += (s2, e2) =>
        {
            if (window.DialogResult)
            {
                if (window.SelectedFile is not FileInfo selectedFile)
                    return;

                C64SystemConfig.SetROM(romName, Path.GetFileName(selectedFile.FullName));
                IsDirty = true;
            }
        };
        window.Show(true);
    }

    private void ShowROMFolderPickerDialog()
    {
        var currentFolder = PathHelper.ExpandOSEnvironmentVariables(C64SystemConfig.EffectiveROMDirectory);
        var window = new FilePickerConsole(FilePickerMode.OpenFolder, currentFolder);
        window.Center();
        window.Closed += (s2, e2) =>
        {
            if (window.DialogResult)
            {
                C64SystemConfig.ROMDirectory = window.SelectedDirectory.FullName;
                IsDirty = true;
            }
        };
        window.Show(true);
    }

    private async Task AutoDownloadROMs()
    {
        var romFolder = PathHelper.ExpandOSEnvironmentVariables(C64SystemConfig.EffectiveROMDirectory);
        if (!Directory.Exists(romFolder))
        {
            Directory.CreateDirectory(romFolder);
        }

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        //httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");

        foreach (var romDownload in C64SystemConfig.ROMDownloadUrls)
        {
            var romName = romDownload.Key;
            var romUrl = romDownload.Value;
            var filename = Path.GetFileName(new Uri(romUrl).LocalPath);
            var dest = Path.Combine(romFolder, filename);
            try
            {
                using var response = await httpClient.GetAsync(romUrl);
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Failed to get '{romUrl}' ({(int)response.StatusCode})");
                await using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
                System.Console.WriteLine($"Downloaded {filename} to {dest}");

                // Update the C64SystemConfig with the downloaded ROM file
                C64SystemConfig.SetROM(romName, filename);
            }
            catch (Exception ex)
            {
                if (File.Exists(dest))
                    File.Delete(dest);
                throw new Exception($"Error downloading {romUrl}: {ex.Message}", ex);
            }
        }
    }

    protected override void OnIsDirtyChanged()
    {
        if (IsDirty)
            SetControlStates();
    }

    private void SetControlStates()
    {
        var romDirectoryTextBox = GetControlOrThrow<TextBox>("romDirectoryTextBox");
        romDirectoryTextBox.Text = C64SystemConfig.ROMDirectory;
        romDirectoryTextBox.IsDirty = true;

        var kernalROMTextBox = GetControlOrThrow<TextBox>("kernalROMTextBox");
        kernalROMTextBox.Text = C64SystemConfig.ROMs.Single(x => x.Name == C64SystemConfig.KERNAL_ROM_NAME).File!;
        kernalROMTextBox.IsDirty = true;

        var basicROMTextBox = GetControlOrThrow<TextBox>("basicROMTextBox");
        basicROMTextBox.Text = C64SystemConfig.ROMs.Single(x => x.Name == C64SystemConfig.BASIC_ROM_NAME).File!;
        basicROMTextBox.IsDirty = true;

        var chargenROMTextBox = GetControlOrThrow<TextBox>("chargenROMTextBox");
        chargenROMTextBox.Text = C64SystemConfig.ROMs.Single(x => x.Name == C64SystemConfig.CHARGEN_ROM_NAME).File!;
        chargenROMTextBox.IsDirty = true;

        var codingAssistantTestButton = GetControlOrThrow<Button>("codingAssistantTestButton");
        codingAssistantTestButton.IsEnabled = C64HostConfig.CodeSuggestionBackendType != CodeSuggestionBackendTypeEnum.None;

        var isOk = C64SystemConfig.IsValid(out List<string> validationErrors);
        var okButton = GetControlOrThrow<Button>("okButton");
        okButton.IsEnabled = isOk;

        var validationErrorsLabel = GetControlOrThrow<Label>("validationErrorsLabel");
        validationErrorsLabel.IsVisible = !isOk;
        var validationErrorsListBox = GetControlOrThrow<ListBox>("validationErrorsListBox");
        validationErrorsListBox.IsVisible = !isOk;
        var validationErrorsHorizontalScrollBar = GetControlOrThrow<ScrollBar>("validationErrorsHorizontalScrollBar");
        validationErrorsHorizontalScrollBar.IsVisible = !isOk;

        _validationErrors = validationErrors;
        var maxHorizontalOffset = _validationErrors.Count == 0
            ? 0
            : Math.Max(0, _validationErrors.Max(error => error.Length) - ValidationErrorListVisibleTextWidth);
        validationErrorsHorizontalScrollBar.MaximumValue = maxHorizontalOffset;
        validationErrorsHorizontalScrollBar.IsEnabled = maxHorizontalOffset > 0;
        if (validationErrorsHorizontalScrollBar.Value > maxHorizontalOffset)
            validationErrorsHorizontalScrollBar.Value = maxHorizontalOffset;

        RefreshValidationErrorsListBox();
    }

    private void RefreshValidationErrorsListBox()
    {
        var validationErrorsListBox = GetControlOrThrow<ListBox>("validationErrorsListBox");
        var validationErrorsHorizontalScrollBar = GetControlOrThrow<ScrollBar>("validationErrorsHorizontalScrollBar");
        var horizontalOffset = validationErrorsHorizontalScrollBar.Value;

        validationErrorsListBox.Items.Clear();
        foreach (var error in _validationErrors)
        {
            var visibleError = error.Length <= horizontalOffset
                ? string.Empty
                : error.Substring(horizontalOffset, Math.Min(ValidationErrorListVisibleTextWidth, error.Length - horizontalOffset));
            var coloredString = new ColoredString(visibleError, foreground: Color.Red, background: Color.Black);
            validationErrorsListBox.Items.Add(coloredString);
        }
    }

    private T GetControlOrThrow<T>(string name) where T : class
    {
        return Controls[name] as T ?? throw new InvalidOperationException($"Control '{name}' is not initialized.");
    }

    /// <summary>
    /// SadConsole's <see cref="ComboBox"/> parents its dropdown to one screen above the host control,
    /// so when this ComboBox is hosted on a modal <see cref="Window"/> (where <c>IsExclusiveMouse</c>
    /// is forced true to enforce modality) the dropdown never receives mouse clicks — they all get
    /// routed to the Window's own controls instead. This subclass relinquishes the Window's mouse
    /// exclusivity while the dropdown is open and restores the prior value when it closes.
    /// </summary>
    private sealed class ModalWindowComboBox : ComboBox
    {
        private readonly Window _window;
        private bool _previousExclusiveMouse;

        public ModalWindowComboBox(int width, int dropdownWidth, int dropdownHeight, object[] items, Window window)
            : base(width, dropdownWidth, dropdownHeight, items)
        {
            _window = window;
        }

        protected override void OnIsSelected()
        {
            base.OnIsSelected();
            if (IsSelected)
            {
                _previousExclusiveMouse = _window.IsExclusiveMouse;
                _window.IsExclusiveMouse = false;
            }
            else
            {
                _window.IsExclusiveMouse = _previousExclusiveMouse;
            }
        }
    }

    /// <summary>
    /// (Re)populate the audio target combo box so it only lists targets compatible with the
    /// current audio provider. Called once at initial layout and again whenever the provider
    /// combo's selection changes. The ComboBox is created on the first call and re-used on
    /// subsequent calls via <c>SetItems</c> — recreating the control and swapping it in and out
    /// of <see cref="Controls"/> leaves the new instance visually stale until the next IsDirty
    /// cycle, so we keep the same control instance instead.
    /// </summary>
    private void RebuildAudioTargetComboBox()
    {
        var audioTargetLabel = GetControlOrThrow<Label>("audioTargetLabel");

        var providerType = C64SystemConfig.AudioProviderType;
        var compatibleTargets = _audioCombinations
            .Where(c => c.audioProviderType == providerType)
            .Select(c => c.audioTargetType)
            .Distinct()
            .ToList();

        var currentTarget = C64SystemConfig.AudioTargetType;
        bool currentIsCompatible = currentTarget != null && compatibleTargets.Contains(currentTarget);

        // Display list: compatible targets plus the current one if it's NOT compatible (so the
        // user can see what's misconfigured). The auto-select logic further down always prefers
        // a compatible target so the C64SystemConfig is actually corrected when the user switches
        // providers — earlier versions merged the incompatible target into compatibleTargets and
        // then matched it during auto-select, which silently left the bad combination in place.
        _audioTargetDisplayTypes = new List<Type>(compatibleTargets);
        if (currentTarget != null && !currentIsCompatible)
            _audioTargetDisplayTypes.Add(currentTarget);

        var items = _audioTargetDisplayTypes.Count > 0
            ? _audioTargetDisplayTypes.Select(t => (object)TypeDisplayHelper.GetDisplayName(t)).ToArray()
            : new object[] { "(none)" };

        // Choose the target to settle on: prefer the current one if compatible, otherwise the
        // first compatible one. Only fall back to the (incompatible) current target if there are
        // no compatible targets at all — keeps the config from silently changing under the user
        // in that degenerate case.
        Type? settledTarget;
        if (currentIsCompatible)
            settledTarget = currentTarget;
        else if (compatibleTargets.Count > 0)
            settledTarget = compatibleTargets[0];
        else
            settledTarget = currentTarget;

        // Write the settled target back to config now (before the user clicks anything) so that
        // switching providers always leaves the (provider, target) pair in a valid combination.
        if (settledTarget != null && settledTarget != currentTarget)
            C64SystemConfig.SetAudioTargetType(settledTarget);

        // First-time creation. Subsequent calls just swap the items on the same control.
        // Allocate the dropdown panel large enough to hold every audio target the host could
        // ever surface, so SetItems on later calls (with potentially fewer items) doesn't need
        // a resize. The set of possible targets is small and fixed at compile time.
        if (_audioTargetComboBox == null)
        {
            var maxTargets = _audioCombinations.Select(c => c.audioTargetType).Distinct().Count();
            var initialDropdownHeight = Math.Max(items.Length, maxTargets) + 2;
            _audioTargetComboBox = new ModalWindowComboBox(
                width: 28,
                dropdownWidth: 32,
                dropdownHeight: initialDropdownHeight,
                items: items,
                window: this)
            {
                Name = "audioTargetComboBox",
                Position = (audioTargetLabel.Bounds.MaxExtentX + 2, audioTargetLabel.Position.Y),
            };
            _audioTargetComboBox.SelectedItemChanged += (s, e) =>
            {
                if (_audioTargetComboBox.SelectedItem is not string displayName)
                    return;
                var selected = _audioTargetDisplayTypes.FirstOrDefault(t => TypeDisplayHelper.GetDisplayName(t) == displayName);
                if (selected == null)
                    return;
                C64SystemConfig.SetAudioTargetType(selected);
                IsDirty = true;
            };
            Controls.Add(_audioTargetComboBox);
        }
        else
        {
            _audioTargetComboBox.SetItems(items);
        }

        _audioTargetComboBox.IsEnabled = _audioTargetDisplayTypes.Count > 0;

        if (settledTarget != null)
            _audioTargetComboBox.SelectedItem = TypeDisplayHelper.GetDisplayName(settledTarget);
    }
}
