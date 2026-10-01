using System;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Services;

/// <summary>Optional platform adapter for committed text and mobile editing events.</summary>
public interface INativeKeyboardInputBridge
{
    bool Start(Action<string> onText, Action onFocusLost);
    void Stop();
}
