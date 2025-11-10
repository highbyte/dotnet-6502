using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Highbyte.DotNet6502.App.Avalonia.Core;

namespace Highbyte.DotNet6502.App.Avalonia.Android;

[Activity(
    Label = "Avalonia.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<Highbyte.DotNet6502.App.Avalonia.Core.App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
