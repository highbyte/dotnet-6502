using SadConsole.UI.Controls;

namespace Highbyte.DotNet6502.App.SadConsole.Core;

public interface ISadConsoleInfoContribution
{
    string TabTitle { get; }

    Panel InfoPanel { get; }
}
