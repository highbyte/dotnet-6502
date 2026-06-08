using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class StatisticItem
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    // UI-only display order within its group. Lower is shown first; ties fall back to Name.
    public int SortOrder { get; set; } = int.MaxValue;
}

public class StatisticSection
{
    public string Title { get; set; } = string.Empty;
    public ObservableCollection<StatisticItem> Items { get; } = new();

    // UI-only display order of the group. Lower is shown first; ties fall back to Title.
    public int SortOrder { get; set; } = int.MaxValue;
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
        "Main",
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
        ["Main"] = new[] { "SystemTime", "InputTime", "ScriptTime", "OnUpdateFPS" },
        ["Render"] = new[] { "FPS", "FlushIfDirty" },
        ["Audio"] = new[] { "CommandsPerSecond", "Execute", "BufferFill", "Underruns", "Overruns" },
    };

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


    private void UpdateStats(object? sender, EventArgs e)
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

        foreach ((string name, IStat stat) in stats)
        {
            if (!stat.ShouldShow())
                continue;

            var description = stat.GetDescription();
            var nameParts = name.Split('-', StringSplitOptions.RemoveEmptyEntries);

            var sectionName = nameParts.Length > 1
                ? string.Join('-', nameParts[..^1])
                : "Main";

            var displayName = nameParts.Length > 1
                ? nameParts[^1]
                : nameParts[0];

            if (!groupedStats.TryGetValue(sectionName, out var items))
            {
                items = new List<StatisticItem>();
                groupedStats[sectionName] = items;
            }

            items.Add(new StatisticItem
            {
                Name = displayName,
                Value = description,
                SortOrder = StatSortOrderOf(sectionName, displayName)
            });
        }

        // Sort groups by their UI sort order, then alphabetically for any not explicitly ordered.
        var orderedGroups = groupedStats
            .OrderBy(kvp => GroupSortOrderOf(kvp.Key))
            .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in orderedGroups)
        {
            var section = new StatisticSection
            {
                Title = group.Key,
                SortOrder = GroupSortOrderOf(group.Key)
            };

            // Sort stats within the group by their UI sort order, then alphabetically.
            foreach (var item in group.Value
                         .OrderBy(i => i.SortOrder)
                         .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
            {
                section.Items.Add(item);
            }

            Sections.Add(section);
        }
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
        // Widest stat name across all sections, so every row pads to the same width.
        // Padding all names to the same width means the tab always starts at the same
        // column, which makes the value column align in monospaced text too.
        var nameWidth = 0;
        foreach (var section in Sections)
            foreach (var item in section.Items)
                nameWidth = Math.Max(nameWidth, item.Name.Length);

        var sb = new System.Text.StringBuilder();
        foreach (var section in Sections)
        {
            sb.AppendLine($"{section.Title}:");
            foreach (var item in section.Items)
                sb.AppendLine($"  {item.Name.PadRight(nameWidth)}\t{item.Value}");
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

            // Refresh the displayed values immediately instead of waiting for the next timer tick.
            UpdateStats(this, EventArgs.Empty);
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
