namespace Highbyte.DotNet6502;

/// <summary>
/// The CPU's IRQ and NMI input lines.
///
/// Line state is held as bitmasks, one bit per registered source, so that the CPU's interrupt
/// sampling is a couple of integer tests with no collection walking, no LINQ and no allocation.
/// That matters today on the per-instruction path and is a prerequisite for sampling the lines
/// per cycle.
///
/// Devices identify themselves by a source name. A name is registered on first use and mapped to
/// one bit (see <see cref="GetSource(string)"/>); the string-based API remains for wiring code that
/// runs on device events, while devices may hold the <see cref="InterruptSource"/> handle and use
/// the handle-based overloads instead.
///
/// Line semantics:
/// <list type="bullet">
/// <item>IRQ is level-triggered: the line is asserted while any source is active. A source raised
/// with <c>autoAcknowledge</c> is dropped by the CPU when it services the IRQ; every other source
/// stays asserted until its device clears it.</item>
/// <item>NMI is edge-triggered: a source transitioning from inactive to active latches
/// <see cref="NMIPending"/> until the CPU services it. Keeping a source active does not retrigger
/// NMI until it has been cleared and asserted again.</item>
/// </list>
/// </summary>
public sealed class CPUInterrupts
{
    /// <summary>Maximum number of distinct sources one CPU can register (one bit each).</summary>
    public const int MaxSources = 64;

    // Hot-path state. Invariant: _irqAutoAcknowledgeMask is a subset of _irqLines.
    private ulong _irqLines;
    private ulong _irqAutoAcknowledgeMask;
    private ulong _nmiLines;

    // Registry. Only touched when a device wires itself up or uses the string overloads.
    private readonly Dictionary<string, InterruptSource> _sourcesByName = new(StringComparer.Ordinal);
    private readonly List<string> _sourceNames = new();

    /// <summary>True while at least one IRQ source is active (the IRQ line is asserted).</summary>
    public bool IRQLineEnabled => _irqLines != 0;

    /// <summary>True while at least one NMI source is active (the NMI line is asserted).</summary>
    public bool NMILineEnabled => _nmiLines != 0;

    /// <summary>Latched NMI edge waiting to be serviced by the CPU.</summary>
    public bool NMIPending { get; private set; }

    /// <summary>
    /// The CPU bus cycle (<see cref="CPU.BusCycles"/>, the 1-based number of the access in
    /// progress) during which the IRQ line went active, as reported by the device that asserted
    /// it. 0 when the device gave no cycle, which the CPU treats as "before its last poll", so the
    /// interrupt is taken at the next instruction boundary as before. See
    /// <see cref="CPU.ProcessPendingInterrupts"/> for the sampling rule.
    /// </summary>
    public ulong IRQAssertedAtBusCycle { get; private set; }

    /// <summary>The bus cycle during which the pending NMI edge was detected; 0 if not given.</summary>
    public ulong NMIPendingAtBusCycle { get; private set; }

    /// <summary>Number of sources registered on this instance.</summary>
    public int RegisteredSourceCount => _sourceNames.Count;

    /// <summary>
    /// Returns the handle for a source name, registering the name on first use.
    /// </summary>
    /// <exception cref="InvalidOperationException">More than <see cref="MaxSources"/> distinct names were registered.</exception>
    public InterruptSource GetSource(string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        if (_sourcesByName.TryGetValue(source, out var existing))
            return existing;

        if (_sourceNames.Count >= MaxSources)
            throw new InvalidOperationException($"Cannot register interrupt source '{source}': at most {MaxSources} distinct sources are supported.");

        var handle = new InterruptSource(_sourceNames.Count, source);
        _sourceNames.Add(source);
        _sourcesByName.Add(source, handle);
        return handle;
    }

    /// <summary>Looks up a registered source without registering it.</summary>
    public bool TryGetSource(string source, out InterruptSource handle)
        => _sourcesByName.TryGetValue(source, out handle);

    // ----- IRQ -----

    /// <summary>
    /// Sets an IRQ source active. If the source is already active its acknowledge mode is left unchanged.
    /// </summary>
    /// <param name="source">Unique name of source</param>
    /// <param name="autoAcknowledge">Set to true if the IRQ source should automatically be removed when processed by CPU.</param>
    public void SetIRQSourceActive(string source, bool autoAcknowledge)
        => SetIRQActive(GetSource(source), autoAcknowledge, assertedAtBusCycle: 0);

    /// <summary>
    /// Sets an IRQ source active and records the bus cycle during which the line went active,
    /// so the CPU can apply its end-of-instruction sampling rule (an interrupt asserted during an
    /// instruction's last cycle is taken after the following instruction).
    /// </summary>
    /// <param name="assertedAtBusCycle">The <see cref="CPU.BusCycles"/> value during the access
    /// on which the device asserted the line; a device catching up at an instruction boundary
    /// passes the cycle at which the condition arose.</param>
    public void SetIRQSourceActive(string source, bool autoAcknowledge, ulong assertedAtBusCycle)
        => SetIRQActive(GetSource(source), autoAcknowledge, assertedAtBusCycle);

    /// <inheritdoc cref="SetIRQSourceActive(string, bool)"/>
    public void SetIRQActive(InterruptSource source, bool autoAcknowledge)
        => SetIRQActive(source, autoAcknowledge, assertedAtBusCycle: 0);

