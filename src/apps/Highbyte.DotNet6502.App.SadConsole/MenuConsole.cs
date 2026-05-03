using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Utils;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;
using Microsoft.Extensions.Logging;
using static SadConsole.IFont;

namespace Highbyte.DotNet6502.App.SadConsole;
public class MenuConsole : ControlsConsole
{
    public const int CONSOLE_WIDTH = USABLE_WIDTH + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    public const int CONSOLE_HEIGHT = USABLE_HEIGHT + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    public const int USABLE_WIDTH = 23;
    private const int USABLE_HEIGHT = 17;

    private readonly SadConsoleHostApp _sadConsoleHostApp;
    private readonly ILogger _logger;

    public MenuConsole(SadConsoleHostApp sadConsoleHostApp, ILoggerFactory loggerFactory) : base(CONSOLE_WIDTH, CONSOLE_HEIGHT)
    {
        _sadConsoleHostApp = sadConsoleHostApp;
        _logger = loggerFactory.CreateLogger(typeof(MenuConsole).Name);

        // The UI font is set as default during program SadConsole startup.
        // Note: Not yet implemented changing of UI font and size. Currently it leads to issues in the layout.
        //FontSize = Font.GetFontSize(_sadConsoleHostApp.EmulatorConfig.UIFontSize);

        Controls.ThemeColors = SadConsoleUISettings.ThemeColors;
        Surface.DefaultBackground = Controls.ThemeColors.ControlHostBackground;
        Surface.DefaultForeground = Controls.ThemeColors.ControlHostForeground;
        Surface.Clear();

        FocusedMode = FocusBehavior.None;

        UseMouse = true;
        UseKeyboard = true;

        DrawUIItems();

        if (SadConsoleUISettings.UI_USE_CONSOLE_BORDER)
            Surface.DrawBox(new Rectangle(0, 0, Width, Height), SadConsoleUISettings.UIConsoleDrawBoxBorderParameters);
    }

