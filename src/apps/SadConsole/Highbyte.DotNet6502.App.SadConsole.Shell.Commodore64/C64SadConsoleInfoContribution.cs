using System.Diagnostics;
using Highbyte.DotNet6502.App.SadConsole.Core;
using SadConsole.UI.Controls;
using SadRogue.Primitives;

namespace Highbyte.DotNet6502.App.SadConsole.Shell.Commodore64;

public sealed class C64SadConsoleInfoContribution : ISadConsoleInfoContribution
{
    public string TabTitle => "C64 info";

    public Panel InfoPanel { get; } = BuildPanel();

    private static Panel BuildPanel()
    {
        var panel = new Panel(10, 10);
        var themeColors = SadConsoleUISettings.ThemeColors;
        const int colTab1 = 0;
        const int colTab2 = 30;
        const int colTab3 = 60;
        var row = 0;

        CreateLabel("C64 keyboard mapping", colTab1, row, themeColors.White);
        row++;
        CreateLabel("Command", colTab1, row, themeColors.Title);
        CreateLabel("C64 key", colTab2, row, themeColors.Title);
        CreateLabel("PC/Mac key", colTab3, row, themeColors.Title);
        row++;
        CreateLabel("Stop run Basic prg", colTab1, row, themeColors.ControlHostForeground);
        CreateLabel("Run/stop", colTab2, row, themeColors.ControlHostForeground);
        CreateLabel("Esc", colTab3, row, themeColors.ControlHostForeground);
        row++;
        CreateLabel("Soft reset", colTab1, row, themeColors.ControlHostForeground);
        CreateLabel("Run/Stop + Restore", colTab2, row, themeColors.ControlHostForeground);
        CreateLabel("Esc + PgUp (fn+ArrowUp on Mac)", colTab3, row, themeColors.ControlHostForeground);
        row++;
        CreateLabel("Change text color 1-8", colTab1, row, themeColors.ControlHostForeground);
        CreateLabel("CTRL + numbers 1-8", colTab2, row, themeColors.ControlHostForeground);
        CreateLabel("Tab + numbers 1-8", colTab3, row, themeColors.ControlHostForeground);
        row++;
        CreateLabel("Change text color 9-16", colTab1, row, themeColors.ControlHostForeground);
        CreateLabel("C= + numbers 1-8", colTab2, row, themeColors.ControlHostForeground);
        CreateLabel("LeftCtrl + numbers 1-8", colTab3, row, themeColors.ControlHostForeground);
        row++;
        CreateLabel("AI Basic: accept suggestion", colTab1, row, themeColors.ControlHostForeground);
        CreateLabel("CTRL", colTab2, row, themeColors.ControlHostForeground);
        CreateLabel("Tab", colTab3, row, themeColors.ControlHostForeground);
        row++;
        CreateLabel("AI Basic: ignore suggestion", colTab1, row, themeColors.ControlHostForeground);
        CreateLabel("Any other key than CTRL", colTab2, row, themeColors.ControlHostForeground);
        CreateLabel("Any other key than Tab", colTab3, row, themeColors.ControlHostForeground);
        row += 2;

        var keyboardDocButton = new Button("Full keyboard mapping (docs)")
        {
            Name = "openKeyboardDocURLButton",
            Position = new Point(colTab1, row),
        };
        keyboardDocButton.Click += (s, e) => OpenURL("https://highbyte.github.io/dotnet-6502/docs/systems/c64/keyboard/");
        panel.Add(keyboardDocButton);

        return panel;

        Label CreateLabel(string text, int col, int currentRow, Color? textColor = null, string? name = null)
        {
            var label = new Label(text)
            {
                Position = new Point(col, currentRow),
                Name = name,
                TextColor = textColor ?? themeColors.ControlHostForeground
            };
            panel.Add(label);
            return label;
        }
    }

    private static void OpenURL(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
