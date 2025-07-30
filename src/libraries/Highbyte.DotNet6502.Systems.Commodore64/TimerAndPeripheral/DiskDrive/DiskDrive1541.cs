using System.Text;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.IEC;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive;

public class DiskDrive1541 : IIECDevice
{
    private IECBus? _bus;

    private D64DiskImage? _d64DiskImage;

    private int _deviceNumber = 8;
    public int DeviceNumber => _deviceNumber;

    private DeviceLineState _setCLKLine = DeviceLineState.NotHolding;
    public DeviceLineState SetCLKLine => _setCLKLine;

    private DeviceLineState _setDATALine = DeviceLineState.NotHolding;
    public DeviceLineState SetDATALine => _setDATALine;

    public bool IsDisketteInserted => _d64DiskImage != null;


    /// <summary>
    /// The 1541 mode, if it's currently listening for commands sent by the C64, talking (sending) to the C64, or idle.
    /// </summary>
    private Mode _currentDriveMode = Mode.Idle;

    // For receiving data from C64
    private ByteTransferState _byteReceiveTransferState = ByteTransferState.Start;
    private bool _receivingCommand = false;
    private int _receiveChannel = -1;
    private byte _receivedByte = 0;
    private bool _receiveBitValue = false;
    private int _receiveBitPos = 0;
    private int _readyToReceiveTimeoutInstructionCounter = 0;
    private bool _pulsingEOIAcknowledgement = false;
    private int _eoiAcknowledgementPulseInstructionCounter = 0;
    private bool _eoiAcknowledgementHandled = false;
    private readonly List<byte> _receivedFilenameBuffer = new();

    // For transmitting data to C64
    private ByteTransferState _byteSendTransferState = ByteTransferState.Start;
    private readonly Queue<byte> _sendBuffer = new();
    private byte? _sendByte;
    private int _sendBitPos = 0;
    private int _delayedSendStartInstructionCounter;
    private int _waitingForListenerToDataReadInstructionCounter = 0;
    private int _intermissionBeforeStartSendBitsInstructionCounter = 0;
    private int _delayBeforeSettingSendBitInstructionCounter;
    private bool _awaitingEOIAcknowledgementStartPulse = false;
    private bool _awaitingEOIAcknowledgementEndPulse = false;
    private bool _sendFileError = false;


    private readonly ILogger _logger;

    public DiskDrive1541(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(typeof(DiskDrive1541).Name);
    }

    public void SetBus(IECBus iECBus)
    {
        _bus = _bus is null ? iECBus : throw new InvalidOperationException("DiskDrive1541 is already set to a bus.");
    }

    public void SetD64DiskImage(D64DiskImage d64DiskImage)
    {
        _d64DiskImage = d64DiskImage;
    }

    public void RemoveD64DiskImage()
    {
        _d64DiskImage = null;
    }

    public void SetDeviceNumber(int deviceNumber)
    {
        if (deviceNumber < 8 || deviceNumber > 11)
            throw new ArgumentOutOfRangeException(nameof(deviceNumber), deviceNumber, "Device number must be between 8 and 11.");
        
        _deviceNumber = deviceNumber;
    }

    public void SetLines(DeviceLineState? setCLKLine = null, DeviceLineState? setDATALine = null)
    {
        _bus?.BeforeDeviceOrHostLineStateChanged();

        bool changed = false;
        if (setCLKLine.HasValue && setCLKLine.Value != _setCLKLine)
        {
            _setCLKLine = setCLKLine.Value; changed = true;
        }
        if (setDATALine.HasValue && setDATALine.Value != _setDATALine)
        {
            _setDATALine = setDATALine.Value; changed = true;
        }

        if (changed && _bus != null)
        {
            _bus.OnDevicesChangedState();
        }
    }

