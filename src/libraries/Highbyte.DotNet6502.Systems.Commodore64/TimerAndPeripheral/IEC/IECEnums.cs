namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.IEC;

/// <summary>
/// The state of a device line on the IEC bus.
/// </summary>
public enum DeviceLineState
{
    /// <summary>
    /// Not holding the line low = line has some voltage = "false" state
    /// </summary>
    NotHolding,
    /// <summary>
    /// Holding (pulling) the line low = trying to ground the line to 0 volts = "true" state
    /// </summary>
    Holding
}

/// <summary>
/// The state of the over all bus line on the IEC bus.
/// </summary>
public enum BusLineState
{
    /// <summary>
    /// No device is holding (pulling) the line.
    /// The line is in an overall "false" state.
    /// </summary>
    Released,
    /// <summary>
    /// At least one device is holding (pulling) the line.
    /// The line is in an overall "true" state.
    /// </summary>
    Low
}

public enum Mode { Idle, Listening, TalkingPending, Talking }

public enum ByteTransferState
{
    /// <summary>
    /// Special case when first switching Talker/Listener roles between C64 and this drive.
    /// Waits for enough time for the previous state to be recognized by the C64 so it won't end up in endless loop.
    /// Workaround because this emulator does not simulate the time an electronic signal takes between setting them on one side of the bus for it to be recognized by the memory mapped port on the other side of the bus.
    /// </summary>
    DelayedStart,

    /// <summary>
    /// Initial state before the transfer of a byte starts.
    /// - Talker holds Clock line low (true).
    /// - Listener holds Data line low (true).
    /// </summary>
    Start,

    /// <summary>
    /// Ready to send byte.
    /// - Talker releases Clock line (false).
    /// </summary>
    Step1_ReadyToSend,

    /// <summary>
    /// Ready to to receive byte.
    /// - Listener releases Data line (false).
    /// </summary>
    Step1_ReadyToReceive,

    /// <summary>
    /// Intermission (Only if EOI - End Of Indicator).
    /// - Listener times out and acknowledges EOI by pulling Data line low (true) for a brief period. 
    /// </summary>
    EOIIntermission,

    /// <summary>
    /// Completion before sending the byte.
    /// - Talker pulls Clock line low (true)
    /// </summary>
    Step2_CompletionBeforeSendByte,

    /// <summary>
    /// - Talker sets data bit on Data line
    /// - Talker releases Clock line (false) to signal "bit ready".
    /// - Talker holds lines steady for fixed time 
    ///    -> Listener must read Data line during this time.
    /// - Talker pulls clock line true and releases Data line.
    /// </summary>
    Step3_BitReadyToSend,

    /// <summary>
    /// - Waiting for the listener to read the data bit within a fixed time.
    /// </summary>
    Step3_BitWaitForReceiver,

    /// <summary>
    /// Acknowledge that byte has been received.
    /// - Listener pulls Data line low (true) to acknowledge that it has received the byte.
    /// </summary>
    Step4_Acknowledge,
}

public enum BusCommand
{
    Listen,
    UnlistenAllDevices,
    Talk,
    UntalkAllDevices,
    Reopen,
    Close,
    Open
}
