using Highbyte.DotNet6502.Systems.Input;
namespace Highbyte.DotNet6502.Systems.Apple2.Peripherals;

/// <summary>
/// The Apple II game port: up to three pushbuttons and four analog paddles.
///
/// There is no digital joystick on this machine. A "joystick" is two 150k potentiometers — paddle
/// 0 for X, paddle 1 for Y — and the software reads them by timing, not by reading a value. A
/// strobe of <see cref="TriggerAddress"/> fires a 558 one-shot per paddle; bit 7 of that paddle's
/// address then reads high until its timer expires, after a delay proportional to the pot's
/// position. The ROM's PREAD counts loop iterations until the bit drops, and that count is the
/// position.
///
/// <para>
/// <b>Hold time.</b> PREAD's polling loop costs <see cref="PreadLoopCycles"/> cycles per
/// iteration, so holding the bit for <c>position * PreadLoopCycles</c> makes PREAD count back
/// exactly the position that was set, and <c>PDL(n)</c> round-trips. Real hardware is close to
/// this by design but not exactly — the 558 runs on its own RC constant (~11 us per unit) rather
/// than on CPU cycles, so the two only track because the machine was built that way. Deliberately
/// modelling the loop cost instead of the RC curve keeps the round-trip exact and testable;
/// nothing observed so far needs the analog behaviour, and the alternative is a model whose
/// off-by-a-little errors would show up as games feeling subtly wrong rather than failing.
/// </para>
///
/// <para>
/// Not modelled: pot non-linearity, the interrupt sensitivity that makes PREAD unreliable on real
/// hardware with interrupts enabled, and the shared-capacitor coupling between paddles.
/// </para>
/// </summary>
public class Apple2GamePort
{
    /// <summary>$C061-$C063: pushbuttons 0-2, in bit 7.</summary>
    public const ushort Button0Address = 0xC061;

    /// <summary>$C064-$C067: paddle timer outputs, in bit 7.</summary>
    public const ushort Paddle0Address = 0xC064;

    /// <summary>$C070: strobing this restarts every paddle's one-shot.</summary>
    public const ushort TriggerAddress = 0xC070;

    public const int ButtonCount = 3;
    public const int PaddleCount = 4;

    /// <summary>
    /// Neutral paddle position — a centred stick, and what a game reads at rest.
    ///
    /// The true midpoint of 0-255 is 127.5, so this has to round one way or the other, and the
    /// choice is not cosmetic: Choplifter compares against 128 with no dead zone, so at 127 it
    /// reads a centred stick as held left and the helicopter drifts. Verified by instrumenting
    /// what the game actually computes — it read back exactly the value set, so the read model was
    /// right and only the resting value was wrong. Rounding up suits real software better,
    /// because a game that splits the range at 128 puts centre in the upper half.
    /// </summary>
    public const byte PaddleCentre = 128;

    /// <summary>Ends of a paddle's travel, which a digital direction drives it straight to.</summary>
    public const byte PaddleMin = 0;
    public const byte PaddleMax = 255;

    /// <summary>
    /// Cycles per iteration of the ROM's PREAD polling loop
    /// (<c>LDA $C064,X</c> 4, <c>BPL</c> 2, <c>INY</c> 2, <c>BNE</c> 3).
    /// </summary>
    public const int PreadLoopCycles = 11;

    private readonly Func<ulong> _cpuCycleProvider;
    private readonly byte[] _paddlePositions = new byte[PaddleCount];
    private readonly bool[] _buttons = new bool[ButtonCount];
    /// <summary>
    /// When the one-shot was last fired, or null if it never has been. Null rather than 0: at
    /// power-on the CPU cycle counter is also 0, so treating "never triggered" as "triggered at
    /// cycle 0" would make an untouched game port read as though every paddle timer were running.
    /// </summary>
    private ulong? _triggeredAtCycle;

    /// <summary>
    /// How many times software has strobed $C070 to start a paddle read. A climbing count is the
    /// only reliable way to tell whether a game actually uses the stick — the same role
    /// <c>Disk2Controller.DataReadCount</c> plays for the drive.
    /// </summary>
    public ulong PaddleTriggerCount { get; private set; }

    /// <summary>
    /// How many times software has read each pushbutton at $C061-$C063. Per button rather than a
    /// total, because which button a game polls is exactly the question worth asking — a game that
    /// reads button 1 needs it mapped, and an aggregate count hides that.
    /// </summary>
    public readonly ulong[] ButtonReadCounts = new ulong[ButtonCount];

    /// <summary>Reads across all buttons.</summary>
    public ulong ButtonReadCount
    {
        get
        {
            ulong total = 0;
            foreach (var count in ButtonReadCounts)
                total += count;
            return total;
        }
    }

    public Apple2GamePort(Func<ulong> cpuCycleProvider)
    {
        _cpuCycleProvider = cpuCycleProvider;
        for (var paddle = 0; paddle < PaddleCount; paddle++)
            _paddlePositions[paddle] = PaddleCentre;
    }

