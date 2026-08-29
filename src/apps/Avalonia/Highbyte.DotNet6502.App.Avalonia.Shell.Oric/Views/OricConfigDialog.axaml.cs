using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Oric.Views;

public partial class OricConfigDialog : Window
{
    public OricConfigDialog() => AvaloniaXamlLoader.Load(this);
    public void OnConfigurationChanged(object? sender, bool saved) => Close(saved);
}
