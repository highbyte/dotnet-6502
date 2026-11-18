using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Interactivity;
using Highbyte.DotNet6502.App.Avalonia.Core.Services;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Recording;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class MainView
{
    private MemoryStream? _recordedDemo;

    private void ToggleDemoSection_Click(object? sender, RoutedEventArgs e) => _subscribedViewModel?.ToggleDemoSection();

    private void StartDemoRecording_Click(object? sender, RoutedEventArgs e) => SafeAsyncHelper.Execute(async () =>
    {
        var vm = _subscribedViewModel;
        if (vm?.CanStartDemoRecording != true) return;
        vm.SetDemoBusy(true);
        try
        {
            if (_recordedDemo != null) await _recordedDemo.DisposeAsync();
            _recordedDemo = new MemoryStream();
            await vm.HostApp.StartDemoRecordingAsync(_recordedDemo);
        }
        finally { vm.SetDemoBusy(false); }
    });

    private void StopDemoRecording_Click(object? sender, RoutedEventArgs e) => SafeAsyncHelper.Execute(async () =>
    {
        var vm = _subscribedViewModel;
        if (vm?.CanStopDemoRecording != true) return;
        vm.SetDemoBusy(true);
        try
        {
            vm.HostApp.StopDemoRecording();
            vm.SetPendingDemoSave(true);
            await SaveRecordedDemoAsync();
        }
        finally { vm.SetDemoBusy(false); }
    });

    private void SaveDemo_Click(object? sender, RoutedEventArgs e) => SafeAsyncHelper.Execute(async () =>
    {
        var vm = _subscribedViewModel;
        if (vm?.CanSaveDemo != true) return;
        vm.SetDemoBusy(true);
        try { await SaveRecordedDemoAsync(); }
        finally { vm.SetDemoBusy(false); }
    });

    private async Task SaveRecordedDemoAsync()
    {
        if (_recordedDemo == null || _subscribedViewModel == null) return;
        var saver = (Application.Current as App)?.GetServiceProvider()?.GetService<IAppFileSaver>()
            ?? throw new InvalidOperationException("File saving is unavailable.");
        bool saved = await saver.SaveFileAsync(this, new AppFileSaveOptions(
            "Save recorded demo", _subscribedViewModel.HostApp.SelectedSystemName + "-recording", "d6502demo",
            [new AppFilePickerFileType("Emulator demo", ["*.d6502demo"]) ]), _recordedDemo.ToArray());
        if (!saved) return; // Keep the completed recording for another save attempt.
        _subscribedViewModel.SetPendingDemoSave(false);
        await _recordedDemo.DisposeAsync();
        _recordedDemo = null;
        Logger.LogInformation("Recorded demo saved.");
    }

    private void OpenDemo_Click(object? sender, RoutedEventArgs e) => SafeAsyncHelper.Execute(async () =>
    {
        var vm = _subscribedViewModel;
        if (vm?.CanOpenDemo != true) return;
        var picker = (Application.Current as App)?.GetServiceProvider()?.GetService<IAppFilePicker>()
            ?? throw new InvalidOperationException("File opening is unavailable.");
        vm.SetDemoBusy(true);
        var host = vm.HostApp;
        var original = host.CurrentRunningSystem;
        bool wasRunning = host.EmulatorState == EmulatorState.Running;
        if (wasRunning) host.Pause();
        bool loaded = false;
        try
        {
            var picked = await picker.OpenFileAsync(this, new AppFilePickerOpenOptions(
                "Open demo — replaces the current machine state", false,
                [new AppFilePickerFileType("Emulator demo", ["*.d6502demo"]) ], MaximumFileBytes: DemoReader.MaxPackageBytes));
            if (picked == null) return;
            if (picked.Bytes.LongLength > DemoReader.MaxPackageBytes)
                throw new InvalidDataException("Demo exceeds 128 MiB.");
            using var input = new MemoryStream(picked.Bytes, writable: false);
            await host.LoadDemoAsync(input);
            loaded = true;
            await host.Start();
        }
        finally
        {
            if (!loaded && wasRunning && ReferenceEquals(original, host.CurrentRunningSystem)
                && host.EmulatorState == EmulatorState.Paused)
                await host.Start();
            vm.SetDemoBusy(false);
        }
    });

    private void StopDemoPlayback_Click(object? sender, RoutedEventArgs e)
    {
        _subscribedViewModel?.HostApp.StopDemoPlayback();
        _subscribedViewModel?.RefreshDemoState();
    }

    private void RestartDemo_Click(object? sender, RoutedEventArgs e) => SafeAsyncHelper.Execute(async () =>
    {
        var vm = _subscribedViewModel;
        if (vm?.CanRestartDemo != true) return;
        vm.SetDemoBusy(true);
        try { await vm.HostApp.RestartDemoAsync(); await vm.HostApp.Start(); }
        finally { vm.SetDemoBusy(false); }
    });

    private void StepDemoFrame_Click(object? sender, RoutedEventArgs e) => SafeAsyncHelper.Execute(async () =>
    {
        var vm = _subscribedViewModel;
        if (vm?.CanStepDemo != true) return;
        await vm.HostApp.StepEmulatorFramesAsync(1);
        vm.RefreshDemoState();
    });
}
