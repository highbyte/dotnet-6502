using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Threading;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class StatisticItem : INotifyPropertyChanged
{
    public string Name { get; set; } = string.Empty;

    // Value changes every refresh; raise change notification so it can be updated in place
    // without recreating the item (recreating would tear down the hovered tooltip).
    private string _value = string.Empty;
    public string Value
    {
        get => _value;
        set
        {
            if (_value == value)
                return;
            _value = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }

    // UI-only display order within its group. Lower is shown first; ties fall back to Name.
    public int SortOrder { get; set; } = int.MaxValue;

    // Optional hover tooltip. Null = no tooltip.
    public string? Tooltip { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class StatisticSection
{
    public string Title { get; set; } = string.Empty;
    public ObservableCollection<StatisticItem> Items { get; } = new();

    // UI-only display order of the group. Lower is shown first; ties fall back to Title.
    public int SortOrder { get; set; } = int.MaxValue;

    // Optional hover tooltip shown on the group title. Null = no tooltip.
    public string? Tooltip { get; set; }

    // UI-only nesting depth (0 = top-level group, 1 = sub-group shown under its parent).
    public int IndentLevel { get; set; }

    // Layout margin applied in the view: left indent for nested sub-groups, plus a bit of top
    // spacing before each top-level group (nested children hug their parent, so no top space).
    public Thickness GroupMargin => new(IndentLevel * 12, IndentLevel == 0 ? 6 : 0, 0, 0);
}

public class StatisticsViewModel : ViewModelBase, IDisposable
{
    private readonly DispatcherTimer _updateTimer;
    private readonly AvaloniaHostApp _hostApp;
    private bool _disposed = false;

    // Dynamic Statistics Collection grouped by section
    public ObservableCollection<StatisticSection> Sections { get; } = new();

    // ----------------------------------------------------------------------------------
    // UI-only display ordering.
    // Groups are shown in the order listed here; stat names within a group are shown in
    // the order listed in StatOrder. Anything not listed is appended after the listed
    // ones, alphabetically. Edit these lists to change the display order.
    // ----------------------------------------------------------------------------------
    private static readonly string[] GroupOrder =
    {
        "General",
        "SystemTime",
        "SystemTime-Custom",
        "SystemTime-RenderProvider",
        "SystemTime-AudioProvider",
        "Render",
        "Render-Target",
        "Audio",
        "Audio-Target"
    };

    private static readonly Dictionary<string, string[]> StatOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["General"] = new[] { "InputTime", "ScriptTime", "OnUpdateFPS" },
        ["SystemTime"] = new[] { "Total", "Other" },
        ["Render"] = new[] { "FPS", "FlushIfDirty" },
        ["Audio"] = new[] { "CommandsPerSecond", "Execute", "BufferFill", "Underruns", "Overruns" },
    };

    // ----------------------------------------------------------------------------------
    // UI-only group hierarchy: child group -> parent group. Child groups are rendered
    // nested (indented) directly beneath their parent. Only true time-containment
    // relationships are listed (a child's time is part of its parent's time). The
    // unaccounted remainder of a parent is shown as an "Other" (self) row where derivable.
    // ----------------------------------------------------------------------------------
    private static readonly Dictionary<string, string> GroupParent = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SystemTime-AudioProvider"] = "SystemTime",
        ["SystemTime-RenderProvider"] = "SystemTime",
        ["SystemTime-Custom"] = "SystemTime",
        ["Render-Target"] = "Render",
    };

    // ----------------------------------------------------------------------------------
    // UI-only optional tooltips. GroupTooltips is keyed by group name; StatTooltips is keyed
    // by "<group>/<statName>". Anything not listed simply has no tooltip. Add entries here.
    // ----------------------------------------------------------------------------------
    private static readonly Dictionary<string, string> GroupTooltips = new(StringComparer.OrdinalIgnoreCase)
    {
        ["General"] = "Top-level per-frame timings of the host emulator loop itself — e.g. input handling, scripting hooks, and the update-loop frame rate (OnUpdateFPS).",
        ["SystemTime"] = "Total time to emulate one frame of the system core — CPU plus on-chip hardware (video, audio, etc.). The sub-groups below are parts of this; host-side rendering, audio output and input are timed separately.",
        ["SystemTime-Custom"] = "System-specific extra per-frame work that isn't core CPU or a provider — e.g. sprite collision detection.",
        ["SystemTime-RenderProvider"] = "In-emulation generation of the video output data (per-instruction and end-of-frame hooks). This produces the frame; the host-side Render group draws it.",
        ["SystemTime-AudioProvider"] = "In-emulation generation of the audio data (per-instruction and end-of-frame hooks). This produces the samples/commands; the host-side Audio group plays them.",
        ["Render"] = "Host-side rendering of the emulated frame to the screen. FPS is the render rate; FlushIfDirty is the time spent pushing a frame.",
        ["Render-Target"] = "The host render target backend — time spent presenting the drawn frame (part of the Render group's flush).",
        ["Audio"] = "Host-side audio coordination — forwarding emulated audio to the output backend (command throughput/apply time, or sample ring-buffer health).",
        ["Audio-Target"] = "The host audio output backend — how often it pulls samples and how long those reads take.",
    };

    private static readonly Dictionary<string, string> StatTooltips = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SystemTime/Other"] = "Mainly the core CPU emulation, plus any other system-specific per-frame work (e.g. video/audio chip emulation) not broken out into the sub-groups below.",
    };

    private static string? GroupTooltipOf(string group) =>
        GroupTooltips.TryGetValue(group, out var tip) ? tip : null;

    private static string? StatTooltipOf(string group, string name) =>
        StatTooltips.TryGetValue($"{group}/{name}", out var tip) ? tip : null;

    // Matches the millisecond formatting of ElapsedMillisecondsTimedStat.GetDescription().
    private static string FormatMs(double ms) =>
        ms < 0.01 ? "< 0.01ms" : Math.Round(ms, 2).ToString("0.00") + "ms";

    // Short title for a nested sub-group, e.g. "SystemTime-AudioProvider" -> "AudioProvider".
    private static string ShortTitle(string group)
    {
        var idx = group.LastIndexOf('-');
        return idx >= 0 && idx < group.Length - 1 ? group[(idx + 1)..] : group;
    }

    private static int GroupSortOrderOf(string group)
    {
        var index = Array.FindIndex(GroupOrder, g => string.Equals(g, group, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : int.MaxValue;
    }

    private static int StatSortOrderOf(string group, string name)
    {
        if (StatOrder.TryGetValue(group, out var names))
        {
            var index = Array.FindIndex(names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                return index;
        }
        return int.MaxValue;
    }

    public StatisticsViewModel(AvaloniaHostApp hostApp)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));

        // Create a timer that updates every second
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += UpdateStats;
        _updateTimer.Start();
    }


    /// <summary>
    /// When true the periodic refresh is suspended. The view sets this while the pointer is over
    /// the stats panel so values (and the layout) don't change under the cursor — which would
    /// otherwise dismiss a tooltip the user is reading.
    /// </summary>
    public bool UpdatesPaused { get; set; }

    private void UpdateStats(object? sender, EventArgs e)
    {
        if (UpdatesPaused)
            return;

        RefreshStats();
    }

    private void RefreshStats()
    {
        if (_disposed || _hostApp == null)
            return;

        try
        {
            // Get statistics from the host app
            var stats = _hostApp.GetStats();

            UpdatePerformanceStats(stats);
        }
        catch (Exception)
        {
            // Handle any exceptions gracefully to prevent UI crashes
        }
    }

    private void UpdatePerformanceStats(List<(string name, IStat stat)> stats)
    {
        Sections.Clear();

        var groupedStats = new Dictionary<string, List<StatisticItem>>(StringComparer.OrdinalIgnoreCase);

        // Numeric accumulation (in milliseconds) used to derive the SystemTime "Other" (self) row:
        // self = SystemTime total - sum of its measured children.
        double? systemTimeTotalMs = null;
        double systemTimeChildrenMs = 0;
        var anySystemTimeChild = false;

        foreach ((string name, IStat stat) in stats)
        {
            if (!stat.ShouldShow())
                continue;

            var description = stat.GetDescription();
            var nameParts = name.Split('-', StringSplitOptions.RemoveEmptyEntries);

            var sectionName = nameParts.Length > 1
                ? string.Join('-', nameParts[..^1])
                : "General";

            var displayName = nameParts.Length > 1
                ? nameParts[^1]
                : nameParts[0];

            // Accumulate millisecond totals for the SystemTime hierarchy.
            if (stat is ElapsedMillisecondsTimedStat ems && ems.GetStatMilliseconds() is { } ms)
            {
                if (sectionName.Equals("General", StringComparison.OrdinalIgnoreCase)
                    && displayName.Equals("SystemTime", StringComparison.OrdinalIgnoreCase))
                {
                    systemTimeTotalMs = ms;
                }
                else if (sectionName.StartsWith("SystemTime-", StringComparison.OrdinalIgnoreCase))
                {
                    systemTimeChildrenMs += ms;
                    anySystemTimeChild = true;
                }
            }

            if (!groupedStats.TryGetValue(sectionName, out var items))
            {
                items = new List<StatisticItem>();
                groupedStats[sectionName] = items;
            }

            items.Add(new StatisticItem
            {
                Name = displayName,
                Value = description,
                SortOrder = StatSortOrderOf(sectionName, displayName),
                Tooltip = StatTooltipOf(sectionName, displayName)
            });
        }

        PromoteSystemTimeGroup(groupedStats, systemTimeTotalMs, systemTimeChildrenMs, anySystemTimeChild);

        // Build a section per group, with its items sorted by UI order then alphabetically.
        var sectionByGroup = new Dictionary<string, StatisticSection>(StringComparer.OrdinalIgnoreCase);
        foreach (var (groupName, items) in groupedStats)
        {
            var section = new StatisticSection
            {
                Title = groupName,
                SortOrder = GroupSortOrderOf(groupName),
                Tooltip = GroupTooltipOf(groupName)
            };
            foreach (var item in items
                         .OrderBy(i => i.SortOrder)
                         .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
            {
                section.Items.Add(item);
            }
            sectionByGroup[groupName] = section;
        }

        // A group is top-level unless it declares a parent that is actually present.
        bool IsTopLevel(string group) =>
            !GroupParent.TryGetValue(group, out var parent) || !sectionByGroup.ContainsKey(parent);

        var topLevel = sectionByGroup.Keys
            .Where(IsTopLevel)
            .OrderBy(GroupSortOrderOf)
            .ThenBy(g => g, StringComparer.OrdinalIgnoreCase);

        var newSections = new List<StatisticSection>();
        foreach (var groupName in topLevel)
        {
            newSections.Add(sectionByGroup[groupName]);

            // Emit child groups nested (indented) directly under their parent.
            var children = sectionByGroup.Keys
                .Where(c => GroupParent.TryGetValue(c, out var p)
                            && sectionByGroup.ContainsKey(p)
                            && p.Equals(groupName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(GroupSortOrderOf)
                .ThenBy(c => c, StringComparer.OrdinalIgnoreCase);

            foreach (var childName in children)
            {
                var child = sectionByGroup[childName];
                child.IndentLevel = 1;
                child.Title = ShortTitle(childName);
                newSections.Add(child);
            }
        }

        ApplySections(newSections);
    }

    /// <summary>
    /// Applies the freshly built sections to the bound <see cref="Sections"/> collection. When the
    /// structure (groups and stat names) is unchanged — the normal case while running — only the
    /// stat values are updated in place. This avoids recreating the visual elements every refresh,
    /// which would otherwise dismiss a tooltip the user is hovering. The collection is rebuilt only
    /// when the structure actually changes (e.g. a system starts/stops, audio mode switches).
    /// </summary>
    private void ApplySections(List<StatisticSection> newSections)
    {
        if (HasSameStructure(newSections))
        {
            for (var s = 0; s < newSections.Count; s++)
            {
                var existingItems = Sections[s].Items;
                var incomingItems = newSections[s].Items;
                for (var i = 0; i < incomingItems.Count; i++)
                    existingItems[i].Value = incomingItems[i].Value;
            }
            return;
        }

        Sections.Clear();
        foreach (var section in newSections)
            Sections.Add(section);
    }

    private bool HasSameStructure(List<StatisticSection> incoming)
    {
        if (Sections.Count != incoming.Count)
            return false;

        for (var s = 0; s < incoming.Count; s++)
        {
            var current = Sections[s];
            var next = incoming[s];

            if (current.IndentLevel != next.IndentLevel
                || !string.Equals(current.Title, next.Title, StringComparison.Ordinal)
                || current.Items.Count != next.Items.Count)
            {
                return false;
            }

            for (var i = 0; i < next.Items.Count; i++)
            {
                if (!string.Equals(current.Items[i].Name, next.Items[i].Name, StringComparison.Ordinal))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Moves the "SystemTime" total out of the General group into its own parent group (so its
    /// sub-groups can nest beneath it) and appends an "Other" (self) row = total - sum(children),
    /// making the breakdown account for the otherwise-invisible core CPU/chip emulation time.
    /// </summary>
    private static void PromoteSystemTimeGroup(
        Dictionary<string, List<StatisticItem>> groupedStats,
        double? systemTimeTotalMs,
        double systemTimeChildrenMs,
        bool anySystemTimeChild)
    {
        if (!groupedStats.TryGetValue("General", out var mainItems))
            return;

        var systemTimeItem = mainItems.FirstOrDefault(i =>
            i.Name.Equals("SystemTime", StringComparison.OrdinalIgnoreCase));
        if (systemTimeItem == null)
            return;

        mainItems.Remove(systemTimeItem);

        var systemTimeGroup = new List<StatisticItem>
        {
            new()
            {
                Name = "Total",
                Value = systemTimeItem.Value,
                SortOrder = StatSortOrderOf("SystemTime", "Total"),
                Tooltip = StatTooltipOf("SystemTime", "Total")
            }
        };

        // Derived self/Other time (only when we have both a total and at least one measured child).
        if (systemTimeTotalMs.HasValue && anySystemTimeChild)
        {
            var selfMs = Math.Max(0, systemTimeTotalMs.Value - systemTimeChildrenMs);
            systemTimeGroup.Add(new StatisticItem
            {
                Name = "Other",
                Value = FormatMs(selfMs),
                SortOrder = StatSortOrderOf("SystemTime", "Other"),
                Tooltip = StatTooltipOf("SystemTime", "Other")
            });
        }

        groupedStats["SystemTime"] = systemTimeGroup;
    }

    /// <summary>
    /// Builds a plain-text representation of the currently displayed statistics,
    /// suitable for copying to the clipboard.
    /// Each stat name is padded to a fixed width so the value column lines up in a
    /// monospaced text editor, while a single tab before the value keeps the output
    /// tab-delimited (two columns) for pasting/importing into a spreadsheet.
    /// </summary>
    public string GetStatsText()
    {
        // Widest "left field" (group indent + 2-space item indent + name) across all rows.
        // Padding every row's left field to this width keeps the tab in the same column, so the
        // value column aligns in monospaced text while the indentation still shows the hierarchy.
        var leftWidth = 0;
        foreach (var section in Sections)
            foreach (var item in section.Items)
                leftWidth = Math.Max(leftWidth, section.IndentLevel * 2 + 2 + item.Name.Length);

        var sb = new System.Text.StringBuilder();
        foreach (var section in Sections)
        {
            var indent = new string(' ', section.IndentLevel * 2);
            sb.AppendLine($"{indent}{section.Title}:");
            foreach (var item in section.Items)
            {
                var left = $"{indent}  {item.Name}";
                sb.AppendLine($"{left.PadRight(leftWidth)}\t{item.Value}");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Clears the accumulated/averaged values of all stats so they start fresh.
    /// </summary>
    public void ResetStats()
    {
        if (_disposed || _hostApp == null)
            return;

        try
        {
            foreach ((_, IStat stat) in _hostApp.GetStats())
                stat.ResetAverage();

            // Refresh the displayed values immediately (bypassing any hover-pause) instead of
            // waiting for the next timer tick.
            RefreshStats();
        }
        catch (Exception)
        {
            // Handle any exceptions gracefully to prevent UI crashes
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _updateTimer?.Stop();
            _disposed = true;
        }
    }
}
