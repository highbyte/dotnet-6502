namespace Highbyte.DotNet6502.Systems.Input;

/// <summary>
/// A direction or button press on a host joystick, independent of the machine it drives. More
/// than one can be active at a time (diagonals, or a direction plus fire).
///
/// This is the seam between input acquisition and hardware: hosts and mapping tables deal in
/// these, and each system decides what they mean electrically — the C64 pulls CIA port bits low,
/// while the Apple II has no digital joystick at all and turns them into analog paddle positions.
///
/// Not every system has every action. An Apple II stick has two buttons and games do use both
/// (Choplifter turns the helicopter with the second), whereas a C64 joystick has one — so a system
/// asked for an action it has no wiring for should say so rather than guess.
///
/// The numeric values carry no meaning. A system that needs a particular bit order must map to it
/// explicitly rather than casting, so reordering or extending this enum cannot silently change
/// what a machine reads.
/// </summary>
public enum JoystickAction
{
    Up,
    Down,
    Left,
    Right,
    Fire,

    /// <summary>
    /// The second button, which the Apple II game port has and a C64 joystick does not.
    /// </summary>
    Fire2,
}
