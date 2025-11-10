using Highbyte.DotNet6502.Monitor;

namespace Highbyte.DotNet6502.Impl.Avalonia.Monitor;

public record MonitorEntry(string Text, MessageSeverity Severity, bool IsCommand);
