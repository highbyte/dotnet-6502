using System;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class ScriptEditorViewModel : ViewModelBase
{
    private string _fileName;
    private string _content;

    public bool IsNew { get; }

    public string Title => IsNew ? "New Script" : $"Edit: {_fileName}";

    public string FileName
    {
        get => _fileName;
        set
        {
            this.RaiseAndSetIfChanged(ref _fileName, value);
            this.RaisePropertyChanged(nameof(CanSave));
        }
    }

    public string Content
    {
        get => _content;
        set => this.RaiseAndSetIfChanged(ref _content, value);
    }

    public bool CanSave => !string.IsNullOrWhiteSpace(_fileName);

    /// <summary>
    /// Set by SaveCommand. Null means the user cancelled.
    /// </summary>
    public (string fileName, string content)? DialogResult { get; private set; }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public event EventHandler? CloseRequested;

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "ReactiveUI WhenAnyValue is used intentionally for ViewModel bindings; members are rooted by XAML and direct references.")]
    public ScriptEditorViewModel(bool isNew, string fileName = "", string content = "")
    {
        IsNew = isNew;
        _fileName = fileName;
        _content = content;

        var canSave = this.WhenAnyValue(vm => vm.CanSave);
        SaveCommand = ReactiveCommandHelper.CreateSafeCommand(Save, canSave);
        CancelCommand = ReactiveCommandHelper.CreateSafeCommand(Cancel);
    }

    private void Save()
    {
        DialogResult = (FileName.Trim(), Content);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel()
    {
        DialogResult = null;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
