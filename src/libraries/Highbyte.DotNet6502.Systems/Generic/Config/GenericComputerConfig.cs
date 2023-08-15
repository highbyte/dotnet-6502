namespace Highbyte.DotNet6502.Systems.Generic.Config;

public class GenericComputerConfig : ISystemConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.GenericComputer";

    private bool _isDirty = false;
    private string _programBinaryFile = default!;
    private byte[] _programBinary = default!;
    private bool _stopAtBRK;
    private ulong _cPUCyclesPerFrame;
    private float _screenRefreshFrequencyHz;
    private bool _waitForHostToAcknowledgeFrame;
    private EmulatorMemoryConfig _memory = default!;

    public bool IsDirty => _isDirty;
    public void ClearDirty()
    {
        _isDirty = false;
    }

    public string ProgramBinaryFile
    {
        get { return _programBinaryFile; }
        set
        {
            _programBinaryFile = value;
            _isDirty = true;
        }
    }

    public byte[] ProgramBinary
    {
        get { return _programBinary; }
        set
        {
            _programBinary = value;
            _isDirty = true;
        }
    }
    public bool StopAtBRK
    {
        get { return _stopAtBRK; }
        set
        {
            _stopAtBRK = value;
            _isDirty = true;
        }
    }

    public ulong CPUCyclesPerFrame
    {
        get { return _cPUCyclesPerFrame; }
        set
        {
            _cPUCyclesPerFrame = value;
            _isDirty = true;
        }
    }
    public float ScreenRefreshFrequencyHz
    {
        get { return _screenRefreshFrequencyHz; }
        set
        {
            _screenRefreshFrequencyHz = value;
            _isDirty = true;
        }
    }
    public bool WaitForHostToAcknowledgeFrame
    {
        get { return _waitForHostToAcknowledgeFrame; }
        set
        {
            _waitForHostToAcknowledgeFrame = value;
            _isDirty = true;
        }
    }

    public EmulatorMemoryConfig Memory
    {
        get { return _memory; }
        set
        {
            _memory = value;
            _isDirty = true;
        }
    }

    public bool AudioSupported { get; set; }

    private bool _audioEnabled;
    public bool AudioEnabled
    {
        get
        {
            return _audioEnabled;
        }
        set
        {
            _audioEnabled = value;
            _isDirty = true;
        }
    }

    public GenericComputerConfig()
    {
        Memory = new();
        StopAtBRK = true;
        CPUCyclesPerFrame = 5000;
        ScreenRefreshFrequencyHz = 60.0f;
        WaitForHostToAcknowledgeFrame = true;
    }

    public GenericComputerConfig Clone()
    {
        return new GenericComputerConfig
        {
            ProgramBinary = ProgramBinary,
            ProgramBinaryFile = ProgramBinaryFile,
            StopAtBRK = StopAtBRK,
            CPUCyclesPerFrame = CPUCyclesPerFrame,
            ScreenRefreshFrequencyHz = ScreenRefreshFrequencyHz,
            WaitForHostToAcknowledgeFrame = WaitForHostToAcknowledgeFrame,
            Memory = Memory.Clone(),
            AudioSupported = false,
            AudioEnabled = false
        };
    }

    public void Validate()
    {
        if (!IsValid(out List<string> validationErrors))
            throw new Exception($"Config errors: {string.Join(',', validationErrors)}");
    }

    public bool IsValid(out List<string> validationErrors)
    {
        validationErrors = new List<string>();

        if (string.IsNullOrEmpty(ProgramBinaryFile) && ProgramBinary?.Length == 0)
            validationErrors.Add($"{nameof(ProgramBinaryFile)} or {nameof(ProgramBinary)} must be specified.");
        if (!string.IsNullOrEmpty(ProgramBinaryFile) && (ProgramBinary != null & ProgramBinary?.Length > 0))
            validationErrors.Add($"{nameof(ProgramBinaryFile)} and {nameof(ProgramBinary)} cannot both be specified.");

        if (!string.IsNullOrEmpty(ProgramBinaryFile))
        {
            var prgFile = PathHelper.ExpandOSEnvironmentVariables(ProgramBinaryFile);

            if (!File.Exists(prgFile))
            {
                validationErrors.Add($"Cannot find 6502 binary file: {prgFile}");
            }
        }

        // Check for incorrect memory config, overlapping addresses, etc.
        var memoryValidationErrors = new List<string>();
        if (!Memory.Validate(out memoryValidationErrors))
            validationErrors.AddRange(memoryValidationErrors);

        return validationErrors.Count == 0;
    }
}