    public void OnBusChangedState()
    {
        if (_bus == null)
            return;

        // === 1. Detect ATN going low (start of command phase) ===
        if (_bus.ATNLineState == BusLineState.Low
            && _bus.ATNLineStatePrevious == BusLineState.Released
            && !_receivingCommand)
        {
            _logger?.LogTrace("[1541] ATN low: start of command byte reception");
            _byteReceiveTransferState = ByteTransferState.Start;

            _receivingCommand = true;
            _receivedByte = 0;
            _receiveBitPos = 0;

            // When host pulls ATN low, the device should pull down its DATA line. Also release the Clock line.
            _logger?.LogTrace("[1541] Set DATA low as ack.");
            SetLines(setDATALine: DeviceLineState.Holding, setCLKLine: DeviceLineState.NotHolding);

            _currentDriveMode = Mode.Idle;
        }
        // === 1b. Detect ATN being release (end of command phase) ===
        else if (_bus.ATNLineState == BusLineState.Released
            && _bus.ATNLineStatePrevious == BusLineState.Low)
        {
            _logger?.LogTrace("[1541] ATN release: end of command byte reception");

            _receivingCommand = false;
            _byteReceiveTransferState = ByteTransferState.Start;

            _logger?.LogTrace("[1541] Set DATA low as ack.");
            SetLines(setDATALine: DeviceLineState.Holding);
            //SetLines(setDATALine: DeviceLineState.NotHolding);

            //_currentDriveMode = Mode.Idle;
        }

        // SPECIAL: Handle TalkingPending state
        // If ATN is not low and we are in TalkingPending state, we need to check if the Clock line is released and Data line low, which will trigger Talking state.
        if (_currentDriveMode == Mode.TalkingPending
            && _bus.ATNLineState == BusLineState.Released
            && _bus.CLKLineState == BusLineState.Released)
        {
            // Host (C64) has released the Clock line as signal it's leaving Talk mode and entering Listener mode.
            _byteSendTransferState = ByteTransferState.DelayedStart;

            _currentDriveMode = Mode.Talking;
            _logger?.LogTrace("[1541] CLK released: Host is leaving Talking and entering Listener mode.");
            // We acknowledge this by setting our Clock line to low (true) and Data line to released (false).
            SetLines(setCLKLine: DeviceLineState.Holding, setDATALine: DeviceLineState.NotHolding);
            return;
        }

        // If we are in Talking mode, we shouldn't expect any further bytes received.
        if (_currentDriveMode == Mode.Talking)
            return;


        // If neither ATN is low (we are forced to listen to commands) nor we are in Listen mode, don't expect any more received bytes.
        if (_bus.ATNLineState == BusLineState.Released && _currentDriveMode != Mode.Listening)
            return;

        // === 2. Ready to receive, the talker (host) releases clock line ===
        if (_byteReceiveTransferState == ByteTransferState.Start
            && _bus.CLKLineState == BusLineState.Released
            && _bus.CLKLineStatePrevious == BusLineState.Low)
        {
            _byteReceiveTransferState = ByteTransferState.Step1_ReadyToReceive;
            _readyToReceiveTimeoutInstructionCounter = 0;
            _eoiAcknowledgementHandled = false;
            _pulsingEOIAcknowledgement = false;

            // Host is ready to send the first bit of the command byte
            _logger?.LogTrace("[1541] CLK released: ready to send command byte");

            // === 2b. Ready for data, the listener (this devices) releases data line ===
            SetLines(setDATALine: DeviceLineState.NotHolding); // release DATA line
            _logger?.LogTrace("[1541] Setting DATA low ack - ready for command byte");
        }

        // === 3. EOI (End Of Indicator) - handled in Tick() method ===

        // === 4. Completion. Talker (host) sets Clock to low ===
        if (_byteReceiveTransferState == ByteTransferState.Step1_ReadyToReceive
            && _bus.CLKLineState == BusLineState.Low)
        {
            _logger?.LogTrace($"[1541] CLK low: ready to receive bits.");
            _byteReceiveTransferState = ByteTransferState.Step2_CompletionBeforeSendByte;
            _receivedByte = 0;
            _receiveBitPos = 0;
        }

        // === 5. Bit transfer start - Talker releases Clock when it has Data bit ===
        if (_byteReceiveTransferState == ByteTransferState.Step2_CompletionBeforeSendByte
            && _bus.CLKLineState == BusLineState.Released)
        {
            // Sender sets data High (Released / "False") when sending a 1 bit.
            _receiveBitValue = _bus.DATALineState == BusLineState.Released;
            _byteReceiveTransferState = ByteTransferState.Step3_BitReadyToSend;
        }

        // === 5b. Bit transfer end - Talker pulls Clock low it's done sending a bit ===
        if (_byteReceiveTransferState == ByteTransferState.Step3_BitReadyToSend
            && _bus.CLKLineState == BusLineState.Low 
            && _bus.CLKLineStatePrevious == BusLineState.Released)
        {
            // Bits are sent with least significant bit first.
            // If _recvbitValue is true, set bit in _currentReceivedByte at position _recvBitPos.
            // Assume _currentReceivedByte was initialized to 0 before bits being sent.
            if (_receiveBitValue)
                _receivedByte |= (byte)(1 << _receiveBitPos);
            _receiveBitPos++;

            _logger?.LogTrace($"[1541] Bit {_receiveBitPos} received: {(_receiveBitValue ? 1 : 0)}");

            if (_receiveBitPos == 8)
            {
                _logger?.LogTrace($"[1541] Full byte received: ${{_currentReceivedByte:X2}}");

                // Set DATA line to low (pulling) to signal we have received the full byte
                // The talker is now watching the Data line. If the listener doesn't pull the Data line true within one millisecond it will know that something's wrong and may alarm appropriately.
                SetLines(setDATALine: DeviceLineState.Holding);
                _logger?.LogTrace($"[1541] Set DATA low to acknowledge byte received");

                // Process the received byte
                if (_bus.ATNLineState == BusLineState.Low)
                {
                    // If we are in command reception mode (ATN is low), we need to handle the command
                    HandleCommand(_receivedByte); // LISTEN, TALK, etc.
                }
                else
                {
                    // If we are not in command mode, we received a data byte that needs to be processed
                    HandleCommandData(_receivedByte);
                }

                // Prepare for next byte arrival
                _byteReceiveTransferState = ByteTransferState.Start;
                _receivedByte = 0;
                _receiveBitPos = 0;
            }
            else
            {
                // Not a full byte yet, continue receiving bits
                _byteReceiveTransferState = ByteTransferState.Step2_CompletionBeforeSendByte;
            }
        }
    }

