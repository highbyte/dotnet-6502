using System;
using System.IO;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Services;

/// <summary>Whether a file can be written, and if not, something specific to tell the user.</summary>
/// <param name="CanWrite">True when the file was successfully opened for writing.</param>
/// <param name="Reason">Why not, phrased for a tooltip. Null when <paramref name="CanWrite"/> is true.</param>
public sealed record FileWritability(bool CanWrite, string? Reason)
{
    public static FileWritability Writable { get; } = new(true, null);

    /// <summary>
    /// Probes by opening the file for writing and closing it again.
    ///
    /// <para>Deliberately not <c>FileInfo.IsReadOnly</c>, which only reports the DOS read-only
    /// attribute: it says nothing about POSIX permissions, a file on read-only media, or a file
    /// another process holds open. Those all end with the same failed write, and a checkbox that
    /// offers to enable writing and then cannot is worse than one that explains itself up front.</para>
    /// </summary>
    public static FileWritability Probe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new FileWritability(false, "This disk did not come from a file, so there is nothing to write back to.");

        if (!File.Exists(path))
            return new FileWritability(false, $"The file no longer exists: {path}");

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            return Writable;
        }
        catch (UnauthorizedAccessException)
        {
            return new FileWritability(false, "The file is read-only, or you do not have permission to write to it.");
        }
        catch (IOException)
        {
            return new FileWritability(false, "The file is in use by another program.");
        }
        catch (Exception ex)
        {
            return new FileWritability(false, $"The file cannot be written: {ex.Message}");
        }
    }
}
