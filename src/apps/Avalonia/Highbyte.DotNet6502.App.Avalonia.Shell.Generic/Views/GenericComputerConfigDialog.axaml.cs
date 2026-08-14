using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Generic.Views;

public partial class GenericComputerConfigDialog : Window
{
    public bool? DialogResultValue { get; private set; }

    public GenericComputerConfigDialog()
    {
        InitializeComponent();
        DialogResultValue = false;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public void OnConfigurationChanged(object? sender, bool saved)
    {
        DialogResultValue = saved;
        Close(saved);
    }
}
