
Browser-specific:
- When starting C64 assembly example programs the frame rate doubles. Some event or timer that get dobule triggered?  Not a problem in Desktop (seemingly).

Monitor:
- Activating monitor with F12, then typing commands, after that the Close button or "g" command no longer works to close the monitor and resume the emulator.

EmulatorPlaceholderView: 
- Fix setting size based on selected emulator.


MainView / MainViewModel
- Using HostApp directly should probably be moved to ViewModel.
- Use Reactive commands in ViewModel instead of button click handlers.

C64MenuView / C64MenuViewModel: 
- Move logic to ViewModel, use Reactive commands instead of button click handlers.

MonitorDialog/MonitorUserControl (and related ViewModels):
- Move logic to ViewModel, use Reactive commands instead of button click handlers.

C64ConfigDialog/C64ConfigUserControl:
- Move logic to ViewModel, use Reactive commands instead of button click handlers.
