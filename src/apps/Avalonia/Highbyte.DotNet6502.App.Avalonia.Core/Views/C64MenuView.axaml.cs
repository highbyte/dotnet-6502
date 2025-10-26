using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Render.CustomPayload;
using Highbyte.DotNet6502.Systems.Commodore64.Render.Rasterizer;
using Highbyte.DotNet6502.Systems.Commodore64.Render.VideoCommands;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64.Download;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class C64MenuView : UserControl
{
    // Access ViewModel and HostApp through DataContext
    private C64MenuViewModel? ViewModel => DataContext as C64MenuViewModel;
    private AvaloniaHostApp? HostApp => ViewModel?.HostApp;

    // Parameterless constructor for XAML compatibility
    public C64MenuView()
    {
        InitializeComponent();

        UpdateSectionStatesIfNeeded();

        // Subscribe to visibility property changes to update section states when view becomes visible
        this.PropertyChanged += (s, e) =>
        {
            // When the IsVisible property changes to true, update section states if needed
            if (e.Property == IsVisibleProperty && this.IsVisible && ViewModel != null)
            {
                UpdateSectionStatesIfNeeded();
            }
        };

        this.DataContextChanged += (s, e) =>
        {
            // When DataContext changes, update section states if needed
            UpdateSectionStatesIfNeeded();
        };
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
    }

    private void UpdateSectionStatesIfNeeded()
    {
        // If there are validation errors, expand config section and collapse others
        if (ViewModel != null && ViewModel.HasConfigValidationErrors)
        {
            // Collapse Disk Section
            var diskHeaderButton = this.FindControl<Button>("DiskSectionHeader");
            var diskContentBorder = this.FindControl<Border>("DiskSectionContent");
            if (diskHeaderButton != null && diskContentBorder != null)
            {
                SetSectionState(diskHeaderButton, diskContentBorder, expanded: false);
            }

            // Collapse Load/Save Section
            var loadSaveHeaderButton = this.FindControl<Button>("LoadSaveSectionHeader");
            var loadSaveContentBorder = this.FindControl<Border>("LoadSaveSectionContent");
            if (loadSaveHeaderButton != null && loadSaveContentBorder != null)
            {
                SetSectionState(loadSaveHeaderButton, loadSaveContentBorder, expanded: false);
            }

            // Expand Config Section
            var configHeaderButton = this.FindControl<Button>("ConfigSectionHeader");
            var configContentBorder = this.FindControl<Border>("ConfigSectionContent");
            if (configHeaderButton != null && configContentBorder != null)
            {
                SetSectionState(configHeaderButton, configContentBorder, expanded: true);
            }
        }
    }

    private async void OpenC64Config_Click(object? sender, RoutedEventArgs e)
    {
        if (HostApp == null)
            return;

        if (HostApp.CurrentHostSystemConfig is not C64HostConfig c64HostConfig)
            return;

        var renderProviderOptions = HostApp.GetAvailableSystemRenderProviderTypesAndRenderTargetTypeCombinations();

        // Check if running on WASM/Browser platform
        if (PlatformDetection.IsRunningInWebAssembly())
        {
            // For WASM, use ContentDialog instead of Window
            await ShowC64ConfigContentDialog(c64HostConfig, renderProviderOptions);
        }
        else
        {
            // For desktop platforms, use the Window dialog
            await ShowC64ConfigDialog(c64HostConfig, renderProviderOptions);
        }
    }

    private async Task ShowC64ConfigDialog(C64HostConfig c64HostConfig,
        List<(System.Type renderProviderType, System.Type renderTargetType)> renderProviderOptions)
    {
        var dialog = new C64ConfigDialog(HostApp!, c64HostConfig, renderProviderOptions);

        bool? result;
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            result = await dialog.ShowDialog<bool?>(owner);
        }
        else
        {
            var tcs = new TaskCompletionSource<bool?>();
            dialog.Closed += (_, _) => tcs.TrySetResult(dialog.DialogResultValue);
            dialog.Show();
            result = await tcs.Task;
        }

        if (result == true)
        {
            // Notify C64MenuViewModel of state changes
            if (ViewModel is C64MenuViewModel viewModel)
            {
                viewModel.NotifyEmulatorStateChanged();
            }

            // Notify MainViewModel to refresh validation errors
            NotifyMainViewModelOfConfigChange();
        }
    }

    private async Task ShowC64ConfigContentDialog(C64HostConfig c64HostConfig,
        List<(System.Type renderProviderType, System.Type renderTargetType)> renderProviderOptions)
    {
        // Create the UserControl-based config
        var configControl = new C64ConfigUserControl(HostApp!, c64HostConfig, renderProviderOptions);

        // Create a custom overlay with better modal behavior
        var overlay = new Panel
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), // More opaque overlay
            ZIndex = 1000
        };

        // Create a dialog container that looks like a proper modal
        var dialogContainer = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 42)), // Dark gray background
            BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 8,
                Blur = 25,
                Color = Color.FromArgb(128, 0, 0, 0)
            }),
            Margin = new Thickness(20), // Add margin from screen edges
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = configControl // Direct child, no ScrollViewer wrapper
        };

        // Set up event handling for configuration completion
        var taskCompletionSource = new TaskCompletionSource<bool>();
        configControl.ConfigurationChanged += (s, saved) =>
        {
            taskCompletionSource.SetResult(saved);
        };

        overlay.Children.Add(dialogContainer);

        // Get the parent main Grid and add overlay
        // Find the root MainView's Grid by walking up the visual tree
        var root = this.GetVisualRoot();
        Grid? mainGrid = null;
        
        if (root is Window window && window.Content is Grid contentGrid)
        {
            mainGrid = contentGrid;
        }
        
        if (mainGrid == null)
        {
            mainGrid = this.FindAncestorOfType<MainView>(true)?.Content as Grid;
        }

        if (mainGrid != null)
        {
            Grid.SetRowSpan(overlay, mainGrid.RowDefinitions.Count > 0 ? mainGrid.RowDefinitions.Count : 1);
            Grid.SetColumnSpan(overlay, mainGrid.ColumnDefinitions.Count > 0 ? mainGrid.ColumnDefinitions.Count : 1);
            mainGrid.Children.Add(overlay);

            try
            {
                // Wait for the configuration to complete
                var result = await taskCompletionSource.Task;

                if (result)
                {
                    // Notify C64MenuViewModel of state changes
                    ViewModel?.NotifyEmulatorStateChanged();

                    // Notify MainViewModel to refresh validation errors
                    NotifyMainViewModelOfConfigChange();
                }
            }
            finally
            {
                // Clean up - remove the overlay
                mainGrid.Children.Remove(overlay);
            }
        }
    }

    /// <summary>
    /// Helper method to notify MainViewModel to refresh validation errors after configuration changes
    /// </summary>
    private void NotifyMainViewModelOfConfigChange()
    {
        try
        {
            // Find the parent MainView
            var mainView = this.FindAncestorOfType<MainView>(true);
            if (mainView?.DataContext is MainViewModel mainViewModel)
            {
                // Force a full state refresh to update validation errors
                mainViewModel.ForceStateRefresh();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error notifying MainViewModel of config change: {ex.Message}");
        }
    }

    // C64-specific event handlers
    private async void CopyBasicSource_Click(object? sender, RoutedEventArgs e)
    {
        await CopyBasicSourceCode();
    }

    private async void PasteText_Click(object? sender, RoutedEventArgs e)
    {
        await PasteText();
    }

    private async void ToggleDiskImage_Click(object? sender, RoutedEventArgs e)
    {
        await ToggleDiskImage();
    }

    private void OpenBasicAssistantInfo_Click(object? sender, RoutedEventArgs e)
    {
        // Open the info link for Basic coding assistant
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            topLevel.Launcher.LaunchUriAsync(new Uri("https://github.com/highbyte/dotnet-6502/blob/master/doc/SYSTEMS_C64_AI_CODE_COMPLETION.md"));
        }
    }

    private void OpenDiskInfo_Click(object? sender, RoutedEventArgs e)
    {
        // Open the info link for disk functionality
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            topLevel.Launcher.LaunchUriAsync(new Uri("https://github.com/highbyte/dotnet-6502/blob/master/doc/SYSTEMS_C64_COMPATIBLE_PRG.md"));
        }
    }

    // Core C64 functionality methods
    public async Task CopyBasicSourceCode()
    {
        if (HostApp?.EmulatorState != Systems.EmulatorState.Running ||
            !IsC64System())
            return;

        try
        {
            var c64 = (C64)HostApp.CurrentRunningSystem!;
            var sourceCode = c64.BasicTokenParser.GetBasicText();

            if (TopLevel.GetTopLevel(this) is { } topLevel)
            {
                await topLevel.Clipboard?.SetTextAsync(sourceCode.ToLower())!;
            }
        }
        catch (Exception ex)
        {
            // Handle error - could show a dialog or log it
            System.Console.WriteLine($"Error copying Basic source: {ex.Message}");
        }
    }

    public async Task PasteText()
    {
        if (HostApp?.EmulatorState != Systems.EmulatorState.Running ||
       !IsC64System())
            return;

        try
        {
            if (TopLevel.GetTopLevel(this) is { } topLevel)
            {
                var text = await topLevel.Clipboard?.GetTextAsync()!;
                if (!string.IsNullOrEmpty(text))
                {
                    var c64 = (C64)HostApp.CurrentRunningSystem!;
                    c64.TextPaste.Paste(text);
                }
            }
        }
        catch (Exception ex)
        {
            // Handle error
            System.Console.WriteLine($"Error pasting text: {ex.Message}");
        }
    }

    public async Task ToggleDiskImage()
    {
        if (HostApp?.EmulatorState == Systems.EmulatorState.Uninitialized || !IsC64System())
            return;

        try
        {
            var c64 = (C64)HostApp.CurrentRunningSystem!;
            var diskDrive = c64.IECBus?.Devices?.OfType<Systems.Commodore64.TimerAndPeripheral.DiskDrive.DiskDrive1541>().FirstOrDefault();

            if (diskDrive?.IsDisketteInserted == true)
            {
                // Detach current disk image
                diskDrive.RemoveD64DiskImage();
            }
            else
            {
                // Attach new disk image - open file dialog
                await AttachDiskImage();
            }

            // Notify the ViewModel that the disk image state has changed
            if (ViewModel is C64MenuViewModel viewModel)
            {
                viewModel.NotifyDiskImageStateChanged();
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error toggling disk image: {ex.Message}");
        }
    }

    private async Task AttachDiskImage()
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel)
            return;

        var storageProvider = topLevel.StorageProvider;
        if (storageProvider.CanOpen)
        {
            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select D64 Disk Image",
                AllowMultiple = false,
                FileTypeFilter = new[]
               {
     new FilePickerFileType("D64 Disk Images") { Patterns = new[] { "*.d64" } },
              new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
        }
            });

            if (files.Count > 0)
            {
                try
                {
                    await using var stream = await files[0].OpenReadAsync();
                    var fileBuffer = new byte[stream.Length];
                    await stream.ReadExactlyAsync(fileBuffer);

                    // Parse the D64 disk image
                    var d64DiskImage = Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64.D64Parser.ParseD64File(fileBuffer);

                    // Set the disk image on the running C64's DiskDrive1541
                    var c64 = (C64)HostApp!.CurrentRunningSystem!;
                    var diskDrive = c64.IECBus?.Devices?.OfType<Systems.Commodore64.TimerAndPeripheral.DiskDrive.DiskDrive1541>().FirstOrDefault();
                    diskDrive?.SetD64DiskImage(d64DiskImage);
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error loading disk image: {ex.Message}");
                }
            }
        }
    }

    private bool IsC64System()
    {
        return string.Equals(HostApp?.SelectedSystemName, C64.SystemName, StringComparison.OrdinalIgnoreCase);
    }

    private void ToggleDiskSection_Click(object? sender, RoutedEventArgs e)
    {
        ToggleSection("DiskSectionHeader", "DiskSectionContent",
                  new[] { ("LoadSaveSectionHeader", "LoadSaveSectionContent"),
                         ("ConfigSectionHeader", "ConfigSectionContent") });
    }

    private void ToggleLoadSaveSection_Click(object? sender, RoutedEventArgs e)
    {
        ToggleSection("LoadSaveSectionHeader", "LoadSaveSectionContent",
                new[] { ("DiskSectionHeader", "DiskSectionContent"),
                    ("ConfigSectionHeader", "ConfigSectionContent") });
    }

    private void ToggleConfigSection_Click(object? sender, RoutedEventArgs e)
    {
        ToggleSection("ConfigSectionHeader", "ConfigSectionContent",
                  new[] { ("DiskSectionHeader", "DiskSectionContent"),
                      ("LoadSaveSectionHeader", "LoadSaveSectionContent") });
    }

    /// <summary>
    /// Toggles a collapsible section and collapses other specified sections if this one is being expanded.
    /// The arrow character (▼/▶) at the start of the button content is automatically toggled.
    /// </summary>
    /// <param name="headerButtonName">Name of the header button control</param>
    /// <param name="contentBorderName">Name of the content border control</param>
    /// <param name="otherSectionsToCollapse">Array of tuples containing (headerName, contentName) for sections to collapse</param>
    private void ToggleSection(
        string headerButtonName,
        string contentBorderName,
        (string headerName, string contentName)[] otherSectionsToCollapse)
    {
        var headerButton = this.FindControl<Button>(headerButtonName);
        var contentBorder = this.FindControl<Border>(contentBorderName);

        if (headerButton != null && contentBorder != null)
        {
            bool newExpandedState = !contentBorder.IsVisible;
            SetSectionState(headerButton, contentBorder, newExpandedState);

            // Collapse other sections if this section is being expanded
            if (newExpandedState)
            {
                foreach (var (headerName, contentName) in otherSectionsToCollapse)
                {
                    var otherHeaderButton = this.FindControl<Button>(headerName);
                    var otherContentBorder = this.FindControl<Border>(contentName);

                    if (otherHeaderButton != null && otherContentBorder != null)
                    {
                        SetSectionState(otherHeaderButton, otherContentBorder, expanded: false);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Sets the state of a collapsible section (expanded or collapsed).
    /// Updates both the visibility of the content and the arrow character in the header button.
    /// </summary>
    /// <param name="headerButton">The header button control</param>
    /// <param name="contentBorder">The content border control</param>
    /// <param name="expanded">True to expand the section, false to collapse it</param>
    private void SetSectionState(Button headerButton, Border contentBorder, bool expanded)
    {
        contentBorder.IsVisible = expanded;

        if (headerButton.Content is string content)
        {
            if (expanded)
            {
                // Expand: change ▶ to ▼
                headerButton.Content = content.Replace("▶", "▼");
            }
            else
            {
                // Collapse: change ▼ to ▶
                headerButton.Content = content.Replace("▼", "▶");
            }
        }
    }
    // File operation event handlers
    private async void LoadPreloadedDisk_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not C64MenuViewModel viewModel)
            return;

        string selectedPreloadedDisk = viewModel.SelectedPreloadedDisk;
        if (string.IsNullOrEmpty(selectedPreloadedDisk) || !_preloadedD64Images.ContainsKey(selectedPreloadedDisk))
            return;

        var diskInfo = _preloadedD64Images[selectedPreloadedDisk];
        _isLoadingPreloadedDisk = true;
        _latestPreloadedDiskError = "";

        System.Console.WriteLine($"Starting to load preloaded disk: {diskInfo.DisplayName}");

        try
        {
            // Initialize D64AutoDownloadAndRun if not already done
            if (_d64AutoDownloadAndRun == null)
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
             {
                 builder.SetMinimumLevel(LogLevel.Information);
             });

                var c64HostConfig = HostApp!.CurrentHostSystemConfig as C64HostConfig;
                _d64AutoDownloadAndRun = new D64AutoDownloadAndRun(
                   loggerFactory,
                   httpClient,
                   HostApp!,
                    corsProxyUrl: PlatformDetection.IsRunningInWebAssembly() ? c64HostConfig.CorsProxyURL : null);
            }

            await _d64AutoDownloadAndRun.DownloadAndRunDiskImage(
                diskInfo,
                stateHasChangedCallback: async () =>
                {
                    viewModel.NotifyEmulatorStateChanged();
                    await Task.CompletedTask;
                },
                setConfigCallback: async (diskInfo) =>
                {
                    if (HostApp?.CurrentHostSystemConfig is not C64HostConfig c64HostConfig)
                        return;

                    var c64SystemConfig = c64HostConfig.SystemConfig;

                    // Apply keyboard joystick settings to config object while emulator is stopped
                    c64SystemConfig.KeyboardJoystickEnabled = diskInfo.KeyboardJoystickEnabled;
                    c64SystemConfig.KeyboardJoystick = diskInfo.KeyboardJoystickNumber;

                    // Apply renderer setting to config object while emulator is stopped
                    // TODO: If/when a optimized RenderType for use without bitmap graphics is available, set rendererProviderType appropriately here.
                    //Type rendererProviderType = diskInfo.RequiresBitmap ? typeof(Vic2Rasterizer) : typeof(C64VideoCommandStream);
                    Type rendererProviderType = typeof(Vic2Rasterizer);
                    c64HostConfig.SystemConfig.SetRenderProviderType(rendererProviderType);

                    // Apply audio enabled setting to config object while emulator is stopped
                    c64SystemConfig.AudioEnabled = diskInfo.AudioEnabled;

                    // Apply C64 variant setting to config object while emulator is stopped
                    await HostApp.SelectSystemConfigurationVariant(diskInfo.C64Variant);

                    HostApp.UpdateHostSystemConfig(c64HostConfig);
                });
        }
        catch (Exception ex)
        {
            _latestPreloadedDiskError = $"Error downloading or running disk image: {ex.Message}";
            System.Console.WriteLine($"LoadPreloadedDisk_Click error: {_latestPreloadedDiskError}");
        }
        finally
        {
            _isLoadingPreloadedDisk = false;
            System.Console.WriteLine($"Finished loading preloaded disk. Loading state: {_isLoadingPreloadedDisk}");
            if (!string.IsNullOrEmpty(_latestPreloadedDiskError))
                System.Console.WriteLine($"Final error state: {_latestPreloadedDiskError}");
            viewModel.NotifyEmulatorStateChanged();
        }
    }

    private async void LoadBasicFile_Click(object? sender, RoutedEventArgs e)
    {
        await LoadBasicFile();
    }

    private async void SaveBasicFile_Click(object? sender, RoutedEventArgs e)
    {
        await SaveBasicFile();
    }

    private async void LoadBinaryFile_Click(object? sender, RoutedEventArgs e)
    {
        await LoadBinaryFile();
    }

    private async Task LoadBasicFile()
    {
        if (HostApp?.EmulatorState == Systems.EmulatorState.Uninitialized || !IsC64System())
            return;

        if (TopLevel.GetTopLevel(this) is not { } topLevel)
            return;

        var storageProvider = topLevel.StorageProvider;
        if (!storageProvider.CanOpen)
            return;

        bool wasRunning = HostApp.EmulatorState == Systems.EmulatorState.Running;
        if (wasRunning)
            HostApp.Pause();

        try
        {
            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Load Basic PRG File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("PRG Files") { Patterns = new[] { "*.prg" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            });

            if (files.Count > 0)
            {
                try
                {
                    await using var stream = await files[0].OpenReadAsync();
                    var fileBuffer = new byte[stream.Length];
                    await stream.ReadExactlyAsync(fileBuffer);

                    BinaryLoader.Load(
                        HostApp.CurrentRunningSystem!.Mem,
                        fileBuffer,
                        out ushort loadedAtAddress,
                        out ushort fileLength);

                    if (loadedAtAddress != C64.BASIC_LOAD_ADDRESS)
                    {
                        System.Console.WriteLine($"Warning: Loaded program is not a Basic program, it's expected to load at {C64.BASIC_LOAD_ADDRESS.ToHex()} but was loaded at {loadedAtAddress.ToHex()}");
                    }
                    else
                    {
                        var c64 = (C64)HostApp.CurrentRunningSystem!;
                        c64.InitBasicMemoryVariables(loadedAtAddress, fileLength);
                    }

                    System.Console.WriteLine($"Basic program loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error loading Basic .prg: {ex.Message}");
                }
            }
        }
        finally
        {
            if (wasRunning)
                await HostApp.Start();
        }
    }

    private async Task SaveBasicFile()
    {
        if (HostApp?.EmulatorState == Systems.EmulatorState.Uninitialized || !IsC64System())
            return;

        if (TopLevel.GetTopLevel(this) is not { } topLevel)
            return;

        var storageProvider = topLevel.StorageProvider;
        if (!storageProvider.CanSave)
            return;

        bool wasRunning = HostApp.EmulatorState == Systems.EmulatorState.Running;
        if (wasRunning)
            HostApp.Pause();

        try
        {
            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Basic PRG File",
                SuggestedFileName = "program.prg",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PRG Files") { Patterns = new[] { "*.prg" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            });

            if (file != null)
            {
                try
                {
                    ushort startAddress = C64.BASIC_LOAD_ADDRESS;
                    var c64 = (C64)HostApp.CurrentRunningSystem!;
                    var endAddress = c64.GetBasicProgramEndAddress();

                    var saveData = BinarySaver.BuildSaveData(
                        HostApp.CurrentRunningSystem.Mem,
                        startAddress,
                        endAddress,
                        addFileHeaderWithLoadAddress: true);

                    await using var stream = await file.OpenWriteAsync();
                    await stream.WriteAsync(saveData);

                    System.Console.WriteLine($"Basic program saved from {startAddress.ToHex()} to {endAddress.ToHex()}");
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error saving Basic .prg: {ex.Message}");
                }
            }
        }
        finally
        {
            if (wasRunning)
                await HostApp.Start();
        }
    }

    private async Task LoadBinaryFile()
    {
        if (HostApp?.EmulatorState == Systems.EmulatorState.Uninitialized)
            return;

        if (TopLevel.GetTopLevel(this) is not { } topLevel)
            return;

        var storageProvider = topLevel.StorageProvider;
        if (!storageProvider.CanOpen)
            return;

        bool wasRunning = HostApp.EmulatorState == Systems.EmulatorState.Running;
        if (wasRunning)
            HostApp.Pause();

        try
        {
            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Load & Start Binary PRG File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("PRG Files") { Patterns = new[] { "*.prg" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            });

            if (files.Count > 0)
            {
                try
                {
                    await using var stream = await files[0].OpenReadAsync();
                    var fileBuffer = new byte[stream.Length];
                    await stream.ReadExactlyAsync(fileBuffer);

                    BinaryLoader.Load(
                        HostApp.CurrentRunningSystem!.Mem,
                        fileBuffer,
                        out ushort loadedAtAddress,
                        out ushort fileLength);

                    HostApp.CurrentRunningSystem.CPU.PC = loadedAtAddress;

                    System.Console.WriteLine($"Binary program loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");
                    System.Console.WriteLine($"Program Counter set to {loadedAtAddress.ToHex()}");

                    await HostApp.Start();
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error loading binary .prg: {ex.Message}");
                }
            }
        }
        finally
        {
            if (wasRunning && HostApp.EmulatorState != Systems.EmulatorState.Running)
                await HostApp.Start();
        }
    }

    private async void LoadAssemblyExample_Click(object? sender, RoutedEventArgs e)
    {
        await LoadAssemblyExample();
    }

    private async Task LoadAssemblyExample()
    {
        if (HostApp?.EmulatorState == Systems.EmulatorState.Uninitialized || !IsC64System())
            return;

        if (ViewModel is not C64MenuViewModel viewModel)
            return;

        string url = viewModel.SelectedAssemblyExample;
        if (string.IsNullOrEmpty(url))
            return;

        bool wasRunning = HostApp.EmulatorState == Systems.EmulatorState.Running;
        if (wasRunning)
            HostApp.Pause();

        try
        {
            // Download the .prg file
            var httpClient = new HttpClient();
            var prgBytes = await httpClient.GetByteArrayAsync(url);

            // Load file into memory
            BinaryLoader.Load(
    HostApp.CurrentRunningSystem!.Mem,
     prgBytes,
       out ushort loadedAtAddress,
         out ushort fileLength);

            // Set Program Counter to start of loaded file
            HostApp.CurrentRunningSystem.CPU.PC = loadedAtAddress;

            System.Console.WriteLine($"Assembly example loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");
            System.Console.WriteLine($"Program Counter set to {loadedAtAddress.ToHex()}");

            // Start the emulator
            await HostApp.Start();
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error loading assembly example: {ex.Message}");
            if (wasRunning)
                await HostApp.Start();
        }
    }

    private async void LoadBasicExample_Click(object? sender, RoutedEventArgs e)
    {
        await LoadBasicExample();
    }

    private async Task LoadBasicExample()
    {
        if (HostApp?.EmulatorState == Systems.EmulatorState.Uninitialized || !IsC64System())
            return;

        if (ViewModel is not C64MenuViewModel viewModel)
            return;

        string url = viewModel.SelectedBasicExample;
        if (string.IsNullOrEmpty(url))
            return;

        bool wasRunning = HostApp.EmulatorState == Systems.EmulatorState.Running;
        if (wasRunning)
            HostApp.Pause();

        try
        {
            // Download the .prg file
            var httpClient = new HttpClient();
            var prgBytes = await httpClient.GetByteArrayAsync(url);

            // Load file into memory
            BinaryLoader.Load(
                HostApp.CurrentRunningSystem!.Mem,
              prgBytes,
              out ushort loadedAtAddress,
                  out ushort fileLength);

            var c64 = (C64)HostApp.CurrentRunningSystem!;
            if (loadedAtAddress != C64.BASIC_LOAD_ADDRESS)
            {
                // Probably not a Basic program that was loaded. Don't init BASIC memory variables.
                System.Console.WriteLine($"Warning: Loaded program is not a Basic program, it's expected to load at {C64.BASIC_LOAD_ADDRESS.ToHex()} but was loaded at {loadedAtAddress.ToHex()}");
            }
            else
            {
                // Init C64 BASIC memory variables
                c64.InitBasicMemoryVariables(loadedAtAddress, fileLength);
            }

            // Send "list" + NewLine (Return) to the keyboard buffer to immediately list the loaded program
            c64.TextPaste.Paste("list\n");

            System.Console.WriteLine($"Basic example loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");

            // Start the emulator
            await HostApp.Start();
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error loading basic example: {ex.Message}");
            if (wasRunning)
                await HostApp.Start();
        }
    }

    // Private fields for preloaded disk functionality
    private readonly Dictionary<string, D64DownloadDiskInfo> _preloadedD64Images = new()
    {
        {"bubblebobble", new D64DownloadDiskInfo("Bubble Bobble", "https://csdb.dk/release/download.php?id=191127", downloadType: DownloadType.ZIP, keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, requiresBitmap: true, audioEnabled: false, directLoadPRGName: "*")}, // Note: Bubble Bobble is not a bitmap game, but somehow this version fails to initialize the custom charset in text mode correctly in SkiaSharp renderer.
        {"digiloi", new D64DownloadDiskInfo("Digiloi", "https://csdb.dk/release/download.php?id=213381", keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, audioEnabled: true, directLoadPRGName: "*")},
        {"elite", new D64DownloadDiskInfo("Elite", "https://csdb.dk/release/download.php?id=70413", downloadType: DownloadType.ZIP, keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, requiresBitmap: true, audioEnabled: false,directLoadPRGName: "*", c64Variant: "C64PAL")},
        {"lastninja", new D64DownloadDiskInfo("Last Ninja", "https://csdb.dk/release/download.php?id=101848", downloadType: DownloadType.ZIP, keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, requiresBitmap: true, audioEnabled: false, directLoadPRGName: "*")},
        {"minizork", new D64DownloadDiskInfo("Mini Zork", "https://csdb.dk/release/download.php?id=42919", audioEnabled: false, directLoadPRGName: "*")},
        {"montezuma", new D64DownloadDiskInfo("Montezuma's Revenge", "https://csdb.dk/release/download.php?id=128101", downloadType: DownloadType.ZIP, keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, audioEnabled: true, directLoadPRGName: "*")},
        {"rallyspeedway", new D64DownloadDiskInfo("Rally Speedway", "https://csdb.dk/release/download.php?id=219614", keyboardJoystickEnabled: true, keyboardJoystickNumber: 1, audioEnabled: true, directLoadPRGName: "*")}
    };

    // Fields for preloaded disk loading functionality
    private string _latestPreloadedDiskError = "";
    private bool _isLoadingPreloadedDisk = false;
    private D64AutoDownloadAndRun? _d64AutoDownloadAndRun;
}
