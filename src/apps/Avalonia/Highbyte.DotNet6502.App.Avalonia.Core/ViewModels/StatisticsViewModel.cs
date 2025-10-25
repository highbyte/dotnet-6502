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

    public void Dispose()
    {
        if (!_disposed)
        {
            _updateTimer?.Stop();
            _disposed = true;
        }
    }
}