    public void Tick()
    {
        if (_bus == null)
            return;

        if (_currentDriveMode == Mode.Listening)
        {
            HandleReceiveEOI();
        }
        else if (_currentDriveMode == Mode.Talking && _bus.ATNLineState == BusLineState.Released)
        {
            TalkTick();
        }
    }

    /// <summary>   
    /// Handle timeout check of Talker acknowledgement of "Ready to Receive" state,
    /// If it's the last byte sent (EOI), then the acknowledgement from the Talker (setting Clock back low) won't come until 
    /// the listener acknowledges the EOI state by waiting for 200 Microseconds for Clock low, and it not received then it 
    /// must set the Data line Low for at least 60 Microseconds and then back to High (released) again.
    /// </summary>   
    private void HandleReceiveEOI()
    {
        if (_byteReceiveTransferState == ByteTransferState.Step1_ReadyToReceive && !_eoiAcknowledgementHandled)
        {

            if (_bus.DATALineState == BusLineState.Released && !_pulsingEOIAcknowledgement)
            {
                _readyToReceiveTimeoutInstructionCounter++;

                // Number of cycles for 200 Microseconds:
                //  200 µs ÷ 1.015 µs/cycle ≈ 197.04 cycles
                // Assume "worst" case scenario all all instrucitons taking 1 cycle.
                // 197.04 cycles  = 198 instructions
                //const int NumberOfInstructionsToWaitForReadForDataAcknowledgement = 198;
                const int NumberOfInstructionsToWaitForReadForDataAcknowledgement = 198/7;
                if (_readyToReceiveTimeoutInstructionCounter >= NumberOfInstructionsToWaitForReadForDataAcknowledgement)
                {
                    // EOI detected
                    _logger?.LogTrace("[1541] Timeout for waiting for ackknowledgement from talker for Ready for Data. Pulsing EOI acknowledgement by pulling Data low for 60 microseconds.");
                    SetLines(setDATALine: DeviceLineState.Holding);
                    _readyToReceiveTimeoutInstructionCounter = 0;
                    _eoiAcknowledgementHandled = false;
                    _pulsingEOIAcknowledgement = true;
                    _eoiAcknowledgementPulseInstructionCounter = 0;
                }
            }
            else
            {
                if (_pulsingEOIAcknowledgement)
                {
                    _eoiAcknowledgementPulseInstructionCounter++;
                    // Now acknowledge the EOI by pulling Data line low for 60 Microseconds
                    // Number of cycles for 60 Microseconds:
                    //  60 µs ÷ 1.015 µs/cycle ≈ 59.02 cycles
                    // Assume worst case scenario of 7 cycles per instruction. Number of instruction to wait at least:
                    //   59.02 cycles ÷ 7 cycles/instruction ≈ 8.43
                    const int NumberOfInstructionsToPulseEOIAcknowledgement = 9;
                    if (_eoiAcknowledgementPulseInstructionCounter >= NumberOfInstructionsToPulseEOIAcknowledgement)
                    {
                        // EOI acknowledgement pulse done
                        _eoiAcknowledgementHandled = true;
                        _pulsingEOIAcknowledgement = false;
                        _eoiAcknowledgementPulseInstructionCounter = 0;
                        _logger?.LogTrace("[1541] Has now pulsed EOI acknowledgement by pulling Data low for 60 microseconds.");
                        SetLines(setDATALine: DeviceLineState.NotHolding);
                    }
                }
            }
        }
    }

