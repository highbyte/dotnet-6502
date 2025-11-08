using System;
using Avalonia.Input;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

/// <summary>
/// Event args for host key events
/// </summary>
public class HostKeyEventArgs : EventArgs
{
    public Key Key { get; init; }
    public KeyModifiers KeyModifiers { get; init; }
}
