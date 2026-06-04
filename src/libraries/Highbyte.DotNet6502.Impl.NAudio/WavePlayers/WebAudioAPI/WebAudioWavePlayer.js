// WebAudioWavePlayer.js
// JavaScript module for WebAudio playback of audio from .NET NAudio

export const WebAudioWavePlayer = (() => {
    let audioContext = null;
    let audioWorkletNode = null;
    let sampleRate = 44100;
    let channels = 1;

    // Ring buffer for continuous audio samples instead of array of buffers
    let sampleBuffer = null;
    let writePosition = 0;
    let readPosition = 0;
    let bufferedSamples = 0;
    let bufferCapacity = 0;
    let targetBufferedSamples = 0;
    let highWatermarkSamples = 0;
    let sharedSampleBuffer = null;
    let sharedStateBuffer = null;
    let sharedSamples = null;
    let sharedState = null;
    let workletModuleUrl = null;
    let statsTimerId = null;
    let useSharedAudioWorklet = false;

    const StateIndex = {
        ReadPosition: 0,
        WritePosition: 1,
        BufferedSamples: 2,
        IsPlaying: 3,
        Underruns: 4,
        Overflows: 5
    };
    const StateCount = 6;

    // Minimum samples needed before starting playback (to avoid immediate underrun)
    let minBufferBeforePlay = 0;
    let hasEnoughToStart = false;

    // Configurable settings (set from C#)
    let scriptProcessorBufferSize = 2048;
    let statsIntervalMs = 10000;

    // Adaptive buffering: track underruns and increase buffer if needed
    let underrunCount = 0;
    let overflowCount = 0;
    let totalUnderrunCount = 0;
    let totalOverflowCount = 0;
    let consecutiveUnderruns = 0;
    let lastStatsTime = 0;

    let isPlaying = false;

    // Log callback to .NET
    let logCallback = null;
    
    // Log levels matching LogLevel in C#
    const LogLevel = {
        Debug: 0,
        Info: 1,
        Warning: 2,
        Error: 3
    };

    /**
     * Log a message to .NET and optionally to console as fallback
     * @param {number} level - Log level from LogLevel enum
     * @param {string} message - The message to log
     */
    function log(level, message) {
        if (logCallback) {
            try {
                logCallback(level, message);
            } catch {
                // Fallback to console if callback fails
                console.log(`[WebAudioWavePlayer] ${message}`);
            }
        } else if (level === LogLevel.Error) {
            // Fallback to console if no callback registered
            console.error(`[WebAudioWavePlayer] ${message}`);
        } else {
            console.log(`[WebAudioWavePlayer] ${message}`);
        }
    }

    function logDebug(message) { log(LogLevel.Debug, message); }
    function logInfo(message) { log(LogLevel.Info, message); }
    function logWarning(message) { log(LogLevel.Warning, message); }
    function logError(message) { log(LogLevel.Error, message); }

    /**
     * Register the log callback from .NET
     * This is called from C# to set up the callback for log messages
     * @param {Function} callback - The callback function that takes (level, message)
     */
    function registerLogCallback(callback) {
        logCallback = callback;
        if (logCallback) {
            logInfo('Log callback registered successfully');
        }
    }

    function getValidScriptProcessorBufferSize() {
        const clampedSize = Math.min(16384, Math.max(256, scriptProcessorBufferSize));
        return Math.pow(2, Math.round(Math.log2(clampedSize)));
    }

    function getAudioLatencyText() {
        if (!audioContext) {
            return 'n/a';
        }

        const baseLatency = typeof audioContext.baseLatency === 'number'
            ? `${(audioContext.baseLatency * 1000).toFixed(1)}ms`
            : 'n/a';
        const outputLatency = typeof audioContext.outputLatency === 'number'
            ? `${(audioContext.outputLatency * 1000).toFixed(1)}ms`
            : 'n/a';
        return `base=${baseLatency}, output=${outputLatency}`;
    }

    function clearStatsTimer() {
        if (statsTimerId !== null) {
            clearInterval(statsTimerId);
            statsTimerId = null;
        }
    }

    /**
     * Initialize WebAudio context and audio worklet
     * @param {number} sRate - Sample rate (e.g., 44100)
     * @param {number} numChannels - Number of channels (1 = mono, 2 = stereo)
     * @param {number} bufSize - Buffer size in samples (from C# DesiredLatency)
     * @param {number} ringBufferCapacityMultiplier - Multiplier for ring buffer capacity (e.g., 3.0)
     * @param {number} minBufferBeforePlayMultiplier - Multiplier for min buffer before play (e.g., 1.0)
     * @param {number} scriptProcBufferSize - ScriptProcessor buffer size (power of 2, 256-16384)
     * @param {number} statsIntMs - Stats logging interval in ms (0 to disable)
     */
    function initialize(sRate, numChannels, bufSize, ringBufferCapacityMultiplier, minBufferBeforePlayMultiplier, scriptProcBufferSize, statsIntMs) {
        initializeCore(sRate, numChannels, bufSize, ringBufferCapacityMultiplier, minBufferBeforePlayMultiplier, scriptProcBufferSize, statsIntMs, false, false);
    }

    function initializeDirectWrite(sRate, numChannels, bufSize, ringBufferCapacityMultiplier, minBufferBeforePlayMultiplier, scriptProcBufferSize, statsIntMs) {
        initializeCore(sRate, numChannels, bufSize, ringBufferCapacityMultiplier, minBufferBeforePlayMultiplier, scriptProcBufferSize, statsIntMs, true, false);
    }

    function initializeDirectWriteAudioWorklet(sRate, numChannels, bufSize, ringBufferCapacityMultiplier, minBufferBeforePlayMultiplier, scriptProcBufferSize, statsIntMs) {
        initializeCore(sRate, numChannels, bufSize, ringBufferCapacityMultiplier, minBufferBeforePlayMultiplier, scriptProcBufferSize, statsIntMs, true, true);
    }

    function initializeCore(sRate, numChannels, bufSize, ringBufferCapacityMultiplier, minBufferBeforePlayMultiplier, scriptProcBufferSize, statsIntMs, preferSharedAudioWorklet, requireSharedAudioWorklet) {
        sampleRate = sRate;
        channels = numChannels;
        scriptProcessorBufferSize = scriptProcBufferSize;
        statsIntervalMs = statsIntMs;
        isPlaying = false;
        hasEnoughToStart = false;
        useSharedAudioWorklet = false;
        underrunCount = 0;
        overflowCount = 0;
        totalUnderrunCount = 0;
        totalOverflowCount = 0;
        consecutiveUnderruns = 0;
        lastStatsTime = performance.now();
        sharedSampleBuffer = null;
        sharedStateBuffer = null;
        sharedSamples = null;
        sharedState = null;
        clearStatsTimer();

        // Ring buffer capacity based on multiplier from C#
        bufferCapacity = Math.ceil(bufSize * channels * ringBufferCapacityMultiplier);
        sampleBuffer = new Float32Array(bufferCapacity);
        writePosition = 0;
        readPosition = 0;
        bufferedSamples = 0;

        // Start playback threshold based on multiplier from C#. AudioWorklet always renders
        // 128-frame quanta. This check must not depend on audioContext yet because the threshold
        // is calculated before the new context is created below.
        const callbackFrames = preferSharedAudioWorklet && canUseSharedAudioWorkletEnvironment()
            ? 128
            : getValidScriptProcessorBufferSize();
        const callbackSamples = callbackFrames * channels;
        minBufferBeforePlay = Math.max(
            Math.ceil(bufSize * channels * minBufferBeforePlayMultiplier),
            callbackSamples * 2);
        targetBufferedSamples = Math.min(
            bufferCapacity,
            Math.max(Math.ceil(bufSize * channels), minBufferBeforePlay));
        highWatermarkSamples = Math.max(targetBufferedSamples, Math.floor(bufferCapacity * 0.95));


        // Close existing AudioContext if it exists (sample rate may have changed)
        if (audioContext) {
            if (audioWorkletNode) {
                audioWorkletNode.disconnect();
                audioWorkletNode = null;
            }
            audioContext.close();
            audioContext = null;
            logInfo('Existing audioContext closed');
        }

        // Create new AudioContext with the specified sample rate
        audioContext = new (globalThis.AudioContext || globalThis.webkitAudioContext)({
            sampleRate: sampleRate,
            latencyHint: 'interactive' // Low latency for real-time audio
        });
        logInfo(`AudioContext created with sample rate: ${audioContext.sampleRate}`);
        logInfo(`AudioContext latency: ${getAudioLatencyText()}`);

        // Resume context if it was suspended (browser autoplay policy)
        if (audioContext.state === 'suspended') {
            audioContext.resume();
            logInfo('AudioContext resumed');
        }

        const bufferCapacityMs = (bufferCapacity / channels / sampleRate * 1000).toFixed(1);
        const minBufferMs = (minBufferBeforePlay / channels / sampleRate * 1000).toFixed(1);
        const desiredLatencyMs = (bufSize / sampleRate * 1000).toFixed(1);
        logInfo(`Initialized:`);
        logInfo(`  Sample rate: ${sampleRate}Hz, Channels: ${channels}`);
        logInfo(`  C# buffer: ${bufSize} samples (~${desiredLatencyMs}ms)`);
        logInfo(`  Ring buffer: ${bufferCapacity} samples (~${bufferCapacityMs}ms) [${ringBufferCapacityMultiplier}x multiplier]`);
        logInfo(`  Ring target/high-water: ${targetBufferedSamples}/${highWatermarkSamples} samples`);
        logInfo(`  Start threshold: ${minBufferBeforePlay} samples (~${minBufferMs}ms) [${minBufferBeforePlayMultiplier}x multiplier]`);
        logInfo(`  ScriptProcessor buffer: ${scriptProcessorBufferSize} samples`);
        logInfo(`  Stats interval: ${statsIntervalMs}ms${statsIntervalMs === 0 ? ' (disabled)' : ''}`);

        if (preferSharedAudioWorklet && canUseSharedAudioWorklet()) {
            setupSharedAudioWorklet(requireSharedAudioWorklet);
        } else {
            if (preferSharedAudioWorklet) {
                const message = 'SharedArrayBuffer AudioWorklet unavailable';
                if (requireSharedAudioWorklet) {
                    logError(`${message}; DirectWriteAudioWorklet mode cannot start`);
                    return;
                }
                logWarning(`${message}; falling back to ScriptProcessorNode`);
            }
            setupScriptProcessor();
        }

        logDebug(`Initialize exit`);
    }

    function canUseSharedAudioWorklet() {
        return Boolean(
            canUseSharedAudioWorkletEnvironment() &&
            audioContext?.audioWorklet
        );
    }

    function canUseSharedAudioWorkletEnvironment() {
        return Boolean(
            globalThis.crossOriginIsolated &&
            typeof SharedArrayBuffer === 'function'
        );
    }

    function createWorkletProcessorSource() {
        return `
const StateIndex = {
  ReadPosition: 0,
  WritePosition: 1,
  BufferedSamples: 2,
  IsPlaying: 3,
  Underruns: 4,
  Overflows: 5
};

class DotNet6502SharedRingProcessor extends AudioWorkletProcessor {
  constructor(options) {
    super();
    const processorOptions = options.processorOptions;
    this.samples = new Float32Array(processorOptions.sampleBuffer);
    this.state = new Int32Array(processorOptions.stateBuffer);
    this.capacity = processorOptions.capacity;
    this.channels = processorOptions.channels;
  }

  process(_inputs, outputs) {
    const output = outputs[0];
    if (!output || output.length === 0) {
      return true;
    }

    const frameCount = output[0].length;
    const samplesNeeded = frameCount * this.channels;
    const playing = Atomics.load(this.state, StateIndex.IsPlaying) === 1;
    const buffered = Atomics.load(this.state, StateIndex.BufferedSamples);

    if (!playing || buffered < samplesNeeded) {
      for (let channel = 0; channel < output.length; channel++) {
        output[channel].fill(0);
      }
      if (playing) {
        Atomics.add(this.state, StateIndex.Underruns, 1);
        Atomics.store(this.state, StateIndex.IsPlaying, 0);
      }
      return true;
    }

    let readPosition = Atomics.load(this.state, StateIndex.ReadPosition);
    for (let channel = 0; channel < output.length; channel++) {
      const channelOutput = output[channel];
      for (let frame = 0; frame < frameCount; frame++) {
        channelOutput[frame] = this.samples[(readPosition + frame * this.channels + channel) % this.capacity];
      }
    }

    readPosition = (readPosition + samplesNeeded) % this.capacity;
    Atomics.store(this.state, StateIndex.ReadPosition, readPosition);
    Atomics.sub(this.state, StateIndex.BufferedSamples, samplesNeeded);
    return true;
  }
}

registerProcessor('dotnet6502-shared-ring-processor', DotNet6502SharedRingProcessor);
`;
    }

    async function setupSharedAudioWorklet(requireSharedAudioWorklet) {
        logDebug(`SetupSharedAudioWorklet start`);

        if (audioWorkletNode) {
            audioWorkletNode.disconnect();
            audioWorkletNode = null;
        }

        sharedSampleBuffer = new SharedArrayBuffer(Float32Array.BYTES_PER_ELEMENT * bufferCapacity);
        sharedStateBuffer = new SharedArrayBuffer(Int32Array.BYTES_PER_ELEMENT * StateCount);
        sharedSamples = new Float32Array(sharedSampleBuffer);
        sharedState = new Int32Array(sharedStateBuffer);
        sharedSamples.fill(0);
        sharedState.fill(0);
        useSharedAudioWorklet = true;

        try {
            if (!workletModuleUrl) {
                const workletBlob = new Blob([createWorkletProcessorSource()], { type: 'text/javascript' });
                workletModuleUrl = URL.createObjectURL(workletBlob);
            }

            await audioContext.audioWorklet.addModule(workletModuleUrl);
            audioWorkletNode = new AudioWorkletNode(audioContext, 'dotnet6502-shared-ring-processor', {
                numberOfInputs: 0,
                numberOfOutputs: 1,
                outputChannelCount: [channels],
                processorOptions: {
                    sampleBuffer: sharedSampleBuffer,
                    stateBuffer: sharedStateBuffer,
                    capacity: bufferCapacity,
                    channels
                }
            });

            audioWorkletNode.connect(audioContext.destination);

            const renderQuantumMs = (128 / sampleRate * 1000).toFixed(1);
            logInfo(`AudioWorklet shared ring connected (~${renderQuantumMs}ms render quantum)`);
            startSharedAudioWorkletStats();
        } catch (error) {
            const message = `AudioWorklet shared ring setup failed: ${error?.message ?? error}`;
            sharedSampleBuffer = null;
            sharedStateBuffer = null;
            sharedSamples = null;
            sharedState = null;
            useSharedAudioWorklet = false;
            if (requireSharedAudioWorklet) {
                logError(message);
                return;
            }
            logWarning(`${message}; falling back to ScriptProcessorNode`);
            setupScriptProcessor();
        }

        logDebug(`SetupSharedAudioWorklet exit`);
    }

    function startSharedAudioWorkletStats() {
        clearStatsTimer();
        if (statsIntervalMs <= 0) {
            return;
        }

        statsTimerId = setInterval(() => {
            if (!sharedState) {
                return;
            }

            const currentUnderruns = Atomics.exchange(sharedState, StateIndex.Underruns, 0);
            const currentOverflows = Atomics.exchange(sharedState, StateIndex.Overflows, 0);
            totalUnderrunCount += currentUnderruns;
            totalOverflowCount += currentOverflows;
            if (currentUnderruns > 0 || currentOverflows > 0) {
                const currentBufferedSamples = Atomics.load(sharedState, StateIndex.BufferedSamples);
                const bufferLevelMs = (currentBufferedSamples / channels / sampleRate * 1000).toFixed(1);
                logWarning(`Stats: underruns=${currentUnderruns}, overflows=${currentOverflows}, current buffer=${bufferLevelMs}ms`);
            }
        }, statsIntervalMs);
    }

    function fillOutputWithSilence(outputBuffer) {
        for (let channel = 0; channel < channels; channel++) {
            outputBuffer.getChannelData(channel).fill(0);
        }
    }

    function copyFromRingBufferToOutput(outputBuffer, bufferLength, samplesNeeded) {
        for (let channel = 0; channel < channels; channel++) {
            const outputData = outputBuffer.getChannelData(channel);
            for (let i = 0; i < bufferLength; i++) {
                // Interleaved audio: sample index = frame * channels + channel
                const ringBufferIndex = (readPosition + i * channels + channel) % bufferCapacity;
                outputData[i] = sampleBuffer[ringBufferIndex];
            }
        }
        readPosition = (readPosition + samplesNeeded) % bufferCapacity;
        bufferedSamples -= samplesNeeded;
    }

    /**
     * Set up ScriptProcessorNode for audio processing
     */
    function setupScriptProcessor() {
        logDebug(`SetupScriptProcessor start`);

        // Clean up existing node
        if (audioWorkletNode) {
            audioWorkletNode.disconnect();
            audioWorkletNode = null;
            logInfo('Existing ScriptProcessorNode disconnected');
        }

        // Create ScriptProcessorNode - use the configured buffer size, clamped to valid range.
        // Buffer size must be power of 2 between 256 and 16384.
        const validBufferSize = getValidScriptProcessorBufferSize();
        
        audioWorkletNode = audioContext.createScriptProcessor(
            validBufferSize,
            0, // No input channels
            channels // Output channels
        );
        
        const processorLatencyMs = (validBufferSize / sampleRate * 1000).toFixed(1);
        logInfo(`ScriptProcessorNode created with buffer size: ${validBufferSize} (~${processorLatencyMs}ms)`);

        // Process audio data
        audioWorkletNode.onaudioprocess = (audioProcessingEvent) => {
            const outputBuffer = audioProcessingEvent.outputBuffer;
            const bufferLength = outputBuffer.length;
            const samplesNeeded = bufferLength * channels;

            // Wait until we have enough initial buffer before starting playback
            if (!hasEnoughToStart) {
                fillOutputWithSilence(outputBuffer);
                return;
            }

            if (isPlaying && bufferedSamples >= samplesNeeded) {
                copyFromRingBufferToOutput(outputBuffer, bufferLength, samplesNeeded);
                consecutiveUnderruns = 0;
            } else {
                fillOutputWithSilence(outputBuffer);
                // Track underruns (but not when paused). hasEnoughToStart is already true here
                // because we returned early above when it wasn't.
                if (isPlaying) {
                    underrunCount++;
                    totalUnderrunCount++;
                    consecutiveUnderruns++;
                    if (consecutiveUnderruns > 3) {
                        hasEnoughToStart = false;
                        consecutiveUnderruns = 0;
                        logWarning(`Re-buffering after repeated underruns; bufferedSamples=${bufferedSamples}`);
                    }
                }
            }

            // Periodic stats logging
            logStatsIfNeeded();
        };

        // Connect to destination (speakers)
        audioWorkletNode.connect(audioContext.destination);
        logInfo('ScriptProcessorNode connected to destination');
        
        logDebug(`SetupScriptProcessor exit`);
    }
    
    /**
     * Log buffer statistics periodically for debugging
     */
    function logStatsIfNeeded() {
        // Skip if stats logging is disabled
        if (statsIntervalMs <= 0) {
            return;
        }
        
        const now = performance.now();
        if (now - lastStatsTime >= statsIntervalMs) {
            if (underrunCount > 0 || overflowCount > 0) {
                const bufferLevelMs = (bufferedSamples / channels / sampleRate * 1000).toFixed(1);
                logWarning(`Stats: underruns=${underrunCount}, overflows=${overflowCount}, current buffer=${bufferLevelMs}ms`);
            }
            underrunCount = 0;
            overflowCount = 0;
            lastStatsTime = now;
        }
    }

    function queueAudioDataToSharedRing(floatArray, sampleCount) {
        if (!sharedSamples || !sharedState) {
            return false;
        }

        let currentBufferedSamples = Atomics.load(sharedState, StateIndex.BufferedSamples);
        const availableSpace = bufferCapacity - currentBufferedSamples;

        if (sampleCount > availableSpace) {
            const samplesToDiscard = sampleCount - availableSpace;
            const discardedSamples = discardOldestSharedSamples(samplesToDiscard);
            if (discardedSamples > 0) {
                Atomics.add(sharedState, StateIndex.Overflows, 1);
                currentBufferedSamples -= discardedSamples;
            }
        }

        let writePosition = Atomics.load(sharedState, StateIndex.WritePosition);
        for (let i = 0; i < sampleCount; i++) {
            sharedSamples[(writePosition + i) % bufferCapacity] = floatArray[i];
        }

        writePosition = (writePosition + sampleCount) % bufferCapacity;
        Atomics.store(sharedState, StateIndex.WritePosition, writePosition);
        currentBufferedSamples = Atomics.add(sharedState, StateIndex.BufferedSamples, sampleCount) + sampleCount;

        const workletIsPlaying = Atomics.load(sharedState, StateIndex.IsPlaying) === 1;
        const startThresholdSamples = hasEnoughToStart && !workletIsPlaying
            ? targetBufferedSamples
            : minBufferBeforePlay;
        if ((!hasEnoughToStart || !workletIsPlaying) && currentBufferedSamples >= startThresholdSamples) {
            hasEnoughToStart = true;
            Atomics.store(sharedState, StateIndex.IsPlaying, 1);
            const bufferLevelMs = (currentBufferedSamples / channels / sampleRate * 1000).toFixed(1);
            logInfo(`Starting AudioWorklet playback with ${currentBufferedSamples} samples (~${bufferLevelMs}ms) buffered`);
        }

        return true;
    }

    function discardOldestSharedSamples(samplesToDiscard) {
        if (samplesToDiscard <= 0 || !sharedState) {
            return 0;
        }

        const buffered = Atomics.load(sharedState, StateIndex.BufferedSamples);
        const discardCount = Math.min(samplesToDiscard, Math.max(0, buffered));
        if (discardCount === 0) {
            return 0;
        }

        const currentReadPosition = Atomics.load(sharedState, StateIndex.ReadPosition);
        Atomics.store(sharedState, StateIndex.ReadPosition, (currentReadPosition + discardCount) % bufferCapacity);
        const previousBuffered = Atomics.sub(sharedState, StateIndex.BufferedSamples, discardCount);
        if (previousBuffered < discardCount) {
            Atomics.store(sharedState, StateIndex.BufferedSamples, 0);
            return previousBuffered;
        }
        return discardCount;
    }

    /**
     * Queue audio data for playback
     * @param {Uint8Array} audioDataBytes - Audio samples as byte array (little-endian float32)
     * @param {number} sampleCount - Number of float samples in the array
     */
    function queueAudioData(audioDataBytes, sampleCount) {
        if (!audioContext || !sampleBuffer) {
            logError('Not initialized');
            return;
        }

        // Convert byte array to Float32Array
        const floatArray = new Float32Array(audioDataBytes.buffer, audioDataBytes.byteOffset, sampleCount);

        if (useSharedAudioWorklet && queueAudioDataToSharedRing(floatArray, sampleCount)) {
            if (audioContext.state === 'suspended') {
                audioContext.resume();
            }

            if (!isPlaying) {
                isPlaying = true;
            }

            return;
        }
        
        // Check if we have room in the ring buffer
        const availableSpace = bufferCapacity - bufferedSamples;
        
        if (sampleCount > availableSpace) {
            // Buffer overflow - drop oldest samples to make room
            const samplesToDiscard = sampleCount - availableSpace;
            readPosition = (readPosition + samplesToDiscard) % bufferCapacity;
            bufferedSamples -= samplesToDiscard;
            overflowCount++;
            totalOverflowCount++;
        }

        // Write samples to ring buffer
        for (let i = 0; i < sampleCount; i++) {
            sampleBuffer[(writePosition + i) % bufferCapacity] = floatArray[i];
        }
        writePosition = (writePosition + sampleCount) % bufferCapacity;
        bufferedSamples += sampleCount;

        // Resume context if it was suspended
        if (audioContext.state === 'suspended') {
            audioContext.resume();
        }

        // Start playing once we have enough buffered samples
        if (!hasEnoughToStart && bufferedSamples >= minBufferBeforePlay) {
            hasEnoughToStart = true;
            consecutiveUnderruns = 0;
            const bufferLevelMs = (bufferedSamples / channels / sampleRate * 1000).toFixed(1);
            logInfo(`Starting playback with ${bufferedSamples} samples (~${bufferLevelMs}ms) buffered`);
        }

        // Start playing if not already playing
        if (!isPlaying) {
            isPlaying = true;
        }
    }

    /**
     * Resume audio playback
     */
    function resume() {
        if (audioContext?.state === 'suspended') {
            audioContext.resume();
        }

        // Reconnect the processor node if it was disconnected during stop
        if (audioWorkletNode && audioContext) {
            audioWorkletNode.connect(audioContext.destination);
        }

        if (useSharedAudioWorklet && sharedState && hasEnoughToStart) {
            Atomics.store(sharedState, StateIndex.IsPlaying, 1);
        }
        
        isPlaying = true;
        logInfo('Resumed');
    }

    /**
     * Pause audio playback
     */
    function pause() {
        isPlaying = false;
        if (useSharedAudioWorklet && sharedState) {
            Atomics.store(sharedState, StateIndex.IsPlaying, 0);
        }
        logInfo('Paused');
    }

    /**
     * Stop audio playback and clear buffer
     */
    function stop() {
        isPlaying = false;
        hasEnoughToStart = false;
        writePosition = 0;
        readPosition = 0;
        bufferedSamples = 0;
        underrunCount = 0;
        overflowCount = 0;
        totalUnderrunCount = 0;
        totalOverflowCount = 0;
        consecutiveUnderruns = 0;
        if (sharedState) {
            sharedState.fill(0);
        }
        if (sharedSamples) {
            sharedSamples.fill(0);
        }
        if (sampleBuffer) {
            sampleBuffer.fill(0);
        }
        
        // Disconnect the processor node immediately to stop all audio output
        // This prevents any buffered audio from continuing to play
        if (audioWorkletNode) {
            audioWorkletNode.disconnect();
        }
        
        logInfo('Stopped');
    }

    /**
     * Cleanup resources
     */
    function cleanup() {
        stop();
        clearStatsTimer();
        
        if (audioWorkletNode) {
            audioWorkletNode.disconnect();
            audioWorkletNode = null;
        }

        if (audioContext) {
            audioContext.close();
            audioContext = null;
        }
        
        sampleBuffer = null;
        sharedSampleBuffer = null;
        sharedStateBuffer = null;
        sharedSamples = null;
        sharedState = null;
        useSharedAudioWorklet = false;
        logCallback = null;
        
        logInfo('Cleaned up');
    }

    function getCurrentBufferedSamples() {
        if (useSharedAudioWorklet && sharedState) {
            return Atomics.load(sharedState, StateIndex.BufferedSamples);
        }
        return bufferedSamples;
    }

    function samplesToMilliseconds(samples) {
        if (sampleRate <= 0 || channels <= 0) {
            return -1;
        }
        return samples / channels / sampleRate * 1000;
    }

    function getTransportMode() {
        if (useSharedAudioWorklet && sharedState) {
            return 'AudioWorklet';
        }
        if (audioWorkletNode) {
            return 'ScriptProcessor';
        }
        return 'Uninitialized';
    }

    function getBufferedMilliseconds() {
        return samplesToMilliseconds(getCurrentBufferedSamples());
    }

    function getStartThresholdMilliseconds() {
        return samplesToMilliseconds(minBufferBeforePlay);
    }

    function getBufferCapacityMilliseconds() {
        return samplesToMilliseconds(bufferCapacity);
    }

    function getEstimatedOutputLatencyMilliseconds() {
        if (!audioContext) {
            return -1;
        }

        let latencySeconds = 0;
        let hasLatency = false;
        if (typeof audioContext.baseLatency === 'number') {
            latencySeconds += audioContext.baseLatency;
            hasLatency = true;
        }
        if (typeof audioContext.outputLatency === 'number') {
            latencySeconds += audioContext.outputLatency;
            hasLatency = true;
        }
        return hasLatency ? latencySeconds * 1000 : -1;
    }

    function getTotalUnderruns() {
        const pendingUnderruns = useSharedAudioWorklet && sharedState ? Atomics.load(sharedState, StateIndex.Underruns) : 0;
        return totalUnderrunCount + pendingUnderruns;
    }

    function getTotalOverflows() {
        const pendingOverflows = useSharedAudioWorklet && sharedState ? Atomics.load(sharedState, StateIndex.Overflows) : 0;
        return totalOverflowCount + pendingOverflows;
    }

    // Public API
    return {
        initialize,
        initializeDirectWrite,
        initializeDirectWriteAudioWorklet,
        queueAudioData,
        resume,
        pause,
        stop,
        cleanup,
        registerLogCallback,
        getTransportMode,
        getBufferedMilliseconds,
        getStartThresholdMilliseconds,
        getBufferCapacityMilliseconds,
        getEstimatedOutputLatencyMilliseconds,
        getTotalUnderruns,
        getTotalOverflows
    };
})();
