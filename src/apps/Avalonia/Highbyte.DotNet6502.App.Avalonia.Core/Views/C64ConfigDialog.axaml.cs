using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class C64ConfigDialog : Window
{
    public bool? DialogResultValue { get; private set; }

    public C64ConfigDialog(
        AvaloniaHostApp hostApp,
        C64HostConfig originalConfig,
        List<(System.Type renderProviderType, System.Type renderTargetType)> renderCombinations)
    {
        InitializeComponent();

        // Initialize the UserControl with the required parameters
        var configUserControl = new C64ConfigUserControl(hostApp, originalConfig, renderCombinations);
        configUserControl.ConfigurationChanged += OnConfigurationChanged;

        // Replace the placeholder UserControl with the initialized one
        Content = configUserControl;

        DialogResultValue = false;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnConfigurationChanged(object? sender, bool saved)
    {
        DialogResultValue = saved;
        Close(saved);
    }
}
