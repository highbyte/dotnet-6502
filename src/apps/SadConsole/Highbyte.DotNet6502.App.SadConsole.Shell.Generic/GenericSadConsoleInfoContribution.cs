using Highbyte.DotNet6502.App.SadConsole.Core;
using SadConsole.UI.Controls;
using SadRogue.Primitives;

namespace Highbyte.DotNet6502.App.SadConsole.Shell.Generic;

public sealed class GenericSadConsoleInfoContribution : ISadConsoleInfoContribution
{
    public string TabTitle => "Generic info";

    public Panel InfoPanel { get; } = BuildPanel();

    private static Panel BuildPanel()
    {
        var panel = new Panel(10, 10);
        var themeColors = SadConsoleUISettings.ThemeColors;
        var label = new Label("A generic 6502-based computer, with custom defined memory layout and IO functionality.")
        {
            Position = new Point(0, 0),
            TextColor = themeColors.ControlHostForeground
        };
        panel.Add(label);
        return panel;
    }
}