    private void TalkTick()
    {
        if (_bus.ATNLineState == BusLineState.Low)
        {
            // If ATN is held low by host (C64), we are in command reception mode, so don't send any data.
            return;
        }


        var isSendingData = _sendBuffer.Count > 0 || _sendByte != null;
        if (!isSendingData && !_sendFileError)
            return;

        // Special case as workaround when first switching Talker/Listener roles between C64 and this drive.
        if (_byteSendTransferState == ByteTransferState.DelayedStart)
        {
            _delayedSendStartInstructionCounter++;
            // Wait for enough time for the previous state to be recognized by the C64 so it won't end up in endless loop.
            const int NumberOfInstructionsToWaitForDelayedStart = 30;
            if (_delayedSendStartInstructionCounter < NumberOfInstructionsToWaitForDelayedStart)
            {
                // Still waiting for the delayed start
                return;
            }
            _delayedSendStartInstructionCounter = 0;
            _byteSendTransferState = ByteTransferState.Start;
        }

        // Wait for the Talker to assume Listener role by it setting Clock line is low (true)
        if (_byteSendTransferState == ByteTransferState.Start
            && _bus.DATALineState == BusLineState.Low)
        {
            // Talker (this drive) holds Clock line low (true) and Data released at the start of sending a byte
            SetLines(setCLKLine: DeviceLineState.Holding, setDATALine: DeviceLineState.NotHolding);
            _byteSendTransferState = ByteTransferState.Step1_ReadyToSend;
            _logger?.LogTrace("[1541] CLK held low, ready to send byte.");
            return;
        }

        if (_byteSendTransferState == ByteTransferState.Step1_ReadyToSend
            && _bus.DATALineState == BusLineState.Low)
        {
            // Talker (this drive) releases Clock line (false) to indicate it's ready to send the byte
            SetLines(setCLKLine: DeviceLineState.NotHolding);
            _byteSendTransferState = ByteTransferState.Step1_ReadyToReceive;
            _logger?.LogTrace("[1541] CLK released, waiting for Data to be released");
            return;
        }

        if (_byteSendTransferState == ByteTransferState.Step1_ReadyToReceive
            && _bus.DATALineState == BusLineState.Released
            && !_awaitingEOIAcknowledgementEndPulse)
        {
            // Listener (C64) has released Data line, we can start sending bits - soon.
            // Before going to step Step2_CompletionBeforeSendByte where Clock line is set low (true):
            // - If this is NOT the last byte, we wait 60 microseconds. This ensures the C64 has time to get in state to detect next byte start (detecting Clock low).
            // - If this is the last byte to send (EOI), wait at least 200 microseconds. This ensures the C64 to detect that next byte is the last.

            if (_sendFileError)
            {
                // Never send a byte, it will trigger an timeout in C64 Kernal and return error to the user.
                return;
            }

            if (_sendBuffer.Count == 1)
            {
                _awaitingEOIAcknowledgementStartPulse = true;
                _awaitingEOIAcknowledgementEndPulse = false;

                // If we are about to send the last byte, indicate this by signaling EOI to the listener (C64) by wait 200 (256?) microseconds or more before sending byte.
                _intermissionBeforeStartSendBitsInstructionCounter++;
                const int IntermissionBeforeStartSendBitsEOIWaitIntructionCount = (256 + 100) / 2; // 256 microseconds (+ slack). Approximate avg 2 cycles (1 cycle approx 1 microsecond) per instruction.
                if (_intermissionBeforeStartSendBitsInstructionCounter < IntermissionBeforeStartSendBitsEOIWaitIntructionCount)
                {
                    // Still waiting a while before sending the byte
                    return;
                }
                _intermissionBeforeStartSendBitsInstructionCounter = 0;

            }
            else
            {
                _awaitingEOIAcknowledgementStartPulse = false;
                _awaitingEOIAcknowledgementEndPulse = false;

                _intermissionBeforeStartSendBitsInstructionCounter++;

                const int IntermissionBeforeStartSendBitsWaitInstructionCount = (60) / 2; // 60 microseconds. Approximate avg 2 cycles (1 cycle approx 1 microsecond) per instruction. 
                if (_intermissionBeforeStartSendBitsInstructionCounter < IntermissionBeforeStartSendBitsWaitInstructionCount)
                {
                    // Still waiting a while before sending the byte
                    return;
                }
                _intermissionBeforeStartSendBitsInstructionCounter = 0;

                _byteSendTransferState = ByteTransferState.Step2_CompletionBeforeSendByte;
            }
        }

        // Receiver acknowledgement of EOI (End Of Indicator) byte is by pulling the Data line low (true) for a brief period.
        if (_byteSendTransferState == ByteTransferState.Step1_ReadyToReceive
            && _awaitingEOIAcknowledgementStartPulse
            && _bus.DATALineState == BusLineState.Low)
        {
            _awaitingEOIAcknowledgementStartPulse = false;
            _awaitingEOIAcknowledgementEndPulse = true;
        }

        // Receiver acknowledgement of EOI (End Of Indicator) byte is by releasing the Data line high (false) direly after pulling it low (true).
        if (_byteSendTransferState == ByteTransferState.Step1_ReadyToReceive
            && _awaitingEOIAcknowledgementEndPulse
            && _bus.DATALineState == BusLineState.Released)
        {
            _awaitingEOIAcknowledgementStartPulse = false;
            _awaitingEOIAcknowledgementEndPulse = false;
            _byteSendTransferState = ByteTransferState.Step2_CompletionBeforeSendByte;
        }

        if (_byteSendTransferState == ByteTransferState.Step2_CompletionBeforeSendByte)
        {
            if (_sendByte == null)
            {
                _sendByte = _sendBuffer.Dequeue();
                _sendBitPos = 0;
            }
            // Talker (this drive) pulls Clock line low (true) to indicate it's ready to send the bits
            SetLines(setCLKLine: DeviceLineState.Holding);
            _byteSendTransferState = ByteTransferState.Step3_BitReadyToSend;
            _logger?.LogTrace("[1541] CLK held low, ready to send bits.");
        }

        if (_byteSendTransferState == ByteTransferState.Step3_BitReadyToSend)
        {

            // The talker (in this case this disk drive) typically has a bit ready to sent withing 70 microseconds.
            // Note: Some delay is necessary for listener to not miss the transition before sending the first bit Step1_ReadyToRecieve -> Step2_CompletionBeforeSendByte.
            _delayBeforeSettingSendBitInstructionCounter++;
            const int WaitBeforeSettingBitInstructionCount = (30 / 2); // 30 microseconds (just a guess). Approximate avg 2 cycles (1 cycle approx 1 microsecond) per instruction. 
            if (_delayBeforeSettingSendBitInstructionCounter < WaitBeforeSettingBitInstructionCount)
            {
                // Still waiting for setting bit
                return;
            }
            _delayBeforeSettingSendBitInstructionCounter = 0;

            // Sending next bit, least significant bit first
            bool bit = (_sendByte.Value & (1 << _sendBitPos)) != 0;

            // Set the Data line to the bit value: 1 = Data line released (false), 0 = Data line pulled low (true).
            // And release the Clock line to signal that the bit is ready to be read by the listener (C64).
            SetLines(
                setDATALine: bit ? DeviceLineState.NotHolding : DeviceLineState.Holding,
                setCLKLine: DeviceLineState.NotHolding);

            _byteSendTransferState = ByteTransferState.Step3_BitWaitForReceiver;
            _waitingForListenerToDataReadInstructionCounter = 0;

            // Wait for a fixed time to allow the listener (C64) to read the Data line, and then pull the Clock line back to low (true) and release Data line.
        }

        if (_byteSendTransferState == ByteTransferState.Step3_BitWaitForReceiver)
        {
            _waitingForListenerToDataReadInstructionCounter++;

            // Number of cycles for 60 Microseconds:
            // 60 µs ÷ 1.015 µs/cycle ≈ 59.02 cycles
            // Assume "worst" case scenario all all instructions taking 1 cycle.
            // 59.02 cycles  = 60 instructions
            const int NumberOfInstructionsToWaitForDataRead = 60 / 2; // 60 microseconds. Approximate avg 2 cycles (avg instructions takes 2 cycles) per instruction.
            if (_waitingForListenerToDataReadInstructionCounter < NumberOfInstructionsToWaitForDataRead)
            {
                // Still waiting for listener to read the Data line
                return;
            }

            // Now assume bit has been read by the listener (C64).
            // Set the CLK line back to low (true), and release the Data line.
            SetLines(setCLKLine: DeviceLineState.Holding, setDATALine: DeviceLineState.NotHolding);

            // Check if we have sent all bits of the byte
            _sendBitPos++;
            if (_sendBitPos >= 8)
            {
                _logger?.LogTrace($"[1541] Byte sent: {_sendByte:X2}");

                // All bits sent
                // Set status to wait for the Listener (C64) to acknowledge the byte sent by pulling the Data line low (true).
                _byteSendTransferState = ByteTransferState.Step4_Acknowledge;
                return;
            }
            else
            {
                // Repeat for next bit
                _byteSendTransferState = ByteTransferState.Step3_BitReadyToSend;
            }
        }

        if (_byteSendTransferState == ByteTransferState.Step4_Acknowledge
            && _bus.DATALineState == BusLineState.Low)
        {
            // Listener (C64) has acknowledged the byte sent by pulling the Data line low (true).

            // Reset for next byte
            _sendByte = null;
            _sendBitPos = 0;
            _byteSendTransferState = ByteTransferState.Start;
        }
    }

