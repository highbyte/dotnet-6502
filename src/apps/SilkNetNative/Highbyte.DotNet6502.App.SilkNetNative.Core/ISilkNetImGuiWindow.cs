namespace Highbyte.DotNet6502.App.SilkNetNative.Core;

public interface ISilkNetImGuiWindow
{
    public bool Visible { get; }
    public bool WindowIsFocused { get; }
    public void PostOnRender();
}
