using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class ScriptEditorDialog : UserControl
{
    /// <summary>
    /// Raised when the dialog completes. True = saved, False = cancelled.
    /// </summary>
    public event EventHandler<bool>? DialogCompleted;

    public ScriptEditorDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        var vm = DataContext as ScriptEditorViewModel;

        // Focus the right TextBox so that single-click button interaction works
        // immediately without a focus-acquiring first click (especially in browser WASM).
        var target = vm?.IsNew == true
            ? this.FindControl<TextBox>("FileNameBox")
            : this.FindControl<TextBox>("ContentBox");
        target?.Focus();

        // In WASM on Mac, Avalonia doesn't map Cmd (Meta) to clipboard shortcuts correctly —
        // Cmd+V inserts "v", Cmd+C inserts "c", etc. Work around by intercepting KeyDown in
        // the tunnel phase and calling the TextBox clipboard methods directly.
        if (PlatformDetection.IsRunningInWebAssembly())
        {
            foreach (var name in new[] { "FileNameBox", "ContentBox" })
            {
                var box = this.FindControl<TextBox>(name);
                box?.AddHandler(KeyDownEvent, TextBox_ClipboardKeyDown, RoutingStrategies.Tunnel);
            }
        }
    }

    private static void TextBox_ClipboardKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox) return;

        bool hasModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control)
                        || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (!hasModifier) return;

        switch (e.Key)
        {
            case Key.V:
                e.Handled = true;
                textBox.Paste();
                break;
            case Key.C:
                e.Handled = true;
                textBox.Copy();
                break;
            case Key.X:
                e.Handled = true;
                textBox.Cut();
                break;
            case Key.A:
                e.Handled = true;
                textBox.SelectAll();
                break;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ScriptEditorViewModel vm)
            vm.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        var saved = (sender as ScriptEditorViewModel)?.DialogResult != null;
        DialogCompleted?.Invoke(this, saved);
    }
}
