using System.Diagnostics;
using Highbyte.DotNet6502.Systems.Input;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.Terminal;

/// <summary>
/// Terminal host input context. Bridges Terminal.Gui key events to the neutral
/// <see cref="IHostInputState"/> consumed by system input handlers (e.g. the C64 input handler).
///
/// Terminals deliver only "cooked" key-press events — there is no key-up and no reliable
/// key-repeat with held-key semantics. To make a press visible to a system that scans a key matrix
/// each frame, a pressed <see cref="HostKey"/> is held in the down-set for a short window
/// (<see cref="KeyHoldMilliseconds"/>) and then expires. This is enough for typing in BASIC and
/// menu navigation; it cannot reproduce precise held-key timing for action games (a documented
/// limitation of a terminal frontend).
/// </summary>
public sealed class TerminalInputHandlerContext : IInputHandlerContext, IHostInputState
{
    /// <summary>How long a pressed key is reported as held (covers several emulator frames).</summary>
    public const long KeyHoldMilliseconds = 130;
    private const long ModifierKeyHoldMilliseconds = 350;
    private const long TabLatchMilliseconds = 450;

    private readonly ILogger _logger;
    private readonly object _lock = new();

    // HostKey -> Stopwatch-timestamp (ms) when it should stop being reported as down.
    private readonly Dictionary<HostKey, long> _keyExpiry = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _tabLatchExpireAt;

    public bool IsInitialized { get; private set; }

    public TerminalInputHandlerContext(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(nameof(TerminalInputHandlerContext));
    }

    public void Init() => IsInitialized = true;

    public void Cleanup()
    {
        lock (_lock)
            _keyExpiry.Clear();
    }

    /// <summary>
    /// Records a Terminal.Gui key press. Called on the Terminal.Gui main thread from the emulator
    /// screen view's KeyDown handler.
    /// </summary>
    public void OnKeyDown(Key key)
    {
        var now = _clock.ElapsedMilliseconds;
        var expireAt = now + KeyHoldMilliseconds;
        var modifierExpireAt = now + ModifierKeyHoldMilliseconds;
        lock (_lock)
        {
            var hostKey = TerminalKeyMap.MapToHostKey(key);
            var altColorDigit = key.IsAlt && IsC64ColorDigit(hostKey);

            // Terminal key events represent a chord as one event. Record modifier keys alongside
            // the physical key so mappings such as Ctrl+1 (C64 Commodore+1) and Shift+digit work.
            if (key.IsShift)
                _keyExpiry[HostKey.ShiftLeft] = modifierExpireAt;
            if (key.IsCtrl)
                _keyExpiry[HostKey.ControlLeft] = modifierExpireAt;
            if (key.IsAlt && !altColorDigit)
                _keyExpiry[HostKey.AltLeft] = modifierExpireAt;

            if (hostKey != HostKey.None)
            {
                if (altColorDigit)
                    _keyExpiry[HostKey.ControlLeft] = modifierExpireAt;

                if (hostKey == HostKey.Tab)
                    _tabLatchExpireAt = now + TabLatchMilliseconds;
                else if (_tabLatchExpireAt >= now && IsC64ColorDigit(hostKey))
                {
                    // Terminals do not report Tab as a held modifier. Treat a recent Tab press as
                    // a rolling C64 Ctrl latch so holding Tab and pressing multiple digits still
                    // works even though the terminal usually sends only one Tab event.
                    _keyExpiry[HostKey.Tab] = modifierExpireAt;
                    _tabLatchExpireAt = now + TabLatchMilliseconds;
                }
                else
                {
                    _tabLatchExpireAt = 0;
                }

                var isEmulatorModifier = hostKey is HostKey.Tab or HostKey.ControlLeft or HostKey.ControlRight
                    or HostKey.AltLeft or HostKey.AltRight or HostKey.ShiftLeft or HostKey.ShiftRight;
                _keyExpiry[hostKey] = isEmulatorModifier ? modifierExpireAt : expireAt;
            }
            else if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Unmapped terminal key: KeyCode={KeyCode} Rune={Rune}", key.KeyCode, key.AsRune);
        }
    }

    private static bool IsC64ColorDigit(HostKey hostKey)
        => hostKey is HostKey.Digit1 or HostKey.Digit2 or HostKey.Digit3 or HostKey.Digit4
            or HostKey.Digit5 or HostKey.Digit6 or HostKey.Digit7 or HostKey.Digit8;

    // ----------------------------------------------------------------------
    // IHostInputState
    // ----------------------------------------------------------------------

    public IReadOnlySet<HostKey> KeysDown
    {
        get
        {
            var now = _clock.ElapsedMilliseconds;
            lock (_lock)
            {
                var result = new HashSet<HostKey>();
                foreach (var (key, expireAt) in _keyExpiry)
                {
                    if (expireAt >= now)
                        result.Add(key);
                }
                return result;
            }
        }
    }

    public IReadOnlySet<GamepadButton> GamepadButtonsDown { get; } = new HashSet<GamepadButton>();

    public bool CapsLockOn => false;

    public void UpdatePerFrame()
    {
        var now = _clock.ElapsedMilliseconds;
        lock (_lock)
        {
            if (_keyExpiry.Count == 0)
                return;
            var expired = _keyExpiry.Where(kv => kv.Value < now).Select(kv => kv.Key).ToList();
            foreach (var key in expired)
                _keyExpiry.Remove(key);
        }
    }
}
