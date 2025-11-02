
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
