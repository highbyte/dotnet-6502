using System.Text.Json;
using Highbyte.DotNet6502.Systems.Configuration;

namespace Highbyte.DotNet6502.Systems.Tests.Configuration;

public class AppStoragePathsTest
{
    [Fact]
    public void UserSettingsFilePath_UsesHostSpecificAppFolder()
    {
        var path = AppStoragePaths.GetUserSettingsFilePath("Avalonia.Desktop");

        Assert.EndsWith(
            Path.Combine(AppStoragePaths.CompanyFolderName, AppStoragePaths.AppFolderName, "Avalonia.Desktop", AppStoragePaths.UserSettingsFileName),
            path);
    }

    [Fact]
    public void UserContentDirectories_UseDocumentsAppFolder()
    {
        Assert.EndsWith(Path.Combine(AppStoragePaths.CompanyFolderName, AppStoragePaths.AppFolderName, "scripts"), AppStoragePaths.GetScriptsDirectory());
        Assert.EndsWith(Path.Combine(AppStoragePaths.CompanyFolderName, AppStoragePaths.AppFolderName, "snapshots"), AppStoragePaths.GetSnapshotsDirectory());
        Assert.EndsWith(Path.Combine(AppStoragePaths.CompanyFolderName, AppStoragePaths.AppFolderName, "roms", "C64"), AppStoragePaths.GetRomDirectory("C64"));
    }

    [Fact]
    public void SharedUserContentDirectories_Include_CategoryRoots()
    {
        var directories = AppStoragePaths.GetSharedUserContentDirectories().ToList();

        Assert.Contains(AppStoragePaths.GetUserContentRoot(), directories);
        Assert.Contains(Path.Combine(AppStoragePaths.GetUserContentRoot(), "roms"), directories);
        Assert.Contains(AppStoragePaths.GetScriptsDirectory(), directories);
        Assert.Contains(AppStoragePaths.GetSnapshotsDirectory(), directories);
    }

    [Fact]
    public void DownloadCacheDirectory_UsesLocalAppDataCacheFolder()
    {
        var cacheRoot = AppStoragePaths.GetCacheRoot();
        var downloads = AppStoragePaths.GetDownloadCacheDirectory();

        Assert.EndsWith(Path.Combine(AppStoragePaths.CompanyFolderName, AppStoragePaths.AppFolderName, "cache"), cacheRoot);
        Assert.EndsWith(Path.Combine(AppStoragePaths.CompanyFolderName, AppStoragePaths.AppFolderName, "cache", "downloads"), downloads);
        Assert.StartsWith(cacheRoot, downloads);

        // Cache is machine-local (LocalApplicationData), not under the user-facing content root (MyDocuments).
        Assert.DoesNotContain(AppStoragePaths.GetUserContentRoot(), cacheRoot);
    }

    [Fact]
    public async Task MergeSectionAsync_CreatesFileAndMergesNestedSection()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dotnet6502-settings-{Guid.NewGuid():N}");
        var settingsPath = Path.Combine(directory, AppStoragePaths.UserSettingsFileName);

        try
        {
            await AppSettingsUserFile.MergeSectionAsync(
                settingsPath,
                "Highbyte.DotNet6502.AvaloniaConfig",
                """
                {
                  "DefaultEmulator": "C64",
                  "Monitor": {
                    "StopAfterBRKInstruction": false
                  }
                }
                """);

            await AppSettingsUserFile.MergeSectionAsync(
                settingsPath,
                "Highbyte.DotNet6502.AvaloniaConfig",
                """
                {
                  "ShowDebugTools": true,
                  "Monitor": {
                    "StopAfterUnknownInstruction": false
                  }
                }
                """);

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
            var section = document.RootElement.GetProperty("Highbyte.DotNet6502.AvaloniaConfig");
            Assert.Equal("C64", section.GetProperty("DefaultEmulator").GetString());
            Assert.True(section.GetProperty("ShowDebugTools").GetBoolean());

            var monitor = section.GetProperty("Monitor");
            Assert.False(monitor.GetProperty("StopAfterBRKInstruction").GetBoolean());
            Assert.False(monitor.GetProperty("StopAfterUnknownInstruction").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