    /// <inheritdoc cref="SetIRQSourceActive(string, bool, ulong)"/>
    public void SetIRQActive(InterruptSource source, bool autoAcknowledge, ulong assertedAtBusCycle)
    {
        var mask = source.Mask;
        if ((_irqLines & mask) != 0)
            return;

        // The line is level-sensitive: its assertion cycle is that of the first source to pull
        // it low. A source added while the line is already low does not move it.
        if (_irqLines == 0)
            IRQAssertedAtBusCycle = assertedAtBusCycle;
        _irqLines |= mask;
        if (autoAcknowledge)
            _irqAutoAcknowledgeMask |= mask;
        else
            _irqAutoAcknowledgeMask &= ~mask;
    }

    /// <summary>
    /// Removes an IRQ source.
    /// This is typically done by IRQ sources that need to be manually acknowledged by the IRQ handler.
    /// Unknown names are ignored.
    /// </summary>
    /// <param name="source">Unique name of source</param>
    public void SetIRQSourceInactive(string source)
    {
        if (TryGetSource(source, out var handle))
            SetIRQInactive(handle);
    }

    /// <inheritdoc cref="SetIRQSourceInactive(string)"/>
    public void SetIRQInactive(InterruptSource source)
    {
        var mask = source.Mask;
        _irqLines &= ~mask;
        _irqAutoAcknowledgeMask &= ~mask;
    }

    /// <summary>Returns true if the specified IRQ source is currently active.</summary>
    public bool IsIRQSourceActive(string source)
        => TryGetSource(source, out var handle) && IsIRQActive(handle);

    /// <inheritdoc cref="IsIRQSourceActive(string)"/>
    public bool IsIRQActive(InterruptSource source) => (_irqLines & source.Mask) != 0;

    /// <summary>Returns true if the source is active and will be dropped when the CPU services the IRQ.</summary>
    public bool IsIRQAutoAcknowledged(InterruptSource source) => (_irqAutoAcknowledgeMask & source.Mask) != 0;

    /// <summary>
    /// Drops every auto-acknowledging IRQ source. The CPU calls this when it services an IRQ;
    /// manually acknowledged sources keep the line asserted until their device clears them.
    /// </summary>
    public void AcknowledgeAutoAcknowledgingIRQSources()
    {
        _irqLines &= ~_irqAutoAcknowledgeMask;
        _irqAutoAcknowledgeMask = 0;
    }

    // ----- NMI -----

    /// <summary>
    /// Sets an NMI source active. Latches a pending NMI only on the inactive-to-active transition.
    /// </summary>
    /// <param name="source">Unique name of source</param>
    public void SetNMISourceActive(string source)
        => SetNMIActive(GetSource(source), pendingAtBusCycle: 0);

    /// <summary>
    /// Sets an NMI source active and records the bus cycle during which the edge occurred (see
    /// <see cref="SetIRQSourceActive(string, bool, ulong)"/> for the sampling rule).
    /// </summary>
    public void SetNMISourceActive(string source, ulong pendingAtBusCycle)
        => SetNMIActive(GetSource(source), pendingAtBusCycle);

    /// <inheritdoc cref="SetNMISourceActive(string)"/>
    public void SetNMIActive(InterruptSource source)
        => SetNMIActive(source, pendingAtBusCycle: 0);

    /// <inheritdoc cref="SetNMISourceActive(string, ulong)"/>
    public void SetNMIActive(InterruptSource source, ulong pendingAtBusCycle)
    {
        var mask = source.Mask;
        if ((_nmiLines & mask) != 0)
            return;

        _nmiLines |= mask;
        if (!NMIPending)
            NMIPendingAtBusCycle = pendingAtBusCycle;
        NMIPending = true;
    }

    /// <summary>Removes an NMI source. Unknown names are ignored.</summary>
    /// <param name="source">Unique name of source</param>
    public void SetNMISourceInactive(string source)
    {
        if (TryGetSource(source, out var handle))
            SetNMIInactive(handle);
    }

    /// <inheritdoc cref="SetNMISourceInactive(string)"/>
    public void SetNMIInactive(InterruptSource source) => _nmiLines &= ~source.Mask;

    /// <summary>Returns true if the specified NMI source is currently active.</summary>
    public bool IsNMISourceActive(string source)
        => TryGetSource(source, out var handle) && IsNMIActive(handle);

    /// <inheritdoc cref="IsNMISourceActive(string)"/>
    public bool IsNMIActive(InterruptSource source) => (_nmiLines & source.Mask) != 0;

    /// <summary>Clears the latched NMI edge. The CPU calls this when it services the NMI.</summary>
    public void ClearPendingNMI()
    {
        NMIPending = false;
    }

    // ----- Diagnostics and snapshots (not for hot paths: enumeration allocates an iterator) -----

    /// <summary>Active IRQ sources by name, with their auto-acknowledge mode.</summary>
    public IEnumerable<KeyValuePair<string, bool>> ActiveIRQSources
    {
        get
        {
            for (var i = 0; i < _sourceNames.Count; i++)
            {
                var mask = 1UL << i;
                if ((_irqLines & mask) != 0)
                    yield return new KeyValuePair<string, bool>(_sourceNames[i], (_irqAutoAcknowledgeMask & mask) != 0);
            }
        }
    }

    /// <summary>Active NMI sources by name.</summary>
    public IEnumerable<string> ActiveNMISources
    {
        get
        {
            for (var i = 0; i < _sourceNames.Count; i++)
            {
                if ((_nmiLines & (1UL << i)) != 0)
                    yield return _sourceNames[i];
            }
        }
    }

    /// <summary>
    /// Deasserts both lines, drops all acknowledge flags and clears the pending NMI latch.
    /// Registered source names (and their bit positions) are kept so handles held by devices stay valid.
    /// </summary>
    public void ClearAll()
    {
        _irqLines = 0;
        _irqAutoAcknowledgeMask = 0;
        _nmiLines = 0;
        NMIPending = false;
    }
}
