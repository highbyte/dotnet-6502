using Highbyte.DotNet6502.Monitor;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Monitor;

public record MonitorEntry(string Text, MessageSeverity Severity, bool IsCommand);
