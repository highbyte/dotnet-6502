using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;

namespace Highbyte.DotNet6502.App.SadConsole;
public class MenuConsole : ControlsConsole
{
    public const int CONSOLE_WIDTH = USABLE_WIDTH + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    public const int CONSOLE_HEIGHT = USABLE_HEIGHT + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    private const int USABLE_WIDTH = 21;
    private const int USABLE_HEIGHT = 15;

    private readonly SadConsoleHostApp _sadConsoleHostApp;

    public MenuConsole(SadConsoleHostApp sadConsoleHostApp) : base(CONSOLE_WIDTH, CONSOLE_HEIGHT)
    {
        _sadConsoleHostApp = sadConsoleHostApp;

        Controls.ThemeColors = SadConsoleUISettings.ThemeColors;
        Surface.DefaultBackground = Controls.ThemeColors.ControlHostBackground;
        Surface.DefaultForeground = Controls.ThemeColors.ControlHostForeground;
        Surface.Clear();

        FocusedMode = FocusBehavior.None;

        UseMouse = true;
        UseKeyboard = true;

        DrawUIItems();

        if (SadConsoleUISettings.UI_USE_CONSOLE_BORDER)
            Surface.DrawBox(new Rectangle(0, 0, Width, Height), SadConsoleUISettings.ConsoleDrawBoxBorderParameters);
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
            await _sadConsoleHostApp.SelectSystem(selectSystemComboBox.SelectedItem.ToString());
            await _sadConsoleHostApp.SelectSystemConfigurationVariant(_sadConsoleHostApp.CurrentSystemConfigurationVariants.First());

            var selectSystemVariantComboBox = Controls["selectSystemVariantComboBox"] as ComboBox;
            selectSystemVariantComboBox.SetItems(_sadConsoleHostApp.CurrentSystemConfigurationVariants.ToArray());
            selectSystemVariantComboBox.SelectedIndex = 0;
            IsDirty = true;
        };
        Controls.Add(selectSystemComboBox);

        var variantLabel = CreateLabel("Variant:", 1, systemLabel.Bounds.MaxExtentY + 1);
        ComboBox selectSystemVariantComboBox = new ComboBox(12, 15, 5, _sadConsoleHostApp.CurrentSystemConfigurationVariants.ToArray())
        {
            Position = (variantLabel.Bounds.MaxExtentX + 2, variantLabel.Position.Y),
            Name = "selectSystemVariantComboBox",
            SelectedItem = _sadConsoleHostApp.SelectedSystemConfigurationVariant,
        };
        selectSystemVariantComboBox.SelectedItemChanged += async (s, e) =>
        {
            if (selectSystemVariantComboBox.SelectedIndex >= 0)
            {
                await _sadConsoleHostApp.SelectSystemConfigurationVariant(selectSystemVariantComboBox.SelectedItem.ToString());
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
            IsSelected = _sadConsoleHostApp.IsAudioSupported ? _sadConsoleHostApp.IsAudioEnabled : false,
        };
        audioEnabledCheckBox.IsSelectedChanged += (s, e) => { _sadConsoleHostApp.IsAudioEnabled = audioEnabledCheckBox.IsSelected; IsDirty = true; };
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
            SelectedItem = _sadConsoleHostApp.EmulatorConfig.FontSize,
        };
        selectFontSizeBox.SelectedItemChanged += (s, e) => { _sadConsoleHostApp.EmulatorConfig.FontSize = (IFont.Sizes)e.Item; IsDirty = true; };
        Controls.Add(selectFontSizeBox);

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

    protected override void OnIsDirtyChanged()
    {
        if (IsDirty)
        {
            SetControlStates();
        }
    }

    private void SetControlStates()
    {
        var systemComboBox = Controls["selectSystemComboBox"];
        systemComboBox.IsEnabled = _sadConsoleHostApp.EmulatorState == Systems.EmulatorState.Uninitialized;

        var selectSystemVariantComboBox = Controls["selectSystemVariantComboBox"];
        selectSystemVariantComboBox.IsEnabled = _sadConsoleHostApp.EmulatorState == Systems.EmulatorState.Uninitialized;

        var statusLabel = Controls["statusValueLabel"] as Label;
        statusLabel!.DisplayText = _sadConsoleHostApp.EmulatorState.ToString();

        var startButton = Controls["startButton"];
        startButton.IsEnabled = _sadConsoleHostApp.IsSystemConfigValid().Result && _sadConsoleHostApp.EmulatorState != Systems.EmulatorState.Running;

        var pauseButton = Controls["pauseButton"];
        pauseButton.IsEnabled = _sadConsoleHostApp.EmulatorState == Systems.EmulatorState.Running;

        var stopButton = Controls["stopButton"];
        stopButton.IsEnabled = _sadConsoleHostApp.EmulatorState != Systems.EmulatorState.Uninitialized;

        var resetButton = Controls["resetButton"];
        resetButton.IsEnabled = _sadConsoleHostApp.EmulatorState != Systems.EmulatorState.Uninitialized;

        var monitorButton = Controls["monitorButton"];
        monitorButton.IsEnabled = _sadConsoleHostApp.EmulatorState != Systems.EmulatorState.Uninitialized;

        var audioEnabledCheckBox = Controls["audioEnabledCheckBox"] as CheckBox;
        if (_sadConsoleHostApp.IsAudioSupported)
        {
            audioEnabledCheckBox.IsEnabled = _sadConsoleHostApp.EmulatorState == Systems.EmulatorState.Uninitialized;
            audioEnabledCheckBox.IsSelected = _sadConsoleHostApp.IsAudioEnabled;
        }
        else
        {
            audioEnabledCheckBox.IsEnabled = false;
            audioEnabledCheckBox.IsSelected = false;
        }

        var audioVolumeLabel = Controls["audioVolumeLabel"];
        var audioVolumeSlider = Controls["audioVolumeSlider"];
        audioVolumeSlider.IsEnabled = _sadConsoleHostApp.IsAudioSupported && _sadConsoleHostApp.IsAudioEnabled;
        audioVolumeSlider.IsVisible = audioVolumeSlider.IsEnabled;
        audioVolumeLabel.IsVisible = audioVolumeSlider.IsEnabled;

        var selectFontSizeComboBox = Controls["selectFontSizeComboBox"];
        selectFontSizeComboBox.IsEnabled = _sadConsoleHostApp.EmulatorState == Systems.EmulatorState.Uninitialized;

        if (_sadConsoleHostApp.SystemMenuConsole != null)
            _sadConsoleHostApp.SystemMenuConsole.IsDirty = true;
    }
}
