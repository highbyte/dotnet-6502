using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using AvaloniaAnimation = Avalonia.Animation;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class MainView : UserControl
{
    // Lazy-initialized logger
    private ILogger? _logger;
    private ILogger Logger => _logger ??= AppLogger.CreateLogger(nameof(MainView));

    private bool _isInitialized;

    private MainViewModel? _subscribedViewModel;
    private MonitorDialog? _monitorWindow;
    private Panel? _monitorOverlay;

    // For log auto-scroll
    private ScrollViewer? _logScrollViewer;
    private bool _logAutoScrollEnabled = true;

    // KeyBindings added to the MainWindow on behalf of the current system menu contributor.
    // Tracked so we can remove them when the active contributor swaps or the view detaches.
    private readonly System.Collections.Generic.List<KeyBinding> _contributorKeyBindings = new();

    // Permanent tab-navigation KeyBindings (Windows/Linux). Added once on attach; never swapped.
    private readonly System.Collections.Generic.List<KeyBinding> _generalKeyBindings = new();

    // Tab navigation shortcut definitions — order determines NativeMenu display order.
    private static readonly (Key Key, string TabItemName, string Label)[] TabNavigationShortcuts =
    {
        (Key.I, "InformationTabItem",  "Information"),
        (Key.C, "ConfigStatusTabItem", "Config status"),
        (Key.L, "LogTabItem",          "Log"),
        (Key.S, "ScriptsTabItem",      "Scripts"),
        (Key.G, "GeneralInfoTabItem",  "General info"),
        (Key.D, "DebugTabItem",        "Debug"),
    };

    // Parameterless constructor - child views created by XAML!
    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        this.AttachedToVisualTree += MainView_AttachedToVisualTree;
        // DataContext will be set from App.axaml.cs via DI
        // Child views (C64MenuView, StatisticsView, EmulatorView) are created by XAML
        // and get their DataContext through XAML bindings

        this.Loaded += OnViewLoaded;

        KeyDown += OnKeyDown;
    }

    private void OnViewLoaded(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
        {
            if (_isInitialized)
                return;
            _isInitialized = true;

            // Start fade-in animation
            await FadeIn();

            if (DataContext is MainViewModel viewModel)
            {
                await viewModel.InitializeAsync();
                // Explicit apply after initialization completes: the reactive chain fires
                // during InitializeAsync but NativeMenu.SetMenu may require the call to
                // originate on the UI thread AFTER the window is fully shown.
                ApplyMenuContributor(_subscribedViewModel?.ActiveMenuContributor);
            }

            // TODO: When not having the emulator started, need to set focus to the EmulatorView to capture keyboard input.
            //       When the emulator is running, Focusable should be set back to false.
            //Focus();
            //Focusable = true;
        });

    private async Task FadeIn()
    {
        var animation = new AvaloniaAnimation.Animation
        {
            Duration = TimeSpan.FromMilliseconds(500),
            FillMode = AvaloniaAnimation.FillMode.Forward,
            Children =
            {
                new AvaloniaAnimation.KeyFrame
                {
                    Cue = new AvaloniaAnimation.Cue(0.0),
                    Setters = { new Setter(OpacityProperty, 0.0) }
                },
                new AvaloniaAnimation.KeyFrame
                {
                    Cue = new AvaloniaAnimation.Cue(1.0),
                    Setters = { new Setter(OpacityProperty, 1.0) }
                }
            }
        };

        await animation.RunAsync(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from previous ViewModel's property changes
        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedViewModel.EmulatorOptionsRequested -= OnEmulatorOptionsRequested;
        }

        // Subscribe to new ViewModel's property changes
        _subscribedViewModel = DataContext as MainViewModel;
        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            _subscribedViewModel.EmulatorOptionsRequested += OnEmulatorOptionsRequested;
            _subscribedViewModel.RequestAddScript += OnRequestAddScript;
            _subscribedViewModel.RequestEditScript += OnRequestEditScript;
            _subscribedViewModel.RequestDeleteScript += OnRequestDeleteScript;
            _subscribedViewModel.RequestOpenScriptFolder += OnRequestOpenScriptFolder;
            // Check immediately in case validation errors are already set
            CheckAndSelectValidationErrorsTab();
            // Listen for log changes
            _subscribedViewModel.LogMessages.CollectionChanged += LogMessages_CollectionChanged;
            // Set up tab selection tracking
            SetupTabSelectionTracking();
            // Apply menu + shortcuts for the initial contributor (may already be resolved)
            ApplyMenuContributor(_subscribedViewModel.ActiveMenuContributor);
        }
    }

    // If scale can change at runtime, listen for property changes
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Listen for changes to ValidationErrors property
        if (e.PropertyName == nameof(MainViewModel.ValidationErrors))
        {
            CheckAndSelectValidationErrorsTab();
        }

        if (e.PropertyName == nameof(MainViewModel.ActiveMenuContributor) && _subscribedViewModel != null)
        {
            ApplyMenuContributor(_subscribedViewModel.ActiveMenuContributor);
        }

        if (e.PropertyName == nameof(MainViewModel.IsMonitorVisible) && DataContext is MainViewModel viewModel)
        {
            if (viewModel.IsMonitorVisible)
            {
                // Dispatch to UI thread to ensure all ReactiveUI property updates are complete
                // before showing the monitor UI. This is needed when EnableMonitor is called
                // from non-UI contexts like OnAfterRunEmulatorOneFrame (breakpoint triggers).
                Dispatcher.UIThread.Post(() => ShowMonitorUI(), DispatcherPriority.Loaded);
            }
            else
            {
                // Ensure UI operations run on UI thread
                Dispatcher.UIThread.Post(() => CloseMonitorUI(), DispatcherPriority.Loaded);
            }
        }
    }

    private void CheckAndSelectValidationErrorsTab()
    {
        if (_subscribedViewModel == null)
            return;

        // Check the actual ValidationErrors collection instead of IsSystemConfigValid
        // to avoid timing issues with reactive property updates
        if (_subscribedViewModel.ValidationErrors == null ||
            _subscribedViewModel.ValidationErrors.Count == 0)
            return;

        // Use Dispatcher to ensure the control is properly initialized
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<TabItem>("ConfigStatusTabItem") is TabItem configErrorsTab)
            {
                if (this.FindControl<TabControl>("InformationTabControl") is TabControl tabControl)
                {
                    tabControl.SelectedItem = configErrorsTab;
                }
            }
        });
    }

    private void MainView_Loaded(object? sender, RoutedEventArgs e)
    {
        // Initialization complete
    }

    // ComboBox SelectionChanged handlers - invoke commands
    private void OnSystemSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && e.AddedItems.Count > 0)
        {
            var selectedSystem = e.AddedItems[0]?.ToString();
            if (!string.IsNullOrEmpty(selectedSystem))
            {
                // Fire and forget - let the ReactiveCommand handle scheduling and execution. This works in WebAssembly because we're not subscribing to the observable
                _ = viewModel.SelectSystemCommand.Execute(selectedSystem);
            }
        }
    }

    private void OnSystemVariantSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && e.AddedItems.Count > 0)
        {
            var selectedVariant = e.AddedItems[0]?.ToString();
            if (!string.IsNullOrEmpty(selectedVariant))
            {
                // Fire and forget - let the ReactiveCommand handle scheduling and execution
                // This works in WebAssembly because we're not subscribing to the observable
                _ = viewModel.SelectSystemVariantCommand.Execute(selectedVariant);
            }
        }
    }

    private void ShowMonitorUI()
    {
        // Check if running on WASM/Browser platform
        if (PlatformDetection.IsRunningInWebAssembly())
        {
            // For WASM, show usercontrol overlay instead of Window
            ShowMonitorOverlay();
        }
        else
        {
            // For desktop platforms, use the Window dialog
            ShowMonitorWindow();
            //ShowMonitorOverlay();
        }
    }

    private void CloseMonitorUI()
    {
        CloseMonitorWindow();
        CloseMonitorOverlay();
    }

    private void ShowMonitorWindow()
    {
        if (_monitorWindow != null)
        {
            _monitorWindow.Activate();
            return;
        }

        if (DataContext is not MainViewModel viewModel)
            return;

        if (viewModel.MonitorViewModel == null)
            return;

        _monitorWindow = new MonitorDialog
        {
            DataContext = viewModel.MonitorViewModel
        };
        _monitorWindow.Closed += (sender, e) =>
          {
              _monitorWindow = null;
          };

        if (TopLevel.GetTopLevel(this) is Window owner)
            _ = _monitorWindow.ShowDialog(owner);
        else
            _monitorWindow.Show();
    }

    private void CloseMonitorWindow()
    {
        if (_monitorWindow == null)
            return;

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ClearMonitorViewModel();
        }

        var window = _monitorWindow;
        _monitorWindow = null;

        if (window.IsVisible)
            window.Close();
    }

    private void ShowMonitorOverlay()
    {
        if (_monitorOverlay != null)
            return;

        if (DataContext is not MainViewModel viewModel)
            return;

        if (viewModel.MonitorViewModel == null)
            return;

        var monitorControl = new MonitorUserControl(viewModel.MonitorViewModel)
        {
            MaxHeight = 600  // Limit height in Browser mode to prevent unbounded expansion
        };

        _monitorOverlay = new Panel
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            ZIndex = 1000
        };

        var dialogContainer = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(26, 32, 44)),  // 1A202C, ViewDefaultBg
            BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(20),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = monitorControl
        };

        _monitorOverlay.Children.Add(dialogContainer);

        if (Content is Grid mainGrid)
        {
            Grid.SetRowSpan(_monitorOverlay, mainGrid.RowDefinitions.Count > 0 ? mainGrid.RowDefinitions.Count : 1);
            Grid.SetColumnSpan(_monitorOverlay, mainGrid.ColumnDefinitions.Count > 0 ? mainGrid.ColumnDefinitions.Count : 1);
            mainGrid.Children.Add(_monitorOverlay);
        }
    }

    private void CloseMonitorOverlay()
    {
        Logger.LogDebug("CloseMonitorOverlay called");
        if (_monitorOverlay == null)
            return;

        Logger.LogDebug("Removing monitor overlay from visual tree");
        if (Content is Grid mainGrid && mainGrid.Children.Contains(_monitorOverlay))
            mainGrid.Children.Remove(_monitorOverlay);

        _monitorOverlay = null;

        if (DataContext is MainViewModel viewModel)
        {
            Logger.LogDebug("Clearing MonitorViewModel");
            viewModel.ClearMonitorViewModel();
        }

        // Restore focus to EmulatorView
        Logger.LogDebug("Setting focus to EmulatorView");
        var emulatorView = this.FindControl<EmulatorView>("EmulatorView");
        emulatorView?.Focus();
        Logger.LogDebug("Focus set to EmulatorView");
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Tear down any menu + keybindings we contributed while attached.
        ApplyMenuContributor(null);

        // Remove permanent tab-navigation key bindings.
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            foreach (var kb in _generalKeyBindings)
                window.KeyBindings.Remove(kb);
        }
        _generalKeyBindings.Clear();

        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedViewModel.EmulatorOptionsRequested -= OnEmulatorOptionsRequested;
            _subscribedViewModel.RequestAddScript -= OnRequestAddScript;
            _subscribedViewModel.RequestEditScript -= OnRequestEditScript;
            _subscribedViewModel.RequestDeleteScript -= OnRequestDeleteScript;
            _subscribedViewModel.RequestOpenScriptFolder -= OnRequestOpenScriptFolder;
            _subscribedViewModel.LogMessages.CollectionChanged -= LogMessages_CollectionChanged;
            _subscribedViewModel = null;
        }
    }

    /// <summary>
    /// Replaces any previously-applied menu/keybindings with those from <paramref name="contributor"/>.
    /// Pass null to clear (e.g. on detach or when no system is selected).
    ///
    /// Platform split:
    /// - macOS:         <see cref="NativeMenu"/> is installed on the Application. On macOS this
    ///                  appears in the OS-level system menu bar (outside the app window), which is
    ///                  the desired UX. The items are also exposed via the macOS Accessibility API
    ///                  (AXMenuItem), making shortcuts self-describing for AI agents.
    /// - Windows/Linux: <see cref="NativeMenu"/> would render as in-window chrome on these platforms,
    ///                  which is not desired. <see cref="KeyBinding"/>s are added to the MainWindow
    ///                  instead — shortcuts fire regardless of focus but are not visible in any menu.
    /// - WASM:          No-op; neither applies in the browser target.
    /// </summary>
    private void ApplyMenuContributor(ISystemMenuContributor? contributor)
    {
        if (PlatformDetection.IsRunningInWebAssembly())
            return;

        // NativeMenu.SetMenu must be called on the UI thread on macOS.
        // SelectedSystemName PropertyChanged can fire on a background thread after an async await.
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ApplyMenuContributor(contributor));
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        var window = topLevel as Window;

        // Remove any previously-added key bindings from the window.
        if (window != null)
        {
            foreach (var kb in _contributorKeyBindings)
                window.KeyBindings.Remove(kb);
        }
        _contributorKeyBindings.Clear();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // On macOS: mutate the items of the pre-registered NativeMenu object (set in
            // App.Initialize) rather than replacing the menu. Replacing the object stops
            // Avalonia's backend from tracking it, so shortcuts and the menu bar stop working.
            var appMenu = Application.Current is { } a ? NativeMenu.GetMenu(a) : null;
            if (appMenu == null)
            {
                // Fallback: pre-registration didn't happen (e.g. non-macOS path ran first).
                appMenu = new NativeMenu();
                if (Application.Current is { } currentApp2)
                    NativeMenu.SetMenu(currentApp2, appMenu);
            }

            appMenu.Items.Clear();

            // Always add View menu for tab navigation (order-independent, works regardless of system).
            var viewRoot = new NativeMenuItem { Header = "View" };
            var viewMenu = new NativeMenu();
            const KeyModifiers macViewMod = KeyModifiers.Meta | KeyModifiers.Alt;
            foreach (var (key, tabItemName, label) in TabNavigationShortcuts)
            {
                var name = tabItemName;
                viewMenu.Items.Add(new NativeMenuItem
                {
                    Header = label,
                    Gesture = new KeyGesture(key, macViewMod),
                    Command = ReactiveCommand.Create(() => SelectTab(name))
                });
            }
            viewRoot.Menu = viewMenu;
            appMenu.Items.Add(viewRoot);

            if (contributor != null)
            {
                var systemRoot = new NativeMenuItem { Header = contributor.MenuLabel };
                var submenu = new NativeMenu();
                foreach (var item in contributor.GetNativeMenuItems())
                    submenu.Items.Add(item);
                systemRoot.Menu = submenu;
                appMenu.Items.Add(systemRoot);

                Logger.LogInformation("[NativeMenu] Updated existing NativeMenu for contributor '{Label}': {Count} top-level items",
                    contributor.MenuLabel, appMenu.Items.Count);
            }
            else
            {
                Logger.LogInformation("[NativeMenu] Cleared NativeMenu (no contributor)");
            }
        }
        else
        {
            // On Windows/Linux: NativeMenu would render as in-window chrome, which is not the
            // desired UX (unlike macOS where it goes to the OS system menu bar). Use window-level
            // KeyBindings instead — shortcuts fire regardless of which child has focus, but they
            // are invisible to accessibility tools and require prior knowledge to discover.
            if (window != null && contributor != null)
            {
                foreach (var kb in contributor.GetKeyBindings())
                {
                    window.KeyBindings.Add(kb);
                    _contributorKeyBindings.Add(kb);
                }
            }
        }
    }
    private void MainView_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // Directly find the LogScrollViewer by name
        _logScrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");
        _logScrollViewer?.ScrollChanged += LogScrollViewer_ScrollChanged;

        // Now that the MainView has a TopLevel (Window), apply the contributor's native menu /
        // keybindings — the earlier call during DataContextChanged could not find the window.
        if (_subscribedViewModel != null)
            ApplyMenuContributor(_subscribedViewModel.ActiveMenuContributor);

        // Register always-on tab navigation shortcuts (Windows/Linux only; macOS uses NativeMenu).
        ApplyGeneralKeyBindings();
    }

    // Selects a bottom tab by its Name attribute, regardless of tab order.
    private void SelectTab(string tabItemName)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<TabControl>("InformationTabControl") is not { } tabControl) return;
            if (this.FindControl<TabItem>(tabItemName) is not { } tabItem) return;
            tabControl.SelectedItem = tabItem;
        });
    }

    // Registers permanent Ctrl+Alt+<key> KeyBindings on the Window for tab navigation.
    // Windows/Linux only — on macOS the same shortcuts live in the "View" NativeMenu section.
    private void ApplyGeneralKeyBindings()
    {
        if (PlatformDetection.IsRunningInWebAssembly()) return;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        if (TopLevel.GetTopLevel(this) is not Window window) return;

        foreach (var kb in _generalKeyBindings)
            window.KeyBindings.Remove(kb);
        _generalKeyBindings.Clear();

        const KeyModifiers mod = KeyModifiers.Control | KeyModifiers.Alt;
        foreach (var (key, tabItemName, _) in TabNavigationShortcuts)
        {
            var name = tabItemName;
            var kb = new KeyBinding
            {
                Gesture = new KeyGesture(key, mod),
                Command = ReactiveCommand.Create(() => SelectTab(name))
            };
            window.KeyBindings.Add(kb);
            _generalKeyBindings.Add(kb);
        }
    }

    private void LogScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_logScrollViewer == null)
            return;
        // If user is at the bottom, enable auto-scroll
        double tolerance = 2.0;
        bool isAtBottom = _logScrollViewer.Offset.Y >= _logScrollViewer.Extent.Height - _logScrollViewer.Viewport.Height - tolerance;
        _logAutoScrollEnabled = isAtBottom;
    }

    private void LogEntryContextMenu_CopyMessage_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
        {
            var dataContext = (sender as MenuItem)?.DataContext ?? ((sender as MenuItem)?.Parent as ContextMenu)?.DataContext;
            if (dataContext is LogDisplayEntry entry
                && TopLevel.GetTopLevel(this) is { } topLevel
                && topLevel.Clipboard is { } clipboard)
            {
                using var data = new DataTransfer();
                data.Add(DataTransferItem.CreateText(entry.Message));
                await clipboard.SetDataAsync(data);
            }
        });

    private void LogMessages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_logScrollViewer == null)
            return;
        if (_logAutoScrollEnabled)
        {
            // Scroll to bottom after layout/render
            Dispatcher.UIThread.Post(async () =>
            {
                await Task.Delay(10); // Small delay to ensure layout
                double maxY = Math.Max(0, _logScrollViewer.Extent.Height - _logScrollViewer.Viewport.Height);
                _logScrollViewer.Offset = new Vector(_logScrollViewer.Offset.X, maxY);
            }, DispatcherPriority.Loaded);
        }
    }

    private void SetupTabSelectionTracking()
    {
        // Find the TabControl and set up selection change tracking
        var tabControl = this.FindControl<TabControl>("InformationTabControl");
        if (tabControl != null)
        {
            // Subscribe to tab selection changes
            tabControl.SelectionChanged += OnTabSelectionChanged;
            // Initialize current tab name
            if (_subscribedViewModel != null && tabControl.SelectedItem is TabItem selectedTab)
            {
                _subscribedViewModel.SelectedTabName = selectedTab.Name ?? "";
            }
        }
    }

    private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_subscribedViewModel != null && sender is TabControl tabControl && tabControl.SelectedItem is TabItem selectedTab)
        {
            _subscribedViewModel.SelectedTabName = selectedTab.Name ?? "";
        }
    }

    // Public property to access the EmulatorView
    public EmulatorView? GetEmulatorView()
    {
        return this.FindControl<EmulatorView>("EmulatorView");
    }

    /// <summary>
    /// Handle key down events and forward them to the host app
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Prevent keys from being processed by Avalonia's focus system
        //e.Handled = true;
        //HostApp?.OnKeyDown(e.Key, e.KeyModifiers);

        // Check for Ctrl+Shift+D to open the sound debug overlay
        if (e.Key == Key.D && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            ShowSoundDebug();
        }
    }

    private void OpenSoundDebug_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(ShowSoundDebug);

    private async Task ShowSoundDebug()
    {
        // Only allow opening the sound debug overlay when the emulator is uninitialized
        if (_subscribedViewModel.HostApp.EmulatorState != EmulatorState.Uninitialized)
            return;

        await SoundDebugUserControlOverlay();
    }

    private async Task SoundDebugUserControlOverlay()
    {
        var serviceProvider = (Application.Current as App)?.GetServiceProvider();
        if (serviceProvider == null)
        {
            Logger.LogError("Could not get service provider");
            return;
        }

        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var wavePlayerFactory = new WavePlayerFactory(loggerFactory, _subscribedViewModel!.HostApp.EmulatorConfig);
        var wavePlayer = wavePlayerFactory.CreateWavePlayer();

        // Create the UserControl-based config
        var configControl = new DebugSoundUserControl
        {
            DataContext = new DebugSoundViewModel(
                _subscribedViewModel!.HostApp!,
                serviceProvider.GetRequiredService<IConfiguration>(),
                loggerFactory,
                wavePlayer)
        };

        // Set up event handling for configuration completion
        var taskCompletionSource = new TaskCompletionSource<bool>();
        configControl.CloseRequested += (s, closed) =>
        {
            taskCompletionSource.SetResult(closed);
        };

        // Show user control in overlay dialog
        var overlayDialogHelper = serviceProvider.GetRequiredService<OverlayDialogHelper>();
        var overlayPanel = overlayDialogHelper.BuildOverlayDialogPanel(configControl);
        var mainGrid = overlayDialogHelper.ShowOverlayDialog(overlayPanel, this);

        // Wait for the dialog to complete
        try
        {
            await taskCompletionSource.Task;
        }
        finally
        {
            // Clean up - remove the overlay
            mainGrid.Children.Remove(overlayPanel);
        }
    }

    private void OpenGamepadDebug_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(ShowGamepadDebug);

    private async Task ShowGamepadDebug()
    {
        // Only allow opening the gamepad debug overlay when the emulator is uninitialized
        if (_subscribedViewModel?.HostApp.EmulatorState != EmulatorState.Uninitialized)
            return;

        await GamepadDebugUserControlOverlay();
    }

    private async Task GamepadDebugUserControlOverlay()
    {
        var serviceProvider = (Application.Current as App)?.GetServiceProvider();
        if (serviceProvider == null)
        {
            Logger.LogError("Could not get service provider");
            return;
        }

        if (_subscribedViewModel?.HostApp == null)
            return;

        // Create the UserControl-based config
        var configControl = new DebugGamepadUserControl
        {
            DataContext = new DebugGamepadViewModel(_subscribedViewModel.HostApp)
        };

        // Set up event handling for configuration completion
        var taskCompletionSource = new TaskCompletionSource<bool>();
        configControl.CloseRequested += (s, closed) =>
        {
            taskCompletionSource.SetResult(closed);
        };


        // Show user control in overlay dialog
        var overlayDialogHelper = serviceProvider.GetRequiredService<OverlayDialogHelper>();
        var overlayPanel = overlayDialogHelper.BuildOverlayDialogPanel(configControl);
        var mainGrid = overlayDialogHelper.ShowOverlayDialog(overlayPanel, this);

        // Wait for the dialog to complete
        try
        {
            await taskCompletionSource.Task;
        }
        finally
        {
            // Clean up - remove the overlay
            mainGrid.Children.Remove(overlayPanel);
        }
    }

    private void OnEmulatorOptionsRequested(object? sender, EventArgs e)
        => SafeAsyncHelper.Execute(EmulatorOptionsUserControlOverlay);

    private void OnRequestAddScript(object? sender, EventArgs e)
        => SafeAsyncHelper.Execute(ShowAddScriptDialog);

    private void OnRequestEditScript(object? sender, string fileName)
        => SafeAsyncHelper.Execute(() => ShowEditScriptDialog(fileName));

    private async Task ShowAddScriptDialog()
    {
        if (_subscribedViewModel?.HostApp == null) return;
        var vm = new ScriptEditorViewModel(isNew: true);
        await OpenScriptEditorDialog(vm);
        if (vm.DialogResult is { } result)
            _subscribedViewModel.HostApp.SaveScript(result.fileName, result.content);
    }

    private async Task ShowEditScriptDialog(string fileName)
    {
        if (_subscribedViewModel?.HostApp == null) return;
        var content = _subscribedViewModel.HostApp.LoadScriptContent(fileName) ?? string.Empty;
        var vm = new ScriptEditorViewModel(isNew: false, fileName, content);
        await OpenScriptEditorDialog(vm);
        if (vm.DialogResult is { } result)
            _subscribedViewModel.HostApp.SaveScript(result.fileName, result.content);
    }

    private async Task OpenScriptEditorDialog(ScriptEditorViewModel vm)
    {
        var serviceProvider = (Application.Current as App)?.GetServiceProvider();
        if (serviceProvider == null)
        {
            Logger.LogError("Could not get service provider");
            return;
        }

        var editorControl = new ScriptEditorDialog { DataContext = vm };

        var tcs = new TaskCompletionSource<bool>();
        editorControl.DialogCompleted += (_, saved) => tcs.TrySetResult(saved);

        var overlayDialogHelper = serviceProvider.GetRequiredService<OverlayDialogHelper>();
        var overlayPanel = overlayDialogHelper.BuildOverlayDialogPanel(editorControl);
        var mainGrid = overlayDialogHelper.ShowOverlayDialog(overlayPanel, this);

        try
        {
            await tcs.Task;
        }
        finally
        {
            mainGrid.Children.Remove(overlayPanel);
        }
    }

    private void OnRequestDeleteScript(object? sender, DeleteScriptConfirmationEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
        {
            var confirmed = await ShowDeleteScriptConfirmationOverlay(e.FileName);
            e.SetResult(confirmed);
        });

    private async Task<bool> ShowDeleteScriptConfirmationOverlay(string fileName)
    {
        var tcs = new TaskCompletionSource<bool>();
        Panel? overlayPanel = null;
        Grid? mainGrid = null;

        void CloseOverlay()
        {
            if (overlayPanel != null && mainGrid != null)
                mainGrid.Children.Remove(overlayPanel);
        }

        var deleteButton = new Button
        {
            Content = "Delete",
            Width = 60,
            Classes = { "small", "danger" }
        };
        deleteButton.Click += (_, _) => { CloseOverlay(); tcs.TrySetResult(true); };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 60,
            Classes = { "small", "cancel" }
        };
        cancelButton.Click += (_, _) => { CloseOverlay(); tcs.TrySetResult(false); };

        var dialogContent = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(26, 32, 44)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(74, 85, 104)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(0),
            MaxWidth = 380,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel
            {
                Children =
                {
                    new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(45, 55, 72)),
                        Padding = new Thickness(5),
                        CornerRadius = new CornerRadius(4, 4, 0, 0),
                        Child = new TextBlock
                        {
                            Text = "Delete Script",
                            FontSize = 14,
                            FontWeight = FontWeight.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    },
                    new StackPanel
                    {
                        Margin = new Thickness(16, 12),
                        Spacing = 12,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = $"Are you sure you want to delete '{fileName}'?",
                                FontSize = 11,
                                TextWrapping = TextWrapping.Wrap
                            },
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                Spacing = 10,
                                Children = { cancelButton, deleteButton }
                            }
                        }
                    }
                }
            }
        };

        overlayPanel = new Panel
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            ZIndex = 1000,
            Children = { dialogContent }
        };

        var serviceProvider = (Application.Current as App)?.GetServiceProvider();
        if (serviceProvider == null)
        {
            Logger.LogError("Could not get service provider");
            return false;
        }
        var overlayDialogHelper = serviceProvider.GetRequiredService<OverlayDialogHelper>();
        mainGrid = overlayDialogHelper.ShowOverlayDialog(overlayPanel, this);

        return await tcs.Task;
    }

    private void OnRequestOpenScriptFolder(object? sender, EventArgs e)
    {
        var dir = _subscribedViewModel?.ScriptDirectory;
        if (string.IsNullOrEmpty(dir)) return;
        var topLevel = TopLevel.GetTopLevel(this);
        topLevel?.Launcher.LaunchUriAsync(new Uri("file://" + dir));
    }

    private async Task EmulatorOptionsUserControlOverlay()
    {
        if (_subscribedViewModel?.HostApp == null)
            return;

        // Get IConfiguration from DI
        var serviceProvider = (Application.Current as App)?.GetServiceProvider();
        if (serviceProvider == null)
            return;

        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        // Create the UserControl-based config
        var configControl = new EmulatorConfigUserControl
        {
            DataContext = new EmulatorConfigViewModel(_subscribedViewModel.HostApp, configuration)
        };
        // Set up event handling for configuration completion
        var taskCompletionSource = new TaskCompletionSource<bool>();
        configControl.ConfigurationChanged += (s, saved) =>
        {
            if (saved)
            {
                // Refresh config-dependent properties in MainViewModel
                _subscribedViewModel?.RefreshConfigProperties();
            }
            taskCompletionSource.SetResult(saved);
        };

        // Show user control in overlay dialog
        var overlayDialogHelper = serviceProvider.GetRequiredService<OverlayDialogHelper>();
        var overlayPanel = overlayDialogHelper.BuildOverlayDialogPanel(configControl);

        var mainGrid = overlayDialogHelper.ShowOverlayDialog(overlayPanel, this);

        // Wait for the dialog to complete
        try
        {
            await taskCompletionSource.Task;
        }
        finally
        {
            // Clean up - remove the overlay
            mainGrid.Children.Remove(overlayPanel);
        }
    }
}
