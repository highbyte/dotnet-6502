namespace Highbyte.DotNet6502.Systems.Configuration;

/// <summary>Publish only a fully written file; failures preserve the existing destination.</summary>
public static class AtomicFileWriter
{
    public static async Task WriteAsync(string path, Func<Stream, Task> write, bool overwrite = false)
    {
        var target = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await write(output);
                await output.FlushAsync();
            }
            File.Move(temporary, target, overwrite);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
