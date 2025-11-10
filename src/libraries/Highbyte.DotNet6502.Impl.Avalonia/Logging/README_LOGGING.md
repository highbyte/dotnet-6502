# Unified Logging: ILogger + Avalonia.Logging.Logger

## Overview

This implementation unifies `Microsoft.Extensions.Logging.ILogger` and `Avalonia.Logging.Logger` into a single logging destination using the bridge pattern.

## Problem Solved

Previously, Avalonia UI application had **two separate logging systems**:
1. **ILogger** - Used by third-party libraries and application code
2. **Avalonia.Logging.Logger** - Used by Avalonia framework for diagnostics

These logs went to different places, making it hard to get a complete application trace or configure logging from one place.

## Solution: AvaloniaLoggerBridge

Created `AvaloniaLoggerBridge` which implements Avalonia's `ILogSink` interface and routes all Avalonia logs through existing `ILogger` infrastructure.

### Files in This Directory

- **`AvaloniaLoggerBridge.cs`** - Core bridge implementation
- **`LoggingUsageExample.cs`** - Code examples and usage patterns
- **`README.md`** - This file (complete documentation)

## How It Works

```
┌─────────────────────────────────────────────────────────────┐
│ Application Code.                                           │
└──────────────────────────┬──────────────────────────────────┘
                           │
        ┌──────────────────┴──────────────────┐
        │                                     │
        ▼                                     ▼
   ILogger (Libraries)          Avalonia.Logging.Logger
        │                                     │
        │                         ┌───────────┘
        │                         │
        └─────────────────┬───────┘
                          │
                          ▼
                    LoggerFactory
                          │
                          ▼
                    AvaloniaLoggerBridge
                          │
                          ▼
                DotNet6502InMemLogStore
                   (or any ILogger provider)
```

### In Application

```csharp
// From any library using ILogger - goes to store
var logger = loggerFactory.CreateLogger<MyClass>();
logger.LogError("Something went wrong");

// From Avalonia framework - ALSO goes to store
if (global::Avalonia.Logging.Logger.TryGet(LogEventLevel.Warning, LogArea.Binding, out var log))
{
    log.Log(this, "Binding failed: {Property}", "MyProperty");
}

// Both appear in DotNet6502InMemLogStore!
```

## Configuration

In `Program.cs`, the bridge is initialized like this:

```csharp
// Create an ILogger for bridging Avalonia logs
var avaloniaILogger = loggerFactory.CreateLogger("Avalonia");
var avaloniaLoggerBridge = new AvaloniaLoggerBridge(avaloniaILogger, LogLevel.Debug);

// Set it as Avalonia's sink in AfterSetup
AppBuilder
    .Configure(...)
    .AfterSetup(_ => 
    {
        global::Avalonia.Logging.Logger.Sink = avaloniaLoggerBridge;
    });
```

### Adjust Log Level

```csharp
// More verbose
var bridge = new AvaloniaLoggerBridge(avaloniaILogger, LogLevel.Trace);

// Less verbose
var bridge = new AvaloniaLoggerBridge(avaloniaILogger, LogLevel.Warning);
```

## Log Level Mapping

The bridge automatically converts between log levels:

| Avalonia | ILogger |
|----------|---------|
| Verbose  | Trace   |
| Debug    | Debug   |
| Information | Information |
| Warning  | Warning |
| Error    | Error   |
| Fatal    | Critical |

## Usage Examples

### ILogger from Libraries
```csharp
var logger = loggerFactory.CreateLogger("App");
logger.LogInformation("Application started");
logger.LogError("Error: {Message}", message);
```

### Avalonia Framework Logs
```csharp
if (global::Avalonia.Logging.Logger.TryGet(LogEventLevel.Warning, LogArea.Binding, out var log))
{
    log.Log(this, "Binding issue: {Property}", "MyProperty");
}
```

### With Avalonia Areas
```csharp
var bindingLogger = loggerFactory.CreateLogger("Avalonia.Binding");
var layoutLogger = loggerFactory.CreateLogger("Avalonia.Layout");
```

## Add File Logging

Use standard .NET ILogger providers with Serilog or similar:

```csharp
// Install: dotnet add package Serilog.Sinks.File

var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.AddSerilog(new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.File("logs/app.log", rollingInterval: RollingInterval.Day)
        .WriteTo.Console()
        .CreateLogger());
});

var loggerFactory = services.BuildServiceProvider()
    .GetRequiredService<ILoggerFactory>();

// Both Avalonia and library logs now go to file automatically
var avaloniaILogger = loggerFactory.CreateLogger("Avalonia");
var bridge = new AvaloniaLoggerBridge(avaloniaILogger, LogLevel.Debug);
global::Avalonia.Logging.Logger.Sink = bridge;
```

## Remote Logging

Use standard ILogger providers like Application Insights or Sentry:

```csharp
services.AddLogging(builder =>
{
    builder.AddApplicationInsights();  // or AddSentry(), etc.
});

// Both logging systems automatically route to remote service
```

## Troubleshooting

**Q: Avalonia logs not appearing?**  
A: Ensure `Logger.Sink = bridge` is called in `AfterSetup()`, before rendering starts.

**Q: Performance impact?**  
A: Minimal. Disabled log levels short-circuit before the bridge runs.

**Q: Logs are duplicated?**  
A: Remove any `.LogToTrace()` or `.LogToDelegate()` calls if using the bridge.

**Q: How do I add file logging?**  
A: Use standard ILogger providers like Serilog, not custom sinks. See "Add File Logging" section above.

