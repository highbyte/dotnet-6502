using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Vic20.TimerAndPeripheral;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Vic20.Input;

/// <summary>
/// Host-agnostic VIC-20 input handler.
///
/// Follows the same structure as C64InputHandler:
///   Init()       — called once; stores the host input state provider.
///   BeforeFrame() — called each frame; maps held host keys to VIC-20 matrix keys
///                   and forwards them to Via1.Keyboard.SetKeysPressed().
///   Cleanup()    — no-op.
///
/// The actual keyboard-matrix scanning is performed by the KERNAL via VIA register
/// reads and writes during its 60 Hz IRQ handler — this handler only keeps the
/// "pressed keys" list in Vic20Keyboard up to date.
/// </summary>
public class Vic20InputHandler : IInputConsumer
{
    private readonly Vic20 _vic20;
    private readonly ILogger _logger;
    private readonly Vic20HostKeyboard _hostKeyboard = new();

    private IHostInputState _inputState = default!;

    public ISystem System => _vic20;
    public Instrumentations Instrumentations { get; } = new();

    public Vic20InputHandler(Vic20 vic20, ILoggerFactory loggerFactory)
    {
        _vic20  = vic20;
        _logger = loggerFactory.CreateLogger(nameof(Vic20InputHandler));
    }

    public void Init(IHostInputState inputState)
    {
        _inputState = inputState;
    }

    public void BeforeFrame()
    {
        _inputState.UpdatePerFrame();
        var hostKeysDown = _inputState.KeysDown;
        var vic20Keys = ResolveVic20Keys(hostKeysDown, out bool capsLockOn);
        _vic20.Via1.Keyboard.SetKeysPressed(vic20Keys, capsLockOn);
    }

    public void Cleanup() { }

    public List<string> GetDebugInfo() => new();

    private List<Vic20Key> ResolveVic20Keys(
        IReadOnlySet<HostKey> keysDown,
        out bool capsLockOn)
    {
        capsLockOn = _inputState.CapsLockOn;

        // Find the most-specific matching entries (more host keys in the chord = wins).
        var foundMappings = new List<HostKey[]>();
        foreach (var mapKeys in _hostKeyboard.HostKeyToVic20KeyMap.Keys)
        {
            int matchCount = 0;
            foreach (var k in mapKeys)
                if (keysDown.Contains(k)) matchCount++;

            if (matchCount == mapKeys.Length)
            {
                // Displace any already-found mapping that shares a key with this one.
                for (int i = foundMappings.Count - 1; i >= 0; i--)
                {
                    if (foundMappings[i].Any(x => mapKeys.Contains(x)))
                        foundMappings.RemoveAt(i);
                }
                foundMappings.Add(mapKeys);
            }
        }

        var result = new List<Vic20Key>();
        foreach (var mapKeys in foundMappings)
        {
            foreach (var vic20Key in _hostKeyboard.HostKeyToVic20KeyMap[mapKeys])
            {
                if (!result.Contains(vic20Key))
                    result.Add(vic20Key);
            }
        }

        if (result.Count > 0)
            _logger.LogTrace("VIC-20 input: host=[{h}] vic20=[{v}]",
                string.Join(",", keysDown), string.Join(",", result));

        return result;
    }
}
