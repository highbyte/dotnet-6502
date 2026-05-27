using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.Views;

public partial class Vic20MenuView : UserControl
{
    public Vic20MenuView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
