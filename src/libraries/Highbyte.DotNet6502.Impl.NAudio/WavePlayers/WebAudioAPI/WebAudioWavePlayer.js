// WebAudioWavePlayer.js
// JavaScript module for WebAudio playback of audio from .NET NAudio

export const WebAudioWavePlayer = (() => {
    let audioContext = null;
    let audioWorkletNode = null;
    let sampleRate = 44100;
    let channels = 1;
    let bufferSize = 4096;
    
    // Ring buffer for continuous audio samples instead of array of buffers
    let sampleBuffer = null;
    let writePosition = 0;
    let readPosition = 0;
    let bufferedSamples = 0;
    let bufferCapacity = 0;
    
    // Minimum samples needed before starting playback (to avoid immediate underrun)
    let minBufferBeforePlay = 0;
    let hasEnoughToStart = false;
    
    // Configurable settings (set from C#)
    let scriptProcessorBufferSize = 2048;
    let statsIntervalMs = 10000;
    
    // Adaptive buffering: track underruns and increase buffer if needed
    let underrunCount = 0;
    let overflowCount = 0;
    let lastStatsTime = 0;
    const DEFAULT_STATS_INTERVAL_MS = 10000; // Log stats every 10 seconds
    
    let isPlaying = false;

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
        sampleRate = sRate;
        channels = numChannels;
        bufferSize = bufSize;
        scriptProcessorBufferSize = scriptProcBufferSize;
        statsIntervalMs = statsIntMs;
        isPlaying = false;
        hasEnoughToStart = false;
        underrunCount = 0;
        overflowCount = 0;
        lastStatsTime = performance.now();

        // Ring buffer capacity based on multiplier from C#
        bufferCapacity = Math.ceil(bufSize * channels * ringBufferCapacityMultiplier);
        sampleBuffer = new Float32Array(bufferCapacity);
        writePosition = 0;
        readPosition = 0;
        bufferedSamples = 0;
        
        // Start playback threshold based on multiplier from C#
        minBufferBeforePlay = Math.ceil(bufSize * channels * minBufferBeforePlayMultiplier);

        // Close existing AudioContext if it exists (sample rate may have changed)
        if (audioContext) {
            if (audioWorkletNode) {
                audioWorkletNode.disconnect();
                audioWorkletNode = null;
            }
            audioContext.close();
            audioContext = null;
            console.log('WebAudioWavePlayer existing audioContext closed');
        }

        // Create new AudioContext with the specified sample rate
        audioContext = new (window.AudioContext || window.webkitAudioContext)({
            sampleRate: sampleRate,
            latencyHint: 'interactive' // Low latency for real-time audio
        });
        console.log(`WebAudioWavePlayer audioContext created with sample rate: ${audioContext.sampleRate}`);

        // Resume context if it was suspended (browser autoplay policy)
        if (audioContext.state === 'suspended') {
            audioContext.resume();
            console.log('WebAudioWavePlayer audioContext resumed');
        }

        const bufferCapacityMs = (bufferCapacity / channels / sampleRate * 1000).toFixed(1);
        const minBufferMs = (minBufferBeforePlay / channels / sampleRate * 1000).toFixed(1);
        const desiredLatencyMs = (bufSize / sampleRate * 1000).toFixed(1);
        console.log(`WebAudioWavePlayer initialized:`);
        console.log(`  Sample rate: ${sampleRate}Hz, Channels: ${channels}`);
        console.log(`  C# buffer: ${bufSize} samples (~${desiredLatencyMs}ms)`);
        console.log(`  Ring buffer: ${bufferCapacity} samples (~${bufferCapacityMs}ms) [${ringBufferCapacityMultiplier}x multiplier]`);
        console.log(`  Start threshold: ${minBufferBeforePlay} samples (~${minBufferMs}ms) [${minBufferBeforePlayMultiplier}x multiplier]`);
        console.log(`  ScriptProcessor buffer: ${scriptProcessorBufferSize} samples`);
        console.log(`  Stats interval: ${statsIntervalMs}ms${statsIntervalMs === 0 ? ' (disabled)' : ''}`);

        // Use ScriptProcessorNode as fallback (AudioWorklet requires separate file and HTTPS)
        setupScriptProcessor();

        console.log(`WebAudioWavePlayer initialize exit`);
    }

    /**
     * Set up ScriptProcessorNode for audio processing
     */
    function setupScriptProcessor() {
        console.log(`WebAudioWavePlayer setupScriptProcessor start.`);

        // Clean up existing node
        if (audioWorkletNode) {
            audioWorkletNode.disconnect();
            audioWorkletNode = null;
            console.log('WebAudioWavePlayer existing ScriptProcessorNode disconnected');
        }

        // Create ScriptProcessorNode - use the configured buffer size, clamped to valid range
        // Buffer size must be power of 2 between 256 and 16384
        const clampedSize = Math.min(16384, Math.max(256, scriptProcessorBufferSize));
        const validBufferSize = Math.pow(2, Math.round(Math.log2(clampedSize)));
        
        audioWorkletNode = audioContext.createScriptProcessor(
            validBufferSize,
            0, // No input channels
            channels // Output channels
        );
        
        const processorLatencyMs = (validBufferSize / sampleRate * 1000).toFixed(1);
        console.log(`WebAudioWavePlayer ScriptProcessorNode created with buffer size: ${validBufferSize} (~${processorLatencyMs}ms)`);

        // Process audio data
        audioWorkletNode.onaudioprocess = (audioProcessingEvent) => {
            const outputBuffer = audioProcessingEvent.outputBuffer;
            const bufferLength = outputBuffer.length;
            const samplesNeeded = bufferLength * channels;

            // Wait until we have enough initial buffer before starting playback
            if (!hasEnoughToStart) {
                // Output silence while waiting for buffer to fill
                for (let channel = 0; channel < channels; channel++) {
                    const outputData = outputBuffer.getChannelData(channel);
                    outputData.fill(0);
                }
                return;
            }

            if (isPlaying && bufferedSamples >= samplesNeeded) {
                // We have enough samples - read from ring buffer
                for (let channel = 0; channel < channels; channel++) {
                    const outputData = outputBuffer.getChannelData(channel);
                    
                    for (let i = 0; i < bufferLength; i++) {
                        // For interleaved audio: sample index = frame * channels + channel
                        const ringBufferIndex = (readPosition + i * channels + channel) % bufferCapacity;
                        outputData[i] = sampleBuffer[ringBufferIndex];
                    }
                }
                
                // Advance read position
                readPosition = (readPosition + samplesNeeded) % bufferCapacity;
                bufferedSamples -= samplesNeeded;
            } else {
                // Not enough data or not playing - output silence
                for (let channel = 0; channel < channels; channel++) {
                    const outputData = outputBuffer.getChannelData(channel);
                    outputData.fill(0);
                }
                
                // Track underruns (but not during initial buffering or when paused)
                if (isPlaying && hasEnoughToStart) {
                    underrunCount++;
                }
            }
            
            // Periodic stats logging
            logStatsIfNeeded();
        };

        // Connect to destination (speakers)
        audioWorkletNode.connect(audioContext.destination);
        console.log('WebAudioWavePlayer ScriptProcessorNode connected to destination');
        
        console.log(`WebAudioWavePlayer setupScriptProcessor exit.`);
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
                console.log(`WebAudioWavePlayer stats: underruns=${underrunCount}, overflows=${overflowCount}, current buffer=${bufferLevelMs}ms`);
            }
            underrunCount = 0;
            overflowCount = 0;
            lastStatsTime = now;
        }
    }

    /**
     * Queue audio data for playback
     * @param {Uint8Array} audioDataBytes - Audio samples as byte array (little-endian float32)
     * @param {number} sampleCount - Number of float samples in the array
     */
    function queueAudioData(audioDataBytes, sampleCount) {
        if (!audioContext || !sampleBuffer) {
            console.error('WebAudioWavePlayer not initialized');
            return;
        }

        // Convert byte array to Float32Array
        const floatArray = new Float32Array(audioDataBytes.buffer, audioDataBytes.byteOffset, sampleCount);
        
        // Check if we have room in the ring buffer
        const availableSpace = bufferCapacity - bufferedSamples;
        
        if (sampleCount > availableSpace) {
            // Buffer overflow - drop oldest samples to make room
            const samplesToDiscard = sampleCount - availableSpace;
            readPosition = (readPosition + samplesToDiscard) % bufferCapacity;
            bufferedSamples -= samplesToDiscard;
            overflowCount++;
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
            const bufferLevelMs = (bufferedSamples / channels / sampleRate * 1000).toFixed(1);
            console.log(`WebAudioWavePlayer starting playback with ${bufferedSamples} samples (~${bufferLevelMs}ms) buffered`);
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
        if (audioContext && audioContext.state === 'suspended') {
            audioContext.resume();
        }
        isPlaying = true;
        console.log('WebAudioWavePlayer resumed');
    }

    /**
     * Pause audio playback
     */
    function pause() {
        isPlaying = false;
        console.log('WebAudioWavePlayer paused');
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
        if (sampleBuffer) {
            sampleBuffer.fill(0);
        }
        console.log('WebAudioWavePlayer stopped');
    }

    /**
     * Cleanup resources
     */
    function cleanup() {
        stop();
        
        if (audioWorkletNode) {
            audioWorkletNode.disconnect();
            audioWorkletNode = null;
        }

        if (audioContext) {
            audioContext.close();
            audioContext = null;
        }
        
        sampleBuffer = null;
        
        console.log('WebAudioWavePlayer cleaned up');
    }

    // Public API
    return {
        initialize,
        queueAudioData,
        resume,
        pause,
        stop,
        cleanup
    };
})();
