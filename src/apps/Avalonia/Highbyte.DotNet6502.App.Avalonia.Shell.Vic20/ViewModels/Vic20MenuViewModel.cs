using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia.Vic20;
using Highbyte.DotNet6502.Systems;
using Vic20System = Highbyte.DotNet6502.Systems.Vic20.Vic20;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.ViewModels;

/// <summary>
/// Menu view model for the VIC-20 shell plugin.
/// </summary>
public class Vic20MenuViewModel : ViewModelBase
{
    private readonly AvaloniaHostApp _hostApp;
    private readonly ILogger _logger;

    public AvaloniaHostApp HostApp => _hostApp;

    public ReactiveCommand<byte[], Unit> LoadBasicFileCommand { get; }

    public Vic20MenuViewModel(AvaloniaHostApp hostApp, ILoggerFactory loggerFactory)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));
        _logger = loggerFactory.CreateLogger(typeof(Vic20MenuViewModel).Name);

        _hostApp
            .WhenAnyValue(x => x.EmulatorState)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(IsFileOperationEnabled)));

        LoadBasicFileCommand = ReactiveCommandHelper.CreateSafeCommand<byte[]>(
            async (fileBuffer) => await LoadBasicFileAsync(fileBuffer),
            this.WhenAnyValue(x => x.IsFileOperationEnabled),
            RxSchedulers.MainThreadScheduler);
    }

    public bool IsFileOperationEnabled => _hostApp.EmulatorState != EmulatorState.Uninitialized;

    public bool HasConfigValidationErrors
    {
        get
        {
            if (_hostApp.CurrentHostSystemConfig is not Vic20HostConfig vic20HostConfig)
                return false;

            return !vic20HostConfig.IsValid(out _);
        }
    }

    private async Task LoadBasicFileAsync(byte[] fileBuffer)
    {
        if (_hostApp.EmulatorState == EmulatorState.Uninitialized)
            return;

        bool wasRunning = _hostApp.EmulatorState == EmulatorState.Running;
        if (wasRunning)
            _hostApp.Pause();

        try
        {
            BinaryLoader.Load(
                _hostApp.CurrentRunningSystem!.Mem,
                fileBuffer,
                out ushort loadedAtAddress,
                out ushort fileLength);

            if (loadedAtAddress != Vic20System.BASIC_LOAD_ADDRESS)
            {
                _logger.LogWarning($"Loaded program is not a BASIC program; expected load address {Vic20System.BASIC_LOAD_ADDRESS.ToHex()} but got {loadedAtAddress.ToHex()}");
            }
            else
            {
                var vic20 = (Vic20System)_hostApp.CurrentRunningSystem!;
                vic20.InitBasicMemoryVariables(loadedAtAddress, fileLength);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading BASIC .prg");
        }
        finally
        {
            if (wasRunning)
                await _hostApp.Start();
        }
    }
}
