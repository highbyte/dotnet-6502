using Highbyte.DotNet6502.Systems.Commodore64.Config;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;

namespace Highbyte.DotNet6502.App.SadConsole;
public class C64MenuConsole : ControlsConsole
{
    public const int CONSOLE_WIDTH = USABLE_WIDTH + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    public const int CONSOLE_HEIGHT = USABLE_HEIGHT + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    private const int USABLE_WIDTH = 21;
    private const int USABLE_HEIGHT = 12;

    private readonly SadConsoleHostApp _sadConsoleHostApp;
    private C64Config _c64Config => (C64Config)_sadConsoleHostApp.GetSystemConfig().Result;

    private C64MenuConsole(SadConsoleHostApp sadConsoleHostApp) : base(CONSOLE_WIDTH, CONSOLE_HEIGHT)
    {
        _sadConsoleHostApp = sadConsoleHostApp;
    }

    public static C64MenuConsole Create(SadConsoleHostApp sadConsoleHostApp)
    {
        var console = new C64MenuConsole(sadConsoleHostApp);

        console.Surface.DefaultForeground = SadConsoleUISettings.UIConsoleForegroundColor;
        console.Surface.DefaultBackground = SadConsoleUISettings.UIConsoleBackgroundColor;
        console.Clear();

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

        var c64ConfigButton = new Button("C64 Config")
        {
            Name = "c64ConfigButton",
            Position = (1, 1),
        };
        c64ConfigButton.Click += C64ConfigButton_Click;
        Controls.Add(c64ConfigButton);

        var validationMessageValueLabel = CreateLabelValue(new string(' ', 20), 1, c64ConfigButton.Bounds.MaxExtentY + 2, "validationMessageValueLabel");
        validationMessageValueLabel.TextColor = Controls.GetThemeColors().Red;

        // Helper function to create a label and add it to the console
        Label CreateLabel(string text, int col, int row, string? name = null)
        {
            var labelTemp = new Label(text) { Position = new Point(col, row), Name = name };
            Controls.Add(labelTemp);
            return labelTemp;
        }
        Label CreateLabelValue(string text, int col, int row, string? name = null)
        {
            var labelTemp = new Label(text) { Position = new Point(col, row), TextColor = Controls.GetThemeColors().Title, Name = name };
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
        var systemComboBox = Controls["c64ConfigButton"];
        systemComboBox.IsEnabled = _sadConsoleHostApp.EmulatorState == Systems.EmulatorState.Uninitialized;

        var validationMessageValueLabel = Controls["validationMessageValueLabel"] as Label;
        (bool isOk, List<string> validationErrors) = _sadConsoleHostApp.IsValidConfigWithDetails().Result;
        //validationMessageValueLabel!.DisplayText = isOk ? "" : string.Join(",", validationErrors!);
        validationMessageValueLabel!.DisplayText = isOk ? "" : "Config errors.";
        validationMessageValueLabel!.IsVisible = !isOk;
    }

    private void C64ConfigButton_Click(object sender, EventArgs e)
    {
        C64ConfigUIConsole window = C64ConfigUIConsole.Create(_sadConsoleHostApp);

        window.Center();
        window.Closed += (s2, e2) =>
        {
            if (window.DialogResult)
            {
                IsDirty = true;
                _sadConsoleHostApp.MenuConsole.IsDirty = true;
            }
        };
        window.Show(true);
    }
}