    private void HandleCommand(byte command)
    {
        var busCommand = ParseBusCommand(command, out byte? device, out byte? channel);

        if (busCommand == BusCommand.Listen)
        {
            if (device == DeviceNumber)
            {
                _currentDriveMode = Mode.Listening;
                _logger?.LogDebug($"[1541] Command: Listen Device: {device}");
                // Clear the filename buffer when starting a new listen session
                _receivedFilenameBuffer.Clear();
                // Listener (this drive) holds Data line low and Clock released at the start of listen
                SetLines(setDATALine: DeviceLineState.Holding, setCLKLine: DeviceLineState.NotHolding);
            }
        }
        else if (busCommand == BusCommand.UnlistenAllDevices)
        {
            _currentDriveMode = Mode.Idle;
            _byteReceiveTransferState = ByteTransferState.Start;
            _logger?.LogDebug($"[1541] Command: Unlisten all devices");
        }
        else if (busCommand == BusCommand.Talk)
        {
            if (device == DeviceNumber)
            {
                // After the host (C64) sent the Talk command, it still will send another command (Open channel?) so we cannot start talking yet.
                // After the host as sent both the Talk and Open commands, it will release both the Clock and Data lines (which will indicate that the host has switched role in is then a listener and the device (this drive) is the talker)..
                // See code in OnBusChangedState that checks for TalkingPending (set here) and the Clock released.
                _currentDriveMode = Mode.TalkingPending;
                _logger?.LogDebug($"[1541] Command: Talk (pending) Device: {device}");
            }
        }
        else if (busCommand == BusCommand.UntalkAllDevices)
        {
            _currentDriveMode = Mode.Idle;
            _logger?.LogDebug($"[1541] Command: Untalk all devices");
        }
        else if (busCommand == BusCommand.Reopen)
        {
            if (_currentDriveMode == Mode.Listening)
            {
                if (channel == _receiveChannel)
                {
                    _logger?.LogDebug($"[1541] Command: Reopen Channel: {channel}");
                    // TODO:
                }
            }
            else if (_currentDriveMode == Mode.TalkingPending)
            {
                if (channel == _receiveChannel)
                {
                    _logger?.LogDebug($"[1541] Command: Reopen Channel in Talk mode: {channel}");
                    ProcessFilenameCommand();
                }
            }
        }
        else if (busCommand == BusCommand.Close)
        {
            if (_currentDriveMode == Mode.Listening)
            {
                if (channel == _receiveChannel)
                {
                    _logger?.LogDebug($"[1541] Command: Close Channel: {channel}");
                    // TODO:
                    _receiveChannel = -1;
                }
            }
        }
        else if (busCommand == BusCommand.Open)
        {
            if (_currentDriveMode == Mode.Listening)
            {
                _receiveChannel = channel!.Value;
                _logger?.LogDebug($"[1541] Command: Open Channel: {channel}");
            }
            else if (_currentDriveMode == Mode.TalkingPending)
            {
                if (channel == _receiveChannel)
                {
                    _logger?.LogDebug($"[1541] Command: Open Channel in Talk mode: {channel}");
                    ProcessFilenameCommand();
                }
            }
        }
    }

