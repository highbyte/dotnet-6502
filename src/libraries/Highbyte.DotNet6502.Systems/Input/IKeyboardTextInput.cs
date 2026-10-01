namespace Highbyte.DotNet6502.Systems.Input;

/// <summary>
/// Optional text-to-keyboard capability of an <see cref="IInputInjector"/>.
/// Characters are translated to machine key chords, not inserted into ROM input buffers.
/// </summary>
public interface IKeyboardTextInput
{
    bool CanPressCharacter(char character);

    /// <summary>Presses the character's key chord for the current frame only.</summary>
    void PressCharacter(char character);
}
