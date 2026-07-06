using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Highbyte.DotNet6502.App.Avalonia.Core.Services;
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
        (Key.I, "InformationTabItem",     "Information"),
        (Key.C, "ConfigStatusTabItem",     "Config status"),
        (Key.L, "LogTabItem",              "Log"),
        (Key.S, "ScriptsTabItem",          "Scripts"),
        (Key.D, "DebugAndRemotingTabItem", "Debug & Remoting"),
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
            await FadeInAsync();

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

    private async Task FadeInAsync()
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
            _subscribedViewModel.AboutRequested -= OnAboutRequested;
        }

        // Subscribe to new ViewModel's property changes
        _subscribedViewModel = DataContext as MainViewModel;
        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            _subscribedViewModel.EmulatorOptionsRequested += OnEmulatorOptionsRequested;
            _subscribedViewModel.AboutRequested += OnAboutRequested;
            _subscribedViewModel.RequestAddScript += OnRequestAddScript;
            _subscribedViewModel.RequestEditScript += OnRequestEditScript;
            _subscribedViewModel.RequestDeleteScript += OnRequestDeleteScript;
            _subscribedViewModel.RequestOpenScriptFolder += OnRequestOpenScriptFolder;
            _subscribedViewModel.RequestOpenSnapshotFolder += OnRequestOpenSnapshotFolder;
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

    // True while the Config status tab was auto-selected because of validation errors, so it can be
    // switched back to Information once the errors are resolved (e.g. C64 ROMs downloaded at startup).
    private bool _configStatusTabAutoSelected;

    private void CheckAndSelectValidationErrorsTab()
    {
        if (_subscribedViewModel == null)
            return;

        // Check the actual ValidationErrors collection instead of IsSystemConfigValid
        // to avoid timing issues with reactive property updates
        var hasValidationErrors = _subscribedViewModel.ValidationErrors is { Count: > 0 };

        // Use Dispatcher to ensure the control is properly initialized
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<TabControl>("InformationTabControl") is not TabControl tabControl)
                return;

            if (hasValidationErrors)
            {
                // Surface the errors by activating the Config status tab.
                if (this.FindControl<TabItem>("ConfigStatusTabItem") is TabItem configErrorsTab)
                {
                    tabControl.SelectedItem = configErrorsTab;
                    _configStatusTabAutoSelected = true;
                }
            }
            else if (_configStatusTabAutoSelected)
            {
                // Errors resolved (e.g. missing C64 ROMs were downloaded via the startup
                // acknowledgement dialog). Return to the Information tab — but only if Config status
                // is still the active tab, so a user who navigated elsewhere isn't yanked away.
                _configStatusTabAutoSelected = false;
                if (this.FindControl<TabItem>("ConfigStatusTabItem") is TabItem configErrorsTab
                    && ReferenceEquals(tabControl.SelectedItem, configErrorsTab)
                    && this.FindControl<TabItem>("InformationTabItem") is TabItem informationTab)
                {
                    tabControl.SelectedItem = informationTab;
                }
            }
        });
    }

    private void OnDigitsOnlyTextInput(object? sender, TextInputEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text) && e.Text.Any(ch => !char.IsDigit(ch)))
        {
            e.Handled = true;
        }
    }

    private void OnIpv4TextInput(object? sender, TextInputEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text) && e.Text.Any(ch => !char.IsDigit(ch) && ch != '.'))
        {
            e.Handled = true;
        }
    }

    private void OnPortTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        var rawText = textBox.Text ?? string.Empty;
        var filteredText = new string(rawText.Where(char.IsDigit).Take(5).ToArray());
        if (filteredText.Length > 0 && (!int.TryParse(filteredText, out var parsedPort) || parsedPort < 1 || parsedPort > 65535))
        {
            filteredText = textBox.Tag as string ?? string.Empty;
        }

        if (!string.Equals(textBox.Text, filteredText, StringComparison.Ordinal))
        {
            textBox.Text = filteredText;
            textBox.CaretIndex = filteredText.Length;
            return;
        }

        textBox.Tag = filteredText;
    }

    private void OnIpv4TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        var rawText = textBox.Text ?? string.Empty;
        var filteredText = new string(rawText.Where(ch => char.IsDigit(ch) || ch == '.').ToArray());
        if (!string.Equals(textBox.Text, filteredText, StringComparison.Ordinal))
        {
            textBox.Text = filteredText;
            textBox.CaretIndex = filteredText.Length;
        }
    }

    private void MainView_Loaded(object? sender, RoutedEventArgs e)
    {
        // Kick off the non-blocking startup update check (a no-op in the browser host).
        if (_subscribedViewModel != null)
            SafeAsyncHelper.Execute(_subscribedViewModel.CheckForUpdatesOnStartupAsync);
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
            _subscribedViewModel.AboutRequested -= OnAboutRequested;
            _subscribedViewModel.RequestAddScript -= OnRequestAddScript;
            _subscribedViewModel.RequestEditScript -= OnRequestEditScript;
            _subscribedViewModel.RequestDeleteScript -= OnRequestDeleteScript;
            _subscribedViewModel.RequestOpenScriptFolder -= OnRequestOpenScriptFolder;
            _subscribedViewModel.RequestOpenSnapshotFolder -= OnRequestOpenSnapshotFolder;
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

            // Always add an "Emulator" menu for common (non-system-specific) toggles/actions, parallel
            // to the per-system menu. Currently holds the Snapshot sidebar-section toggle. The
            // ⌘⌥⇧ modifier matches the per-system "toggle section" convention (see the C64 menu);
            // S = Snapshot, and ⌘⌥⇧S does not collide with View (⌘⌥) or the C64 ⌘⌥⇧ set {D,L,C,1,2}.
            var emulatorRoot = new NativeMenuItem { Header = "Emulator" };
            var emulatorMenu = new NativeMenu();
            emulatorMenu.Items.Add(new NativeMenuItem
            {
                Header = "Toggle Snapshot section",
                Gesture = new KeyGesture(Key.S, KeyModifiers.Meta | KeyModifiers.Alt | KeyModifiers.Shift),
                Command = _subscribedViewModel?.ToggleSnapshotSectionCommand
            });
            emulatorRoot.Menu = emulatorMenu;
            appMenu.Items.Add(emulatorRoot);

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

        // Common "Emulator" menu shortcut (Windows/Linux counterpart of the macOS Emulator menu):
        // Toggle Snapshot section. Ctrl+Alt+Shift+S mirrors the C64 section-toggle modifier and does
        // not collide with the tab shortcuts above (Ctrl+Alt, no Shift) or the C64 Ctrl+Alt+Shift set.
        if (_subscribedViewModel?.ToggleSnapshotSectionCommand is { } toggleSnapshotCommand)
        {
            var snapshotKb = new KeyBinding
            {
                Gesture = new KeyGesture(Key.S, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift),
                Command = toggleSnapshotCommand
            };
            window.KeyBindings.Add(snapshotKb);
            _generalKeyBindings.Add(snapshotKb);
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

    private void CopyAllLog_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
        {
            if (_subscribedViewModel == null) return;
            if (TopLevel.GetTopLevel(this) is not { } topLevel) return;
            if (topLevel.Clipboard is not { } clipboard) return;

            var text = string.Join(Environment.NewLine,
            _subscribedViewModel.LogMessages.Select(m => m.Message));
            using var data = new DataTransfer();
            data.Add(DataTransferItem.CreateText(text));
            await clipboard.SetDataAsync(data);
        });

    private void LogMessages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_logScrollViewer == null)
            return;
        if (_logAutoScrollEnabled)
        {
            // Scroll to bottom after layout/render
            Dispatcher.UIThread.Post(
                () => SafeAsyncHelper.Execute(ScrollLogToBottomAfterLayoutAsync),
                DispatcherPriority.Loaded);
        }
    }

    private async Task ScrollLogToBottomAfterLayoutAsync()
    {
        await Task.Delay(10);

        if (_logScrollViewer == null)
            return;

        double maxY = Math.Max(0, _logScrollViewer.Extent.Height - _logScrollViewer.Viewport.Height);
        _logScrollViewer.Offset = new Vector(_logScrollViewer.Offset.X, maxY);
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
            SafeAsyncHelper.Execute(ShowSoundDebugAsync);
        }
    }

    private void OpenSoundDebug_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(ShowSoundDebugAsync);

    private async Task ShowSoundDebugAsync()
    {
        var subscribedViewModel = _subscribedViewModel;
        if (subscribedViewModel == null)
            return;

        // Only allow opening the sound debug overlay when the emulator is uninitialized
        if (subscribedViewModel.HostApp.EmulatorState != EmulatorState.Uninitialized)
            return;

        await SoundDebugUserControlOverlayAsync(subscribedViewModel.HostApp);
    }

    private async Task SoundDebugUserControlOverlayAsync(AvaloniaHostApp hostApp)
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

        var wavePlayerFactory = new WavePlayerFactory(loggerFactory, hostApp.EmulatorConfig);
        var wavePlayer = wavePlayerFactory.CreateWavePlayer();

        // Create the UserControl-based config
        var configControl = new DebugSoundUserControl
        {
            DataContext = new DebugSoundViewModel(
                hostApp,
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
        => SafeAsyncHelper.Execute(ShowGamepadDebugAsync);

    private async Task ShowGamepadDebugAsync()
    {
        // Only allow opening the gamepad debug overlay when the emulator is uninitialized
        if (_subscribedViewModel?.HostApp.EmulatorState != EmulatorState.Uninitialized)
            return;

        await GamepadDebugUserControlOverlayAsync();
    }

    private async Task GamepadDebugUserControlOverlayAsync()
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
        => SafeAsyncHelper.Execute(EmulatorOptionsUserControlOverlayAsync);

    private void OnAboutRequested(object? sender, EventArgs e)
        => SafeAsyncHelper.Execute(ShowAboutOverlayAsync);

    private async Task ShowAboutOverlayAsync()
    {
        var serviceProvider = (Application.Current as App)?.GetServiceProvider();
        if (serviceProvider == null)
            return;

        var updateService = serviceProvider.GetRequiredService<Services.IAppUpdateService>();
        var aboutControl = new AboutUserControl
        {
            DataContext = new AboutViewModel(updateService),
        };

        var tcs = new TaskCompletionSource<bool>();
        aboutControl.DialogClosed += (_, _) => tcs.TrySetResult(true);

        var overlayDialogHelper = serviceProvider.GetRequiredService<OverlayDialogHelper>();
        var overlayPanel = overlayDialogHelper.BuildOverlayDialogPanel(aboutControl);
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

    private void OnRequestAddScript(object? sender, EventArgs e)
        => SafeAsyncHelper.Execute(ShowAddScriptDialogAsync);

    private void OnRequestEditScript(object? sender, string fileName)
        => SafeAsyncHelper.Execute(() => ShowEditScriptDialogAsync(fileName));

    private async Task ShowAddScriptDialogAsync()
    {
        if (_subscribedViewModel?.HostApp == null) return;
        var vm = new ScriptEditorViewModel(isNew: true);
        await OpenScriptEditorDialogAsync(vm);
        if (vm.DialogResult is { } result)
            _subscribedViewModel.HostApp.SaveScript(result.fileName, result.content);
    }

    private async Task ShowEditScriptDialogAsync(string fileName)
    {
        if (_subscribedViewModel?.HostApp == null) return;
        var content = _subscribedViewModel.HostApp.LoadScriptContent(fileName) ?? string.Empty;
        var vm = new ScriptEditorViewModel(isNew: false, fileName, content);
        await OpenScriptEditorDialogAsync(vm);
        if (vm.DialogResult is { } result)
            _subscribedViewModel.HostApp.SaveScript(result.fileName, result.content);
    }

    private async Task OpenScriptEditorDialogAsync(ScriptEditorViewModel vm)
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
            var confirmed = await ShowDeleteScriptConfirmationOverlayAsync(e.FileName);
            e.SetResult(confirmed);
        });

    private async Task<bool> ShowDeleteScriptConfirmationOverlayAsync(string fileName)
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
        => SafeAsyncHelper.Execute(OpenScriptFolderAsync);

    private void OnRequestOpenSnapshotFolder(object? sender, EventArgs e)
        => SafeAsyncHelper.Execute(OpenSnapshotFolderAsync);

    private Task OpenScriptFolderAsync()
    {
        var dir = _subscribedViewModel?.ScriptDirectory;
        return OpenFolderAsync(dir);
    }

    private Task OpenSnapshotFolderAsync()
    {
        var dir = _subscribedViewModel?.SnapshotDirectory;
        return OpenFolderAsync(dir);
    }

    private Task OpenFolderAsync(string? dir)
    {
        if (string.IsNullOrEmpty(dir))
            return Task.CompletedTask;

        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            return topLevel.Launcher.LaunchUriAsync(new Uri("file://" + dir));
        }

        return Task.CompletedTask;
    }

    private async Task EmulatorOptionsUserControlOverlayAsync()
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

    // Emulator state snapshots (save/restore). Cross-system feature, so it lives in the common
    // MainView rather than a per-system menu. Only systems whose ISystem implements
    // ISystemSnapshotProvider can be snapshotted (currently the Generic computer); for others the
    // save is skipped with a log message.
    private void SaveSnapshot_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
        {
            var hostApp = _subscribedViewModel?.HostApp;
            if (hostApp == null)
                return;
            if (!hostApp.CanSnapshotCurrentSystem)
            {
                Logger.LogWarning("Current system '{System}' does not support snapshots yet.", hostApp.SelectedSystemName);
                return;
            }

            var serviceProvider = (Application.Current as App)?.GetServiceProvider();
            var fileSaver = serviceProvider?.GetService<IAppFileSaver>();
            if (fileSaver == null)
                return;

            // Pause while the save dialog is open so the emulator isn't advancing underneath: the
            // state written is exactly the frozen frame the user sees, with no ambiguity about which
            // moment was captured. Resumed afterwards if it had been running.
            var wasRunning = hostApp.EmulatorState == EmulatorState.Running;
            if (wasRunning)
                hostApp.Pause();
            try
            {
                // Capture the snapshot to memory, then hand the bytes to the platform saver:
                // Desktop writes via the StorageProvider save picker; Browser triggers a download.
                // Optionally embed current runtime settings ("config") per the user's checkbox.
                using var buffer = new MemoryStream();
                await hostApp.SaveSnapshotAsync(buffer, includeConfig: _subscribedViewModel?.IncludeConfigInSnapshot ?? false);

                // SuggestedFileName is extension-less; each saver adds the extension (Desktop via the
                // StorageProvider DefaultExtension, Browser by appending it to the download name).
                var suggestedName = hostApp.SelectedSystemName.Replace(" ", "_");
                var snapshotDirectory = EnsureSnapshotDirectory(hostApp.EmulatorConfig);
                var saved = await fileSaver.SaveFileAsync(
                    this,
                    new AppFileSaveOptions(
                        "Save emulator snapshot",
                        suggestedName,
                        "d6502snap",
                        [
                            new AppFilePickerFileType("Emulator snapshot", ["*.d6502snap"]),
                            AppFilePickerFileType.AllFiles
                        ],
                        snapshotDirectory),
                    buffer.ToArray());

                if (saved)
                    Logger.LogInformation("Snapshot saved ({Name}.d6502snap).", suggestedName);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving snapshot");

                // Rethrow so the app's global error handling surfaces the failure to the user instead
                // of silently leaving them looking at an unchanged screen with only a log entry.
                throw;
            }
            finally
            {
                if (wasRunning && hostApp.EmulatorState == EmulatorState.Paused)
                    await hostApp.Start();
            }
        });

    private void LoadSnapshot_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
        {
            var hostApp = _subscribedViewModel?.HostApp;
            if (hostApp == null)
                return;

            var serviceProvider = (Application.Current as App)?.GetServiceProvider();
            var filePicker = serviceProvider?.GetService<IAppFilePicker>();
            if (filePicker == null)
                return;

            // Pause the current machine while the load dialog is open so it isn't running (and making
            // sound) while the user browses for a snapshot to restore.
            var wasRunning = hostApp.EmulatorState == EmulatorState.Running;
            if (wasRunning)
                hostApp.Pause();

            var snapshotDirectory = EnsureSnapshotDirectory(hostApp.EmulatorConfig);
            var picked = await filePicker.OpenFileAsync(
                this,
                new AppFilePickerOpenOptions(
                    "Load emulator snapshot",
                    AllowMultiple: false,
                    [
                        new AppFilePickerFileType("Emulator snapshot", ["*.d6502snap"]),
                        AppFilePickerFileType.AllFiles
                    ],
                    snapshotDirectory));
            if (picked == null)
            {
                // Cancelled — resume the machine we paused for the dialog.
                if (wasRunning && hostApp.EmulatorState == EmulatorState.Paused)
                    await hostApp.Start();
                return;
            }

            try
            {
                using var ms = new MemoryStream(picked.Bytes);
                var result = await hostApp.LoadSnapshotAsync(ms, applyConfig: _subscribedViewModel?.RestoreConfigOnLoad ?? false);

                // LoadSnapshotAsync leaves the machine paused (the shared contract used by the CLI,
                // remote, and scripting surfaces). In the interactive UI, resume immediately so the
                // user continues from the restored state — matching the save-state convention of other
                // emulators. A user who wants to inspect can pause again.
                await hostApp.Start();

                Logger.LogInformation("Snapshot '{Name}' loaded ({WarningCount} warning(s)); emulator resumed.",
                    picked.Name, result.Warnings.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading snapshot");

                // Best-effort: if the load failed before the current machine was torn down (it is
                // still paused), resume it. After a successful Stop() the state is Uninitialized, so
                // this guard leaves it stopped rather than rebuilding a fresh machine.
                if (wasRunning && hostApp.EmulatorState == EmulatorState.Paused)
                    await hostApp.Start();

                // Rethrow so the app's global error handling surfaces the failure to the user (e.g.
                // "Snapshot could not be restored: ROM file does not exist: ...") instead of silently
                // leaving them looking at an unchanged screen with only a log entry to explain why.
                throw;
            }
        });

    private static string? EnsureSnapshotDirectory(EmulatorConfig emulatorConfig)
    {
        if (OperatingSystem.IsBrowser())
            return null;

        var snapshotDirectory = emulatorConfig.ResolvedSnapshotDirectory();
        if (string.IsNullOrWhiteSpace(snapshotDirectory))
            return null;

        Directory.CreateDirectory(snapshotDirectory);
        return snapshotDirectory;
    }
}
