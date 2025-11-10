using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Impl.Avalonia.Generic.Input;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;

public class GenericComputerHostConfig : IHostSystemConfig, ICloneable
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.GenericComputer.Avalonia";

    private GenericComputerSystemConfig _systemConfig = new();
    ISystemConfig IHostSystemConfig.SystemConfig => _systemConfig;

    public GenericComputerSystemConfig SystemConfig
    {
        get => _systemConfig;
        set => _systemConfig = value;
    }

    [JsonIgnore]
    public bool AudioSupported => false; // Generic computer doesn't have audio

    public GenericComputerAvaloniaInputConfig InputConfig { get; set; } = new GenericComputerAvaloniaInputConfig();

    private bool _isDirty = false;
    [JsonIgnore]
    public bool IsDirty => _isDirty || _systemConfig.IsDirty;
    public void ClearDirty()
    {
        _isDirty = false;
        _systemConfig.ClearDirty();
    }

    public void Validate()
    {
        if (!IsValid(out var validationErrors))
        {
            throw new Exception($"Invalid configuration: {string.Join(", ", validationErrors)}");
        }
    }

    public bool IsValid(out List<string> validationErrors)
    {
        validationErrors = new List<string>();

        SystemConfig.IsValid(out var systemConfigValidationErrors);
        validationErrors.AddRange(systemConfigValidationErrors);

        return validationErrors.Count == 0;
    }

    public GenericComputerHostConfig()
    {
        _systemConfig = new GenericComputerSystemConfig();
    }

    public object Clone()
    {
        var clone = (GenericComputerHostConfig)this.MemberwiseClone();
        clone._systemConfig = (GenericComputerSystemConfig)_systemConfig.Clone();
        clone.InputConfig = (GenericComputerAvaloniaInputConfig)InputConfig.Clone();
        return clone;
    }
}