    private void HandleCommandData(byte currentReceivedByte)
    {
        // Store received data bytes in buffer instead of handling them immediately.
        // The received buffer will be processed when we receive an OPEN or REOPEN command in TALK mode.
        _receivedFilenameBuffer.Add(currentReceivedByte);
        _logger?.LogTrace($"[1541] Received filename byte: {currentReceivedByte:X2} ('{(char)currentReceivedByte}')");
    }

    private BusCommand? ParseBusCommand(byte busCommand, out byte? device, out byte? channel)
    {
        device = null;
        channel = null;
        if (busCommand == 0x3F)
            return BusCommand.UnlistenAllDevices;
        if (busCommand == 0x5F)
            return BusCommand.UntalkAllDevices;

        switch (busCommand & 0xF0)
        {
            case 0x20: // Listen device (0-30)
                device = (byte?)(busCommand & 0x1F);
                return BusCommand.Listen;
            case 0x40: // Talk device 
                device = (byte?)(busCommand & 0x1F);
                return BusCommand.Talk;
            case 0x60: // Reopen channel (0-15)
                channel = (byte?)(busCommand & 0x0F);
                return BusCommand.Reopen;
            case 0xE0: // Close
                channel = (byte?)(busCommand & 0x0F);
                return BusCommand.Close;
            case 0xF0: // Close
                channel = (byte?)(busCommand & 0x0F);
                return BusCommand.Open;
            default:
                return null;
        }
    }

