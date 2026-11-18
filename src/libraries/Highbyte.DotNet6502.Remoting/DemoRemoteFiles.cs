using System.Runtime.CompilerServices;
using Highbyte.DotNet6502.Systems.Recording;

namespace Highbyte.DotNet6502.Remoting;

/// <summary>Host-owned temporary recordings, shared across remote client connections.</summary>
internal static class DemoRemoteFiles
{
    private sealed class PendingFile(string path, string temporaryPath, FileStream stream)
    {
        public string Path { get; } = path;
        public string TemporaryPath { get; } = temporaryPath;
        public FileStream Stream { get; } = stream;
        public bool Completed { get; set; }
    }
    private static readonly ConditionalWeakTable<IRecordingHost, PendingFile> Files = new();

    public static async Task StartAsync(IRecordingHost host, string path)
    {
        if (Files.TryGetValue(host, out var pending))
        {
            if (pending.Completed || host.DemoSession?.Outcome != PlaybackOutcome.Failed)
                throw new InvalidOperationException("Save the preceding remote recording before starting another.");
            // Failed sessions have no completion manifest and cannot be saved as a demo.
            await pending.Stream.DisposeAsync();
            File.Delete(pending.TemporaryPath);
            Files.Remove(host);
        }
        var target = Path.GetFullPath(path);
        if (File.Exists(target)) throw new IOException("Demo already exists; choose a new path.");
        var directory = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".partial");
        var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        try
        {
            await host.StartDemoRecordingAsync(stream);
            Files.Add(host, new PendingFile(target, temporary, stream));
        }
        catch
        {
            await stream.DisposeAsync();
            File.Delete(temporary);
            throw;
        }
    }

    public static async Task SaveAsync(IRecordingHost host, string? path)
    {
        if (!Files.TryGetValue(host, out var pending))
            throw new InvalidOperationException("No remote file recording is pending.");
        if (!pending.Completed)
        {
            host.StopDemoRecording();
            await pending.Stream.FlushAsync();
            await pending.Stream.DisposeAsync();
            pending.Completed = true;
        }
        var target = path == null ? pending.Path : Path.GetFullPath(path);
        // Preserve a completed temporary file for retry if the target exists or publication fails.
        File.Move(pending.TemporaryPath, target, overwrite: false);
        Files.Remove(host);
    }
}
