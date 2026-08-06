using System;
using System.Collections.Generic;
using System.Linq;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

/// <summary>
/// Coordinates the expanded state of a group of collapsible sidebar sections in accordion
/// style: expanding one section collapses the others, while collapsing a section leaves the
/// rest untouched (so all sections can be closed at once).
///
/// A ViewModel keeps its public <c>IsXxxSectionExpanded</c> properties as thin wrappers over
/// <see cref="IsExpanded"/> and passes a callback that raises the matching property-change
/// notification whenever a section's state changes — including sections collapsed as a side
/// effect of another one expanding.
/// </summary>
/// <typeparam name="TSection">Enum identifying the sections of the group.</typeparam>
public sealed class AccordionSections<TSection> where TSection : struct, Enum
{
    private readonly Dictionary<TSection, bool> _expanded = new();
    private readonly Action<TSection> _sectionStateChanged;

    /// <param name="sectionStateChanged">
    /// Called once per section whose state changed, so the owner can raise property-change
    /// notifications.
    /// </param>
    /// <param name="initiallyExpanded">The section that starts expanded, if any.</param>
    public AccordionSections(Action<TSection> sectionStateChanged, TSection? initiallyExpanded = null)
    {
        _sectionStateChanged = sectionStateChanged;
        foreach (var section in Enum.GetValues<TSection>())
            _expanded[section] = initiallyExpanded.HasValue
                && EqualityComparer<TSection>.Default.Equals(section, initiallyExpanded.Value);
    }

    public bool IsExpanded(TSection section) => _expanded[section];

    /// <summary>Toggles a section; expanding it collapses all others.</summary>
    public void Toggle(TSection section) => SetExpanded(section, !_expanded[section]);

    /// <summary>Sets a section's state; expanding it collapses all others.</summary>
    public void SetExpanded(TSection section, bool expanded)
    {
        if (expanded)
        {
            foreach (var other in _expanded.Keys.ToList())
            {
                if (EqualityComparer<TSection>.Default.Equals(other, section) || !_expanded[other])
                    continue;
                _expanded[other] = false;
                _sectionStateChanged(other);
            }
        }

        if (_expanded[section] == expanded)
            return;
        _expanded[section] = expanded;
        _sectionStateChanged(section);
    }
}
