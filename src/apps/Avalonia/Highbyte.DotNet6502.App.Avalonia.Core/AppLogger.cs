using System;
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
    /// Whether console logging is enabled. When true, bootstrap Console.WriteLine
    /// messages will be written during app initialization (before ILogger is available).
    /// Set this before creating the App instance.
    /// </summary>
    public static bool ConsoleLoggingEnabled { get; set; }

    /// <summary>
    /// Creates a logger with the specified name.
    /// Returns a NullLogger if the factory has not been initialized.
    /// </summary>
    public static ILogger CreateLogger(string name) =>
        Factory?.CreateLogger(name) ?? NullLogger.Instance;

    /// <summary>
    /// Writes a message to the console if console logging is enabled.
    /// Use this for bootstrap logging before ILogger is available.
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <param name="logLevel">
    /// The log level for the message. Default is <see cref="LogLevel.Information"/>.
    /// Messages with <see cref="LogLevel.Error"/> or <see cref="LogLevel.Critical"/> 
    /// are always written to console, even if console logging is disabled.
    /// </param>
    public static void WriteBootstrapLog(string message, LogLevel logLevel = LogLevel.Information)
    {
        // Always write Error and Critical messages to console
        if (ConsoleLoggingEnabled || logLevel >= LogLevel.Error)
        {
            var prefix = logLevel >= LogLevel.Error ? $"[{logLevel}] " : "";
            Console.WriteLine($"{prefix}{message}");
        }
    }
}