    private void DrawUIItems()
    {
        var systemLabel = CreateLabel("System: ", 1, 1);
        ComboBox selectSystemComboBox = new ComboBox(12, 15, 4, _sadConsoleHostApp.AvailableSystemNames.ToArray())
        {
            Position = (systemLabel.Bounds.MaxExtentX + 2, systemLabel.Position.Y),
            Name = "selectSystemComboBox",
            SelectedItem = _sadConsoleHostApp.SelectedSystemName,
        };
        selectSystemComboBox.SelectedItemChanged += async (s, e) =>
        {
            if (selectSystemComboBox.SelectedItem is not string systemName)
                return;

            await _sadConsoleHostApp.SelectSystem(systemName);

            var selectSystemVariantComboBox = GetControlOrThrow<ComboBox>("selectSystemVariantComboBox");
            selectSystemVariantComboBox.SetItems(_sadConsoleHostApp.AllSelectedSystemConfigurationVariants.ToArray());
            selectSystemVariantComboBox.SelectedIndex = 0;
            IsDirty = true;
        };
        Controls.Add(selectSystemComboBox);

        var variantLabel = CreateLabel("Variant:", 1, systemLabel.Bounds.MaxExtentY + 1);
        ComboBox selectSystemVariantComboBox = new ComboBox(12, 15, 5, _sadConsoleHostApp.AllSelectedSystemConfigurationVariants.ToArray())
        {
            Position = (variantLabel.Bounds.MaxExtentX + 2, variantLabel.Position.Y),
            Name = "selectSystemVariantComboBox",
            SelectedItem = _sadConsoleHostApp.SelectedSystemConfigurationVariant,
        };
        selectSystemVariantComboBox.SelectedItemChanged += async (s, e) =>
        {
            if (selectSystemVariantComboBox.SelectedIndex >= 0
                && selectSystemVariantComboBox.SelectedItem is string configurationVariant)
            {
                await _sadConsoleHostApp.SelectSystemConfigurationVariant(configurationVariant);
                IsDirty = true;
            }
        };
        Controls.Add(selectSystemVariantComboBox);

        var statusLabel = CreateLabel("Status:", 1, variantLabel.Bounds.MaxExtentY + 1);
        CreateLabelValue(_sadConsoleHostApp.EmulatorState.ToString(), statusLabel.Bounds.MaxExtentX + 2, statusLabel.Position.Y, "statusValueLabel");

        var startButton = new Button("Start")
        {
            Name = "startButton",
            Position = (1, statusLabel.Bounds.MaxExtentY + 2),
        };
        startButton.Click += async (s, e) => { await _sadConsoleHostApp.Start(); IsDirty = true; };
        Controls.Add(startButton);

        var pauseButton = new Button("Pause")
        {
            Name = "pauseButton",
            Position = (11, startButton.Position.Y),
        };
        pauseButton.Click += (s, e) => { _sadConsoleHostApp.Pause(); IsDirty = true; };
        Controls.Add(pauseButton);

        var stopButton = new Button("Stop")
        {
            Name = "stopButton",
            Position = (1, startButton.Bounds.MaxExtentY + 1),
        };
        stopButton.Click += (s, e) => { _sadConsoleHostApp.Stop(); IsDirty = true; };
        Controls.Add(stopButton);

        var resetButton = new Button("Reset")
        {
            Name = "resetButton",
            Position = (11, stopButton.Position.Y),
        };
        resetButton.Click += async (s, e) => { await _sadConsoleHostApp.Reset(); IsDirty = true; };
        Controls.Add(resetButton);


        var monitorButton = new Button("Monitor (F12)")
        {
            Name = "monitorButton",
            Position = (1, resetButton.Position.Y + 2),
        };
        monitorButton.Click += (s, e) => { _sadConsoleHostApp.ToggleMonitor(); IsDirty = true; };
        Controls.Add(monitorButton);


        var infoButton = new Button("Info (F11)")
        {
            Name = "infoButton",
            Position = (1, monitorButton.Position.Y + 1),
        };
        infoButton.Click += (s, e) => { _sadConsoleHostApp.ToggleInfo(); IsDirty = true; };
        Controls.Add(infoButton);

        //var audioEnabledLabel = CreateLabel("Audio enabled:", 1, infoButton.Bounds.MaxExtentY + 2);
        var audioEnabledCheckBox = new CheckBox("Audio enabled")
        {
            Name = "audioEnabledCheckBox",
            Position = (1, infoButton.Bounds.MaxExtentY + 2),
            IsSelected = _sadConsoleHostApp.IsAudioSupported().Result ? _sadConsoleHostApp.IsAudioEnabled().Result : false,
        };
        audioEnabledCheckBox.IsSelectedChanged += async (s, e) => { await _sadConsoleHostApp.SetAudioEnabled(audioEnabledCheckBox.IsSelected); IsDirty = true; };
        Controls.Add(audioEnabledCheckBox);

        var audioVolumeLabel = CreateLabel("Vol:", 1, audioEnabledCheckBox.Bounds.MaxExtentY + 1, "audioVolumeLabel");
        var audioVolumeSlider = new ScrollBar(Orientation.Horizontal, 16)
        {
            Name = "audioVolumeSlider",
            Position = new Point(audioVolumeLabel.Bounds.MaxExtentX + 1, audioVolumeLabel.Position.Y),
            Value = _sadConsoleHostApp.EmulatorConfig.DefaultAudioVolumePercent,
            MaximumValue = 100
        };
        audioVolumeSlider.ValueChanged += (s, e) => { _sadConsoleHostApp.SetVolumePercent(audioVolumeSlider.Value); IsDirty = true; };
        Controls.Add(audioVolumeSlider);

        var fontSizeLabel = CreateLabel("Font size:", 1, audioVolumeSlider.Bounds.MaxExtentY + 2);
        ComboBox selectFontSizeBox = new ComboBox(9, 9, 5, Enum.GetValues<IFont.Sizes>().Select(x => (object)x).ToArray())
        {
            Position = (fontSizeLabel.Bounds.MaxExtentX + 2, fontSizeLabel.Position.Y),
            Name = "selectFontSizeComboBox",
            SelectedItem = Sizes.One,   // Will be overritten by SetEmulatorFontSize when a system is selected
        };
        selectFontSizeBox.SelectedItemChanged += (s, e) =>
        {
            if (e.Item is not IFont.Sizes fontSize)
                return;

            _sadConsoleHostApp.CommonHostSystemConfig.DefaultFontSize = fontSize;
            IsDirty = true;
        };
        Controls.Add(selectFontSizeBox);

        // Load Basic
        var loadBinaryButton = new Button("Load Binary .prg")
        {
            Name = "loadBinaryButton",
            Position = (1, fontSizeLabel.Bounds.MaxExtentY + 2),
        };
        loadBinaryButton.Click += LoadBinaryButton_Click;
        Controls.Add(loadBinaryButton);


        // Helper function to create a label and add it to the console
        Label CreateLabel(string text, int col, int row, string? name = null)
        {
            var labelTemp = new Label(text) { Position = new Point(col, row), Name = name };
            Controls.Add(labelTemp);
            return labelTemp;
        }
        Label CreateLabelValue(string text, int col, int row, string? name = null)
        {
            var labelTemp = new Label(text) { Position = new Point(col, row), TextColor = Controls.GetThemeColors().White, Name = name };
            Controls.Add(labelTemp);
            return labelTemp;
        }

        // Force OnIsDirtyChanged event which will set control states (see SetControlStates)
        OnIsDirtyChanged();
    }

