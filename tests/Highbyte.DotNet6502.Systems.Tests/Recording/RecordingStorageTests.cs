using Highbyte.DotNet6502.Systems.Configuration;

namespace Highbyte.DotNet6502.Systems.Tests.Recording;

public class RecordingStorageTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Failed_write_preserves_destination_and_removes_temporary_file(bool overwrite)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "demo.d6502demo");
        try
        {
            await File.WriteAllTextAsync(path, "previous demo");
            await Assert.ThrowsAsync<IOException>(() => AtomicFileWriter.WriteAsync(path, async stream =>
            {
                await stream.WriteAsync(new byte[] { 1, 2, 3 });
                throw new IOException("Simulated storage failure");
            }, overwrite));
            Assert.Equal("previous demo", await File.ReadAllTextAsync(path));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Publication_without_overwrite_preserves_existing_destination()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "demo.d6502demo");
        try
        {
            await File.WriteAllTextAsync(path, "previous demo");
            await Assert.ThrowsAsync<IOException>(() => AtomicFileWriter.WriteAsync(path,
                stream => stream.WriteAsync(new byte[] { 1, 2, 3 }).AsTask()));
            Assert.Equal("previous demo", await File.ReadAllTextAsync(path));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
