using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.Views;

public partial class Vic20InfoView : UserControl
{
    public Vic20InfoView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