    private void LoadBinaryButton_Click(object? sender, EventArgs e)
    {
        bool wasRunning = false;
        if (_sadConsoleHostApp.EmulatorState == EmulatorState.Running)
        {
            wasRunning = true;
            _sadConsoleHostApp.Pause();
        }

        var window = new FilePickerConsole(FilePickerMode.OpenFile, Environment.CurrentDirectory, filter: "*.*");
        window.Center();
        window.Closed += (s2, e2) =>
        {
            if (window.DialogResult)
            {
                try
                {
                    if (window.SelectedFile is not FileInfo selectedFile)
                        return;

                    var currentSystem = _sadConsoleHostApp.CurrentRunningSystem;
                    if (currentSystem == null)
                        return;

                    var fileName = selectedFile.FullName;
                    BinaryLoader.Load(
                        currentSystem.Mem,
                        fileName,
                        out ushort loadedAtAddress,
                        out ushort fileLength);

                    currentSystem.CPU.PC = loadedAtAddress;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error loading Binary .prg: {ex.Message}");
                }
            }

            if (wasRunning)
                _sadConsoleHostApp.Start().Wait();

            IsDirty = true;

        };
        window.Show(true);
    }

    protected async override void OnIsDirtyChanged()
    {
        if (IsDirty)
        {
            await SetControlStates();
        }
    }

    private async Task SetControlStates()
    {
        var systemComboBox = GetControlOrThrow<ComboBox>("selectSystemComboBox");
        systemComboBox.IsEnabled = _sadConsoleHostApp.EmulatorState == Systems.EmulatorState.Uninitialized;

        var selectSystemVariantComboBox = GetControlOrThrow<ComboBox>("selectSystemVariantComboBox");
        selectSystemVariantComboBox.IsEnabled = _sadConsoleHostApp.EmulatorState == Systems.EmulatorState.Uninitialized;

        var statusLabel = GetControlOrThrow<Label>("statusValueLabel");
        statusLabel.DisplayText = _sadConsoleHostApp.EmulatorState.ToString();

        var startButton = GetControlOrThrow<Button>("startButton");
        startButton.IsEnabled = _sadConsoleHostApp.IsSystemConfigValid().Result && _sadConsoleHostApp.EmulatorState != Systems.EmulatorState.Running;

        var pauseButton = GetControlOrThrow<Button>("pauseButton");
        pauseButton.IsEnabled = _sadConsoleHostApp.EmulatorState == Systems.EmulatorState.Running;

        var stopButton = GetControlOrThrow<Button>("stopButton");
        stopButton.IsEnabled = _sadConsoleHostApp.EmulatorState != Systems.EmulatorState.Uninitialized;

        var resetButton = GetControlOrThrow<Button>("resetButton");
        resetButton.IsEnabled = _sadConsoleHostApp.EmulatorState != Systems.EmulatorState.Uninitialized;

        var monitorButton = GetControlOrThrow<Button>("monitorButton");
        monitorButton.IsEnabled = _sadConsoleHostApp.EmulatorState != Systems.EmulatorState.Uninitialized;

        var audioEnabledCheckBox = GetControlOrThrow<CheckBox>("audioEnabledCheckBox");
        if (await _sadConsoleHostApp.IsAudioSupported())
        {
            audioEnabledCheckBox.IsEnabled = _sadConsoleHostApp.EmulatorState == Systems.EmulatorState.Uninitialized;
            audioEnabledCheckBox.IsSelected = await _sadConsoleHostApp.IsAudioEnabled();
        }
        else
        {
            audioEnabledCheckBox.IsEnabled = false;
            audioEnabledCheckBox.IsSelected = false;
        }

        var audioVolumeLabel = GetControlOrThrow<Label>("audioVolumeLabel");
        var audioVolumeSlider = GetControlOrThrow<ScrollBar>("audioVolumeSlider");
        audioVolumeSlider.IsEnabled = await _sadConsoleHostApp.IsAudioSupported() && await _sadConsoleHostApp.IsAudioEnabled();
        audioVolumeSlider.IsVisible = audioVolumeSlider.IsEnabled;
        audioVolumeLabel.IsVisible = audioVolumeSlider.IsEnabled;

        var selectFontSizeComboBox = GetControlOrThrow<ComboBox>("selectFontSizeComboBox");
        selectFontSizeComboBox.IsEnabled = _sadConsoleHostApp.EmulatorState == Systems.EmulatorState.Uninitialized;

        var loadBinaryButton = GetControlOrThrow<Button>("loadBinaryButton");
        loadBinaryButton.IsEnabled = _sadConsoleHostApp.EmulatorState != Systems.EmulatorState.Uninitialized;

        if (_sadConsoleHostApp.SystemMenuConsole != null)
            _sadConsoleHostApp.SystemMenuConsole.IsDirty = true;
    }

    internal void SetEmulatorFontSize(IFont.Sizes defaultFontSize)
    {
        var selectFontSizeComboBox = GetControlOrThrow<ComboBox>("selectFontSizeComboBox");
        selectFontSizeComboBox.SelectedItem = defaultFontSize;
        IsDirty = true;
    }

    private T GetControlOrThrow<T>(string name) where T : class
    {
        return Controls[name] as T ?? throw new InvalidOperationException($"Control '{name}' is not initialized.");
    }
}