    /// <summary>Position of a paddle, 0-255. Paddle 0 is a joystick's X axis, paddle 1 its Y.</summary>
    public byte GetPaddlePosition(int paddle) => _paddlePositions[paddle];

    public void SetPaddlePosition(int paddle, byte position)
    {
        ValidatePaddle(paddle);
        _paddlePositions[paddle] = position;
    }

    public bool IsButtonPressed(int button)
    {
        ValidateButton(button);
        return _buttons[button];
    }

    public void SetButton(int button, bool pressed)
    {
        ValidateButton(button);
        _buttons[button] = pressed;
    }

    public void ClearAll()
    {
        for (var paddle = 0; paddle < PaddleCount; paddle++)
            _paddlePositions[paddle] = PaddleCentre;
        Array.Clear(_buttons);
        _triggeredAtCycle = null;
    }

    /// <summary>Strobe of $C070: restarts every paddle's one-shot.</summary>
    public void Trigger()
    {
        _triggeredAtCycle = _cpuCycleProvider();
        PaddleTriggerCount++;
    }

    /// <summary>
    /// Drives the port from a digital joystick. A held direction takes its axis to the end of its
    /// travel and releasing returns it to centre, which is how a switch-type stick behaves on this
    /// analog port — the pots are at their extremes, not at intermediate positions.
    ///
    /// Opposite directions held together cancel to centre: the stick cannot physically be at both
    /// ends of an axis, and centring is what a real one would read as it passes through.
    ///
    /// Both buttons are wired, because games use both: Choplifter fires with button 0 and turns the
    /// helicopter with button 1, and mapping only the first leaves the game half-playable in a way
    /// that looks like a broken joystick rather than a missing mapping.
    /// </summary>
    public void ApplyJoystickActions(IReadOnlySet<JoystickAction> actions)
    {
        SetPaddlePosition(0, AxisPosition(
            actions.Contains(JoystickAction.Left), actions.Contains(JoystickAction.Right)));
        SetPaddlePosition(1, AxisPosition(
            actions.Contains(JoystickAction.Up), actions.Contains(JoystickAction.Down)));
        SetButton(0, actions.Contains(JoystickAction.Fire));
        SetButton(1, actions.Contains(JoystickAction.Fire2));
    }

    private static byte AxisPosition(bool towardsMin, bool towardsMax)
    {
        if (towardsMin == towardsMax)
            return PaddleCentre;   // neither held, or both.
        return towardsMin ? PaddleMin : PaddleMax;
    }

    /// <summary>
    /// Whether a paddle's timer is still running, i.e. what bit 7 of $C064+n reads. Position 0
    /// expires immediately, so PREAD counts zero iterations and reports 0.
    /// </summary>
    public bool IsPaddleTimerRunning(int paddle)
    {
        ValidatePaddle(paddle);
        if (_triggeredAtCycle is not { } triggeredAt)
            return false;

        var elapsed = _cpuCycleProvider() - triggeredAt;
        return elapsed < (ulong)(_paddlePositions[paddle] * PreadLoopCycles);
    }

    /// <summary>
    /// The byte the CPU reads from $C060-$C06F. Bit 7 carries the answer; the remaining bits are
    /// whatever the data bus last held, which no software relies on, so zero is fine.
    /// </summary>
    public byte ReadGamePort(ushort address)
    {
        var offset = address & 0x0F;
        if (offset is >= 1 and <= 3)
            ButtonReadCounts[offset - 1]++;

        return offset switch
        {
            >= 1 and <= 3 => IsButtonPressed(offset - 1) ? (byte)0x80 : (byte)0x00,
            >= 4 and <= 7 => IsPaddleTimerRunning(offset - 4) ? (byte)0x80 : (byte)0x00,
            _ => 0x00,   // $C060 cassette input, and the unassigned addresses above $C067.
        };
    }

    // --- Snapshot support (consumed by the apple2-core snapshot module in the same assembly) ---

    /// <summary>
    /// When the one-shot was last fired, or null if it never has been. Absolute, and meaningful
    /// after a restore because the CPU's cumulative cycle count is restored with it.
    /// </summary>
    internal ulong? SnapshotTriggeredAtCycle => _triggeredAtCycle;

    /// <summary>
    /// Restores the one-shot's firing stamp and the strobe count. Paddle positions and button
    /// states go back through the ordinary public setters — they are plain values with no derived
    /// state behind them.
    /// </summary>
    internal void RestoreSnapshotState(ulong? triggeredAtCycle, ulong paddleTriggerCount)
    {
        _triggeredAtCycle = triggeredAtCycle;
        PaddleTriggerCount = paddleTriggerCount;
    }

    private static void ValidatePaddle(int paddle)
    {
        if (paddle < 0 || paddle >= PaddleCount)
            throw new ArgumentOutOfRangeException(nameof(paddle), paddle, $"Paddle must be 0-{PaddleCount - 1}.");
    }

    private static void ValidateButton(int button)
    {
        if (button < 0 || button >= ButtonCount)
            throw new ArgumentOutOfRangeException(nameof(button), button, $"Button must be 0-{ButtonCount - 1}.");
    }
}
