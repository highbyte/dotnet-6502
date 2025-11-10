namespace Highbyte.DotNet6502.Impl.Avalonia.Monitor;

public class StatusLineEntry
{
    public List<StatusItem> Items { get; private set; } = new();

    public void AddItem(string key, object value)
    {
        Items.Add(new StatusItem(key, value));
    }

    public override string ToString()
    {
        return string.Join(", ", Items.Select(item => $"{item.Key}: {item.Value}"));
    }
}
