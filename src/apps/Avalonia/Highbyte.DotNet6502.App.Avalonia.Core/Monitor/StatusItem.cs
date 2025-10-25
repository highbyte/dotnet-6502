namespace Highbyte.DotNet6502.App.Avalonia.Core.Monitor;

/// <summary>
/// Represents a single status item (key-value pair) for display in the monitor status
/// </summary>
public class StatusItem
{
    public string Key { get; set; }
    public object Value { get; set; }

    public StatusItem(string key, object value)
    {
        Key = key;
        Value = value;
    }
}
