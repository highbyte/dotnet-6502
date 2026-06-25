using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Services;

public sealed record AppPickedFile(string Name, byte[] Bytes);

public sealed record AppFilePickerFileType(string Name, IReadOnlyList<string> Patterns)
{
    public static AppFilePickerFileType AllFiles { get; } = new("All Files", ["*"]);
}

public sealed record AppFilePickerOpenOptions(
    string Title,
    bool AllowMultiple,
    IReadOnlyList<AppFilePickerFileType> FileTypes)
{
    public string ToBrowserAccept()
    {
        return string.Join(
            ",",
            FileTypes
                .SelectMany(fileType => fileType.Patterns)
                .Select(ToBrowserAcceptPattern)
                .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string ToBrowserAcceptPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || pattern == "*")
            return string.Empty;

        return pattern.StartsWith("*.", StringComparison.Ordinal)
            ? pattern[1..]
            : pattern;
    }
}

/// <summary>
/// App-level open-file picker used by Avalonia views that need local file bytes.
/// </summary>
/// <remarks>
/// File dialogs are UI/platform interactions, so ViewModels should request files from their Views
/// instead of opening Avalonia dialogs directly. This abstraction keeps that boundary while avoiding
/// scattered direct StorageProvider calls. It also lets the Browser host replace Avalonia's browser
/// StorageProvider open-file path: in practice, cancelled browser file dialogs can fail to complete
/// awaited command flows, leaving ReactiveCommand-backed buttons disabled. Centralizing the picker
/// gives every open-file UI the same cancel semantics on Desktop and Browser.
/// </remarks>
public interface IAppFilePicker
{
    Task<AppPickedFile?> OpenFileAsync(Control owner, AppFilePickerOpenOptions options);
    Task<IReadOnlyList<AppPickedFile>> OpenFilesAsync(Control owner, AppFilePickerOpenOptions options);
}

/// <summary>
/// Default Desktop-capable implementation backed by Avalonia's StorageProvider.
/// </summary>
public sealed class AvaloniaStorageAppFilePicker : IAppFilePicker
{
    public async Task<AppPickedFile?> OpenFileAsync(Control owner, AppFilePickerOpenOptions options)
    {
        var files = await OpenFilesAsync(owner, options with { AllowMultiple = false });
        return files.Count == 0 ? null : files[0];
    }

    public async Task<IReadOnlyList<AppPickedFile>> OpenFilesAsync(Control owner, AppFilePickerOpenOptions options)
    {
        if (TopLevel.GetTopLevel(owner) is not { } topLevel ||
            !topLevel.StorageProvider.CanOpen)
        {
            return [];
        }

        try
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = options.Title,
                AllowMultiple = options.AllowMultiple,
                FileTypeFilter = options.FileTypes
                    .Select(ToAvaloniaFileType)
                    .ToArray()
            });

            var pickedFiles = new List<AppPickedFile>(files.Count);
            foreach (var file in files)
            {
                await using var stream = await file.OpenReadAsync();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer);
                pickedFiles.Add(new AppPickedFile(file.Name, buffer.ToArray()));
            }

            return pickedFiles;
        }
        catch (OperationCanceledException)
        {
            return [];
        }
    }

    private static FilePickerFileType ToAvaloniaFileType(AppFilePickerFileType fileType)
    {
        if (fileType.Patterns.Count == 1 && fileType.Patterns[0] == "*")
            return FilePickerFileTypes.All;

        return new FilePickerFileType(fileType.Name)
        {
            Patterns = fileType.Patterns
        };
    }
}
