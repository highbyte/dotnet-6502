using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Highbyte.DotNet6502.App.Avalonia.Core.Monitor;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Monitor;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class MonitorUserControl : UserControl
{
    private readonly MonitorViewModel _viewModel;

    private ScrollViewer? _outputScrollViewer;
    private TextBox? _commandTextBox;

    public MonitorUserControl(MonitorViewModel viewModel)
    {
        _viewModel = viewModel;

        InitializeComponent();

        _outputScrollViewer = this.FindControl<ScrollViewer>("OutputScrollViewer");
        _commandTextBox = this.FindControl<TextBox>("CommandTextBox");

        DataContext = _viewModel;

        _viewModel.OutputLines.CollectionChanged += OnOutputLinesChanged;

        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _viewModel.RefreshStatus();
        ScrollToEnd();

        // Set focus after a small delay to ensure the control is fully rendered
        _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _commandTextBox?.Focus();
            });
        });
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _viewModel.OutputLines.CollectionChanged -= OnOutputLinesChanged;
    }

    private void OnOutputLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScrollToEnd();
    }

    private void ScrollToEnd()
    {
        _outputScrollViewer?.ScrollToEnd();
    }

    private void CommandTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                _viewModel.SendCommand.Execute().Subscribe();
                e.Handled = true;
                break;
            case Key.Up:
                _viewModel.NavigateHistoryPrevious();
                if (_commandTextBox is { } upTextBox)
                    upTextBox.CaretIndex = upTextBox.Text?.Length ?? 0;
                e.Handled = true;
                break;
            case Key.Down:
                _viewModel.NavigateHistoryNext();
                if (_commandTextBox is { } downTextBox)
                    downTextBox.CaretIndex = downTextBox.Text?.Length ?? 0;
                e.Handled = true;
                break;
            case Key.Escape:
                _viewModel.ClearInput();
                e.Handled = true;
                break;
            case Key.F12:
                _viewModel.CloseCommand.Execute().Subscribe();
                e.Handled = true;
                break;
        }
    }
}
