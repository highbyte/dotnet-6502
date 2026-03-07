using Highbyte.DotNet6502.Scripting;
using MoonSharp.Interpreter;

namespace Highbyte.DotNet6502.Scripting.MoonSharp;

/// <summary>
/// MoonSharp-specific implementation of <see cref="AdapterScriptHandle"/>.
/// Holds the MoonSharp <see cref="Coroutine"/> representing a single loaded .lua file.
/// </summary>
internal sealed class MoonSharpScriptHandle : AdapterScriptHandle
{
    internal Coroutine Coroutine { get; set; }

    internal MoonSharpScriptHandle(string fileName, Coroutine coroutine)
        : base(fileName) => Coroutine = coroutine;
}
