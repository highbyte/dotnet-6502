using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;

namespace Highbyte.DotNet6502.App.SadConsole;
public class MenuConsole : ControlsConsole
{
    public const int CONSOLE_WIDTH = USABLE_WIDTH + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    public const int CONSOLE_HEIGHT = USABLE_HEIGHT + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    private const int USABLE_WIDTH = 21;
    private const int USABLE_HEIGHT = 12;

    private readonly SadConsoleHostApp _sadConsoleHostApp;

    private MenuConsole(SadConsoleHostApp sadConsoleHostApp) : base(CONSOLE_WIDTH, CONSOLE_HEIGHT)
    {
        _sadConsoleHostApp = sadConsoleHostApp;
    }

    public static MenuConsole Create(SadConsoleHostApp sadConsoleHostApp)
    {
        var console = new MenuConsole(sadConsoleHostApp);

        console.Surface.DefaultForeground = SadConsoleUISettings.UIConsoleForegroundColor;
        console.Surface.DefaultBackground = SadConsoleUISettings.UIConsoleBackgroundColor;
        console.Clear();

        //FontSize = console.Font.GetFontSize(IFont.Sizes.One);
        //console.Surface.UsePrintProcessor = true;

        console.UseMouse = true;
        console.MouseMove += (s, e) =>
        {
        };
        console.UseKeyboard = true;

        console.DrawUIItems();

        if (SadConsoleUISettings.UI_USE_CONSOLE_BORDER)
            console.Surface.DrawBox(new Rectangle(0, 0, console.Width, console.Height), SadConsoleUISettings.ConsoleDrawBoxBorderParameters);

        return console;
    }

    private void DrawUIItems()
    {
        var systemLabel = CreateLabel("System:", 1, 1);
        ComboBox selectSystemComboBox = new ComboBox(10, 15, 4, _sadConsoleHostApp.AvailableSystemNames.ToArray())
        {
            Position = (systemLabel.Bounds.MaxExtentX + 2, systemLabel.Position.Y),
            Name = "selectSystemComboBox",
            SelectedItem = _sadConsoleHostApp.SelectedSystemName,
        };
        selectSystemComboBox.SelectedItemChanged += (s, e) => { _sadConsoleHostApp.SelectSystem(selectSystemComboBox.SelectedItem.ToString()); IsDirty = true; };
        Controls.Add(selectSystemComboBox);

        var statusLabel = CreateLabel("Status:", 1, systemLabel.Bounds.MaxExtentY + 1);
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

        var fontSizeLabel = CreateLabel("Font size:", 1, stopButton.Bounds.MaxExtentY + 2);
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

        var statusLabel = Controls["statusValueLabel"] as Label;
        statusLabel!.DisplayText = _sadConsoleHostApp.EmulatorState.ToString();

        var startButton = Controls["startButton"];
        startButton.IsEnabled = _sadConsoleHostApp.GetSystemConfig().Result.IsValid(out _) && _sadConsoleHostApp.EmulatorState != Systems.EmulatorState.Running;

        var pauseButton = Controls["pauseButton"];
        pauseButton.IsEnabled = _sadConsoleHostApp.EmulatorState == Systems.EmulatorState.Running;

        var stopButton = Controls["stopButton"]; ;
        stopButton.IsEnabled = _sadConsoleHostApp.EmulatorState != Systems.EmulatorState.Uninitialized;

        var resetButton = Controls["resetButton"];
        resetButton.IsEnabled = _sadConsoleHostApp.EmulatorState != Systems.EmulatorState.Uninitialized;

        var selectFontSizeComboBox = Controls["selectFontSizeComboBox"];
        selectFontSizeComboBox.IsEnabled = _sadConsoleHostApp.EmulatorState == Systems.EmulatorState.Uninitialized;

        if (_sadConsoleHostApp.SystemMenuConsole != null)
            _sadConsoleHostApp.SystemMenuConsole.IsDirty = true;
    }
}
