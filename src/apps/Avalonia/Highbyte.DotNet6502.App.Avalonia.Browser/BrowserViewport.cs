using System;
using System.Runtime.Versioning;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Highbyte.DotNet6502.App.Avalonia.Core;

namespace Highbyte.DotNet6502.App.Avalonia.Browser;

/// <summary>
/// Measures the desktop layout at its natural size so the HTML page can scroll the
/// entire canvas, including displays enlarged by the emulator's Scale slider.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class BrowserViewport : Decorator
{
    private Size _contentSize;
    private bool _notificationPending;
    private TopLevel? _attachedRoot;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var root = _attachedRoot = TopLevel.GetTopLevel(this);
        if (root != null)
        {
            root.PropertyChanged += OnRootPropertyChanged;
            UpdateModalState(root);
        }
        Program.JSInterop.SetViewportChangedCallback(json =>
        {
            if (!TryParseViewport(json, out var viewport))
                return;

            // JavaScript can notify during content measurement; apply the change
            // after that pass, on the UI thread.
            Dispatcher.UIThread.Post(() =>
            {
                if (root != null && TopLevel.GetTopLevel(this) == root)
                    root.SetValue(OverlayDialogHelper.VisibleViewportProperty,
                        viewport);
            });
        });
    }

    private void OnRootPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == OverlayDialogHelper.ModalOverlayCountProperty && _attachedRoot != null)
            UpdateModalState(_attachedRoot);
    }

    private static void UpdateModalState(TopLevel root)
        => Program.JSInterop.SetModalOpen(root.GetValue(OverlayDialogHelper.ModalOverlayCountProperty) > 0);

    private static bool TryParseViewport(string json, out Rect viewport)
    {
        viewport = default;
        try
        {
            using var document = JsonDocument.Parse(json);
            var area = document.RootElement;
            if (area.ValueKind != JsonValueKind.Array || area.GetArrayLength() != 4)
                return false;
            foreach (var coordinate in area.EnumerateArray())
                if (coordinate.ValueKind != JsonValueKind.Number)
                    return false;
            if (!area[0].TryGetDouble(out var x) || !area[1].TryGetDouble(out var y)
                || !area[2].TryGetDouble(out var width) || !area[3].TryGetDouble(out var height)
                || !double.IsFinite(x) || !double.IsFinite(y)
                || !double.IsFinite(width) || !double.IsFinite(height)
                || x < 0 || y < 0 || width <= 0 || height <= 0)
                return false;
            viewport = new Rect(x, y, width, height);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Program.JSInterop.ClearViewportChangedCallback();
        if (_attachedRoot != null)
            _attachedRoot.PropertyChanged -= OnRootPropertyChanged;
        Program.JSInterop.SetModalOpen(false);
        _attachedRoot?.ClearValue(OverlayDialogHelper.VisibleViewportProperty);
        _attachedRoot = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
            return default;

        child.Measure(Size.Infinity);
        var size = child.DesiredSize;
        if (size != _contentSize)
        {
            _contentSize = size;
            if (!_notificationPending)
            {
                _notificationPending = true;
                // Resize the DOM after this layout pass, avoiding reentrant measurement.
                Dispatcher.UIThread.Post(() =>
                {
                    _notificationPending = false;
                    Program.JSInterop.SetBrowserContentSize(_contentSize.Width, _contentSize.Height);
                });
            }
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(new Size(
            Math.Max(finalSize.Width, _contentSize.Width),
            Math.Max(finalSize.Height, _contentSize.Height))));
        return finalSize;
    }
}
