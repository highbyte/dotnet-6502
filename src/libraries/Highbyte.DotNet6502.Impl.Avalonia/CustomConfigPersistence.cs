using System;
using System.Threading.Tasks;

namespace Highbyte.DotNet6502.Impl.Avalonia;

/// <summary>
/// DI-friendly carrier for the host's "save custom config JSON" delegate.
/// Lets shell plug-ins (which construct per-system <c>ISystemConfigurer</c>s) read the
/// delegate from DI without depending on <see cref="AvaloniaHostApp"/>, which avoids a
/// resolution cycle during boot (the host is constructed after plug-in-provided
/// configurers are resolved).
/// </summary>
/// <param name="Save">
/// (sectionName, json, optionalFileName) → Task. Null when the host does not support
/// custom config persistence (e.g. a stripped-down test harness).
/// </param>
public sealed record CustomConfigPersistence(Func<string, string, string?, Task>? Save);