    private void ProcessFilenameCommand()
    {
        _sendFileError = false;

        if (_receivedFilenameBuffer.Count == 0)
        {
            _logger?.LogWarning("[1541] No filename received, cannot process command");
            SetSendFileError();
            return;
        }

        if (_d64DiskImage == null)
        {
            _logger?.LogWarning("[1541] No disk inserted, cannot process command");
            // When no disk is inserted, the drive should behave as if no disk is present
            // rather than triggering a file error. This will cause a timeout on the C64 side
            // which is the expected behavior when no disk is inserted.
            SetSendFileError();
            return;
        }

        // Convert the received bytes to a string (PETSCII to ASCII)
        var filename = Encoding.ASCII.GetString(_receivedFilenameBuffer.ToArray()).Trim();
        _logger?.LogInformation($"[1541] Processing filename command: '{filename}'");

        // Clear the transmit buffer before adding new data
        _sendBuffer.Clear();

        if (filename == "$")
        {
            // Directory listing request
            _logger?.LogInformation("[1541] Directory listing requested");
            var directoryProgram = _d64DiskImage.DirectoryToPrgFormat();
            foreach (var b in directoryProgram)
            {
                _sendBuffer.Enqueue(b);
            }
        }
        else if (filename == "*")
        {
            // Load first file (wildcard)
            _logger?.LogInformation("[1541] First file load requested (wildcard *)");
            var firstFileName = _d64DiskImage.GetFirstFileName();
            if (firstFileName == null)
            {
                _logger?.LogWarning("[1541] No files found on disk for wildcard load");
                SetSendFileError();
            }
            else
            {
                _logger?.LogInformation($"[1541] Loading first file: '{firstFileName}'");
                var program = _d64DiskImage.ReadFileContent(firstFileName);
                foreach (var b in program)
                {
                    _sendBuffer.Enqueue(b);
                }
                _logger?.LogInformation($"[1541] First file '{firstFileName}' loaded, {program.Length} bytes ready for transmission");
            }
        }
        else
        {
            // Specific file request
            _logger?.LogInformation($"[1541] File load requested: '{filename}'");
            if (!_d64DiskImage.FileExists(filename))
            {
                _logger?.LogWarning($"[1541] File not found: '{filename}'");
                SetSendFileError();
            }
            else
            {
                var program = _d64DiskImage.ReadFileContent(filename);
                foreach (var b in program)
                {
                    _sendBuffer.Enqueue(b);
                }
                _logger?.LogInformation($"[1541] File '{filename}' loaded, {program.Length} bytes ready for transmission");
            }
        }

        // Clear the filename buffer as it's been processed
        _receivedFilenameBuffer.Clear();
    }

    private void SetSendFileError()
    {
        _sendFileError = true;
        _sendBuffer.Clear();
        _receivedFilenameBuffer.Clear();
    }
}
