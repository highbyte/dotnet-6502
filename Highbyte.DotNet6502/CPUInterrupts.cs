namespace Highbyte.DotNet6502;

public class CPUInterrupts
{
    public bool IRQLineEnabled => ActiveIRQSources.Count() > 0;

    public bool NMILineEnabled => ActiveNMISources.Count() > 0;

    public Dictionary<string, bool> ActiveIRQSources { get; private set; } = new();
    public HashSet<string> ActiveNMISources { get; private set; } = new();

    /// <summary>
    /// Sets an IRQ source active.
    /// </summary>
    /// <param name="source">Unique name of source</param>
    /// <param name="autoAcknowledge">Set to true if the IRQ source should automatically be removed when processed by CPU.</param>
    public void SetIRQSourceActive(string source, bool autoAcknowledge)
    {
        if (!ActiveIRQSources.ContainsKey(source))
            ActiveIRQSources.Add(source, autoAcknowledge);
    }

    /// <summary>
    /// Removes an IRQ source.
    /// This is typically done by IRQ sources that needs to be manually acknowledged by IRQ handler.
    /// </summary>
    /// <param name="source">Unique name of source</param>
    public void SetIRQSourceInactive(string source)
    {
        if (ActiveIRQSources.ContainsKey(source))
            ActiveIRQSources.Remove(source);
    }

    /// <summary>
    /// Returns true if the specified IRQ source is currently active.
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public bool IsIRQSourceActive(string source)
    {
        return ActiveIRQSources.ContainsKey(source);
    }

    /// <summary>
    /// Sets an NMI source active.
    /// </summary>
    /// <param name="source">Unique name of source</param>
    public void SetNMISourceActive(string source)
    {
        if (!ActiveNMISources.Contains(source))
            ActiveNMISources.Add(source);
    }

    /// <summary>
    /// Removes an NMI source. 
    /// </summary>
    /// <param name="source">Unique name of source</param>
    public void SetNMISourceInactive(string source)
    {
        if (ActiveNMISources.Contains(source))
            ActiveNMISources.Remove(source);
    }

    /// <summary>
    /// Retyrns true if the specified NMI source is currently active.   
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public bool IsNMISourceActive(string source)
    {
        return ActiveNMISources.Contains(source);
    }
}
