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
}

public class StatisticSection
{
    public string Title { get; set; } = string.Empty;
    public ObservableCollection<StatisticItem> Items { get; } = new();
}

public class StatisticsViewModel : ViewModelBase, IDisposable
{
    private readonly DispatcherTimer _updateTimer;
    private readonly AvaloniaHostApp _hostApp;
    private bool _disposed = false;

    // Dynamic Statistics Collection grouped by section
    public ObservableCollection<StatisticSection> Sections { get; } = new();

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

        foreach ((string name, IStat stat) in stats.OrderBy(i => i.name, StringComparer.OrdinalIgnoreCase))
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
                Value = description
            });
        }

        foreach (var section in groupedStats
                     .OrderBy(kvp => kvp.Key.Equals("Root", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                     .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
        {
            var statisticSection = new StatisticSection
            {
                Title = section.Key
            };

            foreach (var item in section.Value
                         .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
            {
                statisticSection.Items.Add(item);
            }

            Sections.Add(statisticSection);
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
