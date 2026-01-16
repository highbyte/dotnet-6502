using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

/// <summary>
/// Static logger factory accessor for use in Views and other classes
/// where dependency injection is not available (e.g., XAML-created Views).
/// Initialize this during application startup before Views are created.
/// </summary>
public static class AppLogger
{
    /// <summary>
    /// The application's logger factory. Set during application startup.
    /// </summary>
    public static ILoggerFactory? Factory { get; set; }

    /// <summary>
    /// Creates a logger with the specified name.
    /// Returns a NullLogger if the factory has not been initialized.
    /// </summary>
    public static ILogger CreateLogger(string name) =>
        Factory?.CreateLogger(name) ?? NullLogger.Instance;
}
