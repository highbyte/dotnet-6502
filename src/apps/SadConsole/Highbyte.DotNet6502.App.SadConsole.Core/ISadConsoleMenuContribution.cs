using SadConsole.Input;
using SadConsole.UI;
using SadConsole.UI.Controls;

namespace Highbyte.DotNet6502.App.SadConsole.Core;

public interface ISadConsoleMenuContribution
{
    ControlsConsole Console { get; }

    Task HandleKeyReleased(Keys key);
}
