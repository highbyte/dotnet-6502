using System.Runtime.CompilerServices;

namespace Highbyte.DotNet6502.Systems.Recording;

/// <summary>Lets inspection/debug surfaces enforce ownership even when they only hold an ISystem.</summary>
public static class RecordingSystemGuards
{
    private sealed record Owner(RecordingSession Session, Func<string, bool> QueueText);
    private static readonly ConditionalWeakTable<ISystem, Owner> Sessions = new();
    internal static void Bind(ISystem system, RecordingSession session, Func<string, bool> queueText)
    {
        Sessions.Remove(system);
        Sessions.Add(system, new Owner(session, queueText));
    }
    public static bool HasActiveSession(ISystem? system) => system != null
        && Sessions.TryGetValue(system, out var owner) && owner.Session.Mode != RecordingMode.Idle;
    public static void EnsureMutationAllowed(ISystem system)
    {
        if (HasActiveSession(system))
            throw new InvalidOperationException("Machine mutations are disabled during recording/replay.");
    }
    public static void EnsureMemoryInspectionAllowed(ISystem system, ushort address, int length)
    {
        if (Sessions.TryGetValue(system, out var owner) && owner.Session.Mode != RecordingMode.Idle)
            owner.Session.ValidateMemoryInspection(address, length);
    }

    public static bool TryRedirectRecordedText(ISystem system, string text)
    {
        if (!Sessions.TryGetValue(system, out var owner) || owner.Session.Mode == RecordingMode.Idle) return false;
        if (owner.Session.OwnsInput)
            throw new InvalidOperationException("Live text input is disabled during replay.");
        if (!owner.QueueText(text))
            throw new InvalidOperationException("Text cannot be represented by the recorded keyboard input.");
        return true;
    }
}
