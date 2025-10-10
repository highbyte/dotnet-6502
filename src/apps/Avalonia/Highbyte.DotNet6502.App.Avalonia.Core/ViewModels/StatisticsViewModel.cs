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

public class StatisticsViewModel : ViewModelBase, IDisposable
{
    private readonly DispatcherTimer _updateTimer;
    private bool _disposed = false;

    // Dynamic Statistics Collection
    public ObservableCollection<StatisticItem> Statistics { get; } = new ObservableCollection<StatisticItem>();

    public StatisticsViewModel()
    {
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
        if (_disposed || App.HostApp == null)
            return;

        try
        {
            // Get statistics from the host app
            var stats = App.HostApp.GetStats();

            UpdatePerformanceStats(stats);
        }
        catch (Exception)
        {
            // Handle any exceptions gracefully to prevent UI crashes
        }
    }

    private void UpdatePerformanceStats(List<(string name, IStat stat)> stats)
    {
        // Clear existing statistics
        Statistics.Clear();

        // Add new statistics from the host app
        foreach ((string name, IStat stat) in stats.OrderBy(i => i.name))
        {
            if (!stat.ShouldShow())
                continue;

            var description = stat.GetDescription();

            Statistics.Add(new StatisticItem
            {
                Name = name,
                Value = description
            });
        }
    }

    private string GetDisplayName(string statName)
    {
        var displayName = statName;
        // Remove host name prefix if present
        var parts = statName.Split('-');
        if (parts.Length > 1)
        {
            displayName = string.Join(" ", parts.Skip(1));
        }

        // Convert from PascalCase to readable format
        //displayName = System.Text.RegularExpressions.Regex.Replace(displayName, "([a-z])([A-Z])", "$1 $2");
        return displayName;
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
