using Foundation;
using Avalonia.iOS;

namespace Highbyte.DotNet6502.App.Avalonia.iOS;

// The UIApplicationDelegate for the application. This class is responsible for launching the 
// User Interface of the application, as well as listening (and optionally responding) to 
// application events from iOS.
[Register("AppDelegate")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public partial class AppDelegate : AvaloniaAppDelegate<Highbyte.DotNet6502.App.Avalonia.Core.App>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
}
