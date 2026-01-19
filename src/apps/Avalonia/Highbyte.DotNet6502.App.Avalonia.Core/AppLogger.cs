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
    /// Format matches the default ILogger console output: "{Timestamp} {level}: {Category}[0] {Message}"
    /// with colored log level matching the default console logger colors.
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <param name="logLevel">
    /// The log level for the message. Default is <see cref="LogLevel.Information"/>.
    /// Messages with <see cref="LogLevel.Error"/> or <see cref="LogLevel.Critical"/> 
    /// are always written to console, even if console logging is disabled.
    /// </param>
    /// <param name="category">
    /// The category name for the log message. Default is "App".
    /// </param>
    public static void WriteBootstrapLog(string message, LogLevel logLevel = LogLevel.Information, string category = "App")
    {
        // Always write Error and Critical messages to console
        if (ConsoleLoggingEnabled || logLevel >= LogLevel.Error)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var (levelString, levelColor) = logLevel switch
            {
                LogLevel.Trace => ("trce", ConsoleColor.Gray),
                LogLevel.Debug => ("dbug", ConsoleColor.Gray),
                LogLevel.Information => ("info", ConsoleColor.Green),
                LogLevel.Warning => ("warn", ConsoleColor.Yellow),
                LogLevel.Error => ("fail", ConsoleColor.Red),
                LogLevel.Critical => ("crit", ConsoleColor.Red),
                _ => ("info", ConsoleColor.Green)
            };

            // Only use console colors on Desktop platforms - Browser throws PlatformNotSupportedException
            if (PlatformDetection.IsRunningOnDesktop())
            {
                Console.Write($"{timestamp} ");
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = levelColor;
                Console.Write(levelString);
                Console.ForegroundColor = originalColor;
                Console.WriteLine($": {category}[0] {message}");
            }
            else
            {
                Console.WriteLine($"{timestamp} {levelString}: {category}[0] {message}");
            }
        }
    }
}
