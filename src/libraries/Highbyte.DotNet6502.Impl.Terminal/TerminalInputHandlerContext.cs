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

    private readonly ILogger _logger;
    private readonly object _lock = new();

    // HostKey -> Stopwatch-timestamp (ms) when it should stop being reported as down.
    private readonly Dictionary<HostKey, long> _keyExpiry = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();

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
        var expireAt = _clock.ElapsedMilliseconds + KeyHoldMilliseconds;
        lock (_lock)
        {
            // A printable key with Shift held also presses the Shift modifier so the C64 input
            // handler can produce shifted glyphs/symbols.
            if (key.IsShift)
                _keyExpiry[HostKey.ShiftLeft] = expireAt;

            var hostKey = TerminalKeyMap.MapToHostKey(key);
            if (hostKey != HostKey.None)
                _keyExpiry[hostKey] = expireAt;
            else if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Unmapped terminal key: KeyCode={KeyCode} Rune={Rune}", key.KeyCode, key.AsRune);
        }
    }

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
