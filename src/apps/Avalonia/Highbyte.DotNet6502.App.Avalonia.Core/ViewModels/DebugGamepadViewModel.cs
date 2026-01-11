using System;
using System.Collections.ObjectModel;
using System.Reactive;
using Avalonia.Threading;
using Highbyte.DotNet6502.Impl.Avalonia.Input;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class DebugGamepadViewModel : ViewModelBase
{
    private readonly AvaloniaHostApp _hostApp;
    private readonly DispatcherTimer _updateTimer;

    private bool _isBusy;
    private string? _statusMessage;

    // ReactiveUI Commands
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    public DebugGamepadViewModel(AvaloniaHostApp hostApp)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));

        CloseCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () =>
            {
                StopPolling();
                CloseRequested?.Invoke(this, true);
            },
            outputScheduler: RxApp.MainThreadScheduler);

        // Set up a timer to poll gamepad state
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50) // Poll at ~20 Hz
        };
        _updateTimer.Tick += UpdateTimerTick;
    }

    public event EventHandler<bool>? CloseRequested;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
                return;

            this.RaiseAndSetIfChanged(ref _isBusy, value);
            this.RaisePropertyChanged(nameof(IsNotBusy));
            this.RaisePropertyChanged(nameof(CanClose));
        }
    }

    public bool IsNotBusy => !IsBusy;

    public bool CanClose => IsNotBusy;

    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
                return;

            this.RaiseAndSetIfChanged(ref _statusMessage, value);
            this.RaisePropertyChanged(nameof(HasStatusMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    // Gamepad state properties
    private bool _isGamepadConnected;
    public bool IsGamepadConnected
    {
        get => _isGamepadConnected;
        private set => this.RaiseAndSetIfChanged(ref _isGamepadConnected, value);
    }

    private string? _gamepadName;
    public string? GamepadName
    {
        get => _gamepadName;
        private set => this.RaiseAndSetIfChanged(ref _gamepadName, value);
    }

    private ObservableCollection<string> _pressedButtons = new();
    public ObservableCollection<string> PressedButtons
    {
        get => _pressedButtons;
        private set => this.RaiseAndSetIfChanged(ref _pressedButtons, value);
    }

    public bool HasPressedButtons => _pressedButtons.Count > 0;

    // Individual button states for visual representation
    private bool _isAPressed;
    public bool IsAPressed
    {
        get => _isAPressed;
        private set => this.RaiseAndSetIfChanged(ref _isAPressed, value);
    }

    private bool _isBPressed;
    public bool IsBPressed
    {
        get => _isBPressed;
        private set => this.RaiseAndSetIfChanged(ref _isBPressed, value);
    }

    private bool _isXPressed;
    public bool IsXPressed
    {
        get => _isXPressed;
        private set => this.RaiseAndSetIfChanged(ref _isXPressed, value);
    }

    private bool _isYPressed;
    public bool IsYPressed
    {
        get => _isYPressed;
        private set => this.RaiseAndSetIfChanged(ref _isYPressed, value);
    }

    private bool _isDPadUpPressed;
    public bool IsDPadUpPressed
    {
        get => _isDPadUpPressed;
        private set => this.RaiseAndSetIfChanged(ref _isDPadUpPressed, value);
    }

    private bool _isDPadDownPressed;
    public bool IsDPadDownPressed
    {
        get => _isDPadDownPressed;
        private set => this.RaiseAndSetIfChanged(ref _isDPadDownPressed, value);
    }

    private bool _isDPadLeftPressed;
    public bool IsDPadLeftPressed
    {
        get => _isDPadLeftPressed;
        private set => this.RaiseAndSetIfChanged(ref _isDPadLeftPressed, value);
    }

    private bool _isDPadRightPressed;
    public bool IsDPadRightPressed
    {
        get => _isDPadRightPressed;
        private set => this.RaiseAndSetIfChanged(ref _isDPadRightPressed, value);
    }

    private bool _isLeftBumperPressed;
    public bool IsLeftBumperPressed
    {
        get => _isLeftBumperPressed;
        private set => this.RaiseAndSetIfChanged(ref _isLeftBumperPressed, value);
    }

    private bool _isRightBumperPressed;
    public bool IsRightBumperPressed
    {
        get => _isRightBumperPressed;
        private set => this.RaiseAndSetIfChanged(ref _isRightBumperPressed, value);
    }

    private bool _isLeftTriggerPressed;
    public bool IsLeftTriggerPressed
    {
        get => _isLeftTriggerPressed;
        private set => this.RaiseAndSetIfChanged(ref _isLeftTriggerPressed, value);
    }

    private bool _isRightTriggerPressed;
    public bool IsRightTriggerPressed
    {
        get => _isRightTriggerPressed;
        private set => this.RaiseAndSetIfChanged(ref _isRightTriggerPressed, value);
    }

    private bool _isStartPressed;
    public bool IsStartPressed
    {
        get => _isStartPressed;
        private set => this.RaiseAndSetIfChanged(ref _isStartPressed, value);
    }

    private bool _isBackPressed;
    public bool IsBackPressed
    {
        get => _isBackPressed;
        private set => this.RaiseAndSetIfChanged(ref _isBackPressed, value);
    }

    private bool _isLeftStickPressed;
    public bool IsLeftStickPressed
    {
        get => _isLeftStickPressed;
        private set => this.RaiseAndSetIfChanged(ref _isLeftStickPressed, value);
    }

    private bool _isRightStickPressed;
    public bool IsRightStickPressed
    {
        get => _isRightStickPressed;
        private set => this.RaiseAndSetIfChanged(ref _isRightStickPressed, value);
    }

    private bool _isGuidePressed;
    public bool IsGuidePressed
    {
        get => _isGuidePressed;
        private set => this.RaiseAndSetIfChanged(ref _isGuidePressed, value);
    }

    /// <summary>
    /// Start polling the gamepad for input.
    /// </summary>
    public void StartPolling()
    {
        _updateTimer.Start();
        StatusMessage = "Polling gamepad input...";
    }

    /// <summary>
    /// Stop polling the gamepad for input.
    /// </summary>
    public void StopPolling()
    {
        _updateTimer.Stop();
        StatusMessage = "Polling stopped.";
    }

    private void UpdateTimerTick(object? sender, EventArgs e)
    {
        UpdateGamepadState();
    }

    private void UpdateGamepadState()
    {
        var inputContext = _hostApp.InputHandlerContext;
        var gamepad = inputContext.Gamepad;

        // Update the gamepad state
        gamepad.Update();

        // Update connection status
        IsGamepadConnected = gamepad.IsConnected;
        GamepadName = gamepad.GamepadName ?? "No gamepad connected";

        if (!IsGamepadConnected)
        {
            ClearAllButtonStates();
            StatusMessage = "No gamepad connected. Connect a gamepad and press buttons.";
            return;
        }

        // Get currently pressed buttons
        var buttonsDown = inputContext.GamepadButtonsDown;

        // Update pressed buttons list
        PressedButtons.Clear();
        foreach (var button in buttonsDown)
        {
            PressedButtons.Add(button.ToString());
        }
        this.RaisePropertyChanged(nameof(HasPressedButtons));

        // Update individual button states
        IsAPressed = buttonsDown.Contains(GamepadButton.A);
        IsBPressed = buttonsDown.Contains(GamepadButton.B);
        IsXPressed = buttonsDown.Contains(GamepadButton.X);
        IsYPressed = buttonsDown.Contains(GamepadButton.Y);
        IsDPadUpPressed = buttonsDown.Contains(GamepadButton.DPadUp);
        IsDPadDownPressed = buttonsDown.Contains(GamepadButton.DPadDown);
        IsDPadLeftPressed = buttonsDown.Contains(GamepadButton.DPadLeft);
        IsDPadRightPressed = buttonsDown.Contains(GamepadButton.DPadRight);
        IsLeftBumperPressed = buttonsDown.Contains(GamepadButton.LeftBumper);
        IsRightBumperPressed = buttonsDown.Contains(GamepadButton.RightBumper);
        IsLeftTriggerPressed = buttonsDown.Contains(GamepadButton.LeftTrigger);
        IsRightTriggerPressed = buttonsDown.Contains(GamepadButton.RightTrigger);
        IsStartPressed = buttonsDown.Contains(GamepadButton.Start);
        IsBackPressed = buttonsDown.Contains(GamepadButton.Back);
        IsLeftStickPressed = buttonsDown.Contains(GamepadButton.LeftStick);
        IsRightStickPressed = buttonsDown.Contains(GamepadButton.RightStick);
        IsGuidePressed = buttonsDown.Contains(GamepadButton.Guide);

        StatusMessage = "Gamepad connected. Press buttons to see input.";

        //if (buttonsDown.Count > 0)
        //{
        //    StatusMessage = $"Buttons pressed: {string.Join(", ", buttonsDown)}";
        //}
        //else
        //{
        //    StatusMessage = "Gamepad connected. Press buttons to see input.";
        //}
    }

    private void ClearAllButtonStates()
    {
        PressedButtons.Clear();
        this.RaisePropertyChanged(nameof(HasPressedButtons));

        IsAPressed = false;
        IsBPressed = false;
        IsXPressed = false;
        IsYPressed = false;
        IsDPadUpPressed = false;
        IsDPadDownPressed = false;
        IsDPadLeftPressed = false;
        IsDPadRightPressed = false;
        IsLeftBumperPressed = false;
        IsRightBumperPressed = false;
        IsLeftTriggerPressed = false;
        IsRightTriggerPressed = false;
        IsStartPressed = false;
        IsBackPressed = false;
        IsLeftStickPressed = false;
        IsRightStickPressed = false;
        IsGuidePressed = false;
    }
}
