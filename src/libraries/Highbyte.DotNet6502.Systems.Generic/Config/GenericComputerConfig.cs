namespace Highbyte.DotNet6502.Systems.Generic.Config;

public class GenericComputerConfig
{
    private string _programBinaryFile = default!;
    private byte[] _programBinary = default!;
    private bool _stopAtBRK;
    private ulong _cPUCyclesPerFrame;
    private float _screenRefreshFrequencyHz;
    private bool _waitForHostToAcknowledgeFrame;
    private EmulatorMemoryConfig _memory = default!;
    public Type? RenderProviderType { get; set; }

    public string ProgramBinaryFile
    {
        get { return _programBinaryFile; }
        set
        {
            _programBinaryFile = value;
        }
    }

    public byte[] ProgramBinary
    {
        get { return _programBinary; }
        set
        {
            _programBinary = value;
        }
    }
    public bool StopAtBRK
    {
        get { return _stopAtBRK; }
        set
        {
            _stopAtBRK = value;
        }
    }

    public ulong CPUCyclesPerFrame
    {
        get { return _cPUCyclesPerFrame; }
        set
        {
            _cPUCyclesPerFrame = value;
        }
    }
    public float ScreenRefreshFrequencyHz
    {
        get { return _screenRefreshFrequencyHz; }
        set
        {
            _screenRefreshFrequencyHz = value;
        }
    }
    public bool WaitForHostToAcknowledgeFrame
    {
        get { return _waitForHostToAcknowledgeFrame; }
        set
        {
            _waitForHostToAcknowledgeFrame = value;
        }
    }

    public EmulatorMemoryConfig Memory
    {
        get { return _memory; }
        set
        {
            _memory = value;
        }
    }

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
        }
    }

    public GenericComputerConfig()
    {
        Memory = new();
        StopAtBRK = true;
        CPUCyclesPerFrame = 5000;
        ScreenRefreshFrequencyHz = 60.0f;
        WaitForHostToAcknowledgeFrame = false;
    }

    public object Clone()
    {
        var clone = (GenericComputerConfig)this.MemberwiseClone();
        clone.Memory = (EmulatorMemoryConfig)Memory.Clone();
        return clone;
    }
}
