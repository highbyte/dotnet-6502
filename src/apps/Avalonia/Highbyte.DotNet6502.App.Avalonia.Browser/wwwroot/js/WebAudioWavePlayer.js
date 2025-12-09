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
    
    let isPlaying = false;

    /**
     * Initialize WebAudio context and audio worklet
     * @param {number} sRate - Sample rate (e.g., 44100)
     * @param {number} numChannels - Number of channels (1 = mono, 2 = stereo)
     * @param {number} bufSize - Buffer size in samples
     */
    function initialize(sRate, numChannels, bufSize) {
        sampleRate = sRate;
        channels = numChannels;
        bufferSize = bufSize;
        isPlaying = false;
        hasEnoughToStart = false;

        // Create a ring buffer that can hold ~200ms of audio (reduced from 500ms for lower latency)
        bufferCapacity = Math.ceil(sampleRate * 0.2) * channels;
        sampleBuffer = new Float32Array(bufferCapacity);
        writePosition = 0;
        readPosition = 0;
        bufferedSamples = 0;
        
        // Start playback after buffering ~50ms of audio (reduced for lower latency)
        minBufferBeforePlay = Math.ceil(sampleRate * 0.05) * channels;

        // Create AudioContext if it doesn't exist
        if (!audioContext) {
            audioContext = new (window.AudioContext || window.webkitAudioContext)({
                sampleRate: sampleRate,
                latencyHint: 'interactive' // Low latency for real-time audio
            });
            console.log(`WebAudioWavePlayer audioContext created with sample rate: ${audioContext.sampleRate}`);
        }

        // Resume context if it was suspended (browser autoplay policy)
        if (audioContext.state === 'suspended') {
            audioContext.resume();
            console.log('WebAudioWavePlayer audioContext resumed');
        }

        console.log(`WebAudioWavePlayer initialized: ${sampleRate}Hz, ${channels} channel(s), buffer size: ${bufferSize}, ring buffer capacity: ${bufferCapacity} samples, min buffer before play: ${minBufferBeforePlay} samples`);

        // Use ScriptProcessorNode as fallback (AudioWorklet requires separate file and HTTPS)
        // For production, consider implementing AudioWorklet for better performance
        setupScriptProcessor();

        console.log(`WebAudioWavePlayer initialize exit`);
    }

    /**
     * Set up ScriptProcessorNode for audio processing
     * This is the fallback method that works without additional setup
     * Note: ScriptProcessorNode is deprecated but still widely supported.
     * The deprecation warning in the console can be safely ignored.
     * For better performance, consider implementing AudioWorklet in the future.
     */
    function setupScriptProcessor() {
        console.log(`WebAudioWavePlayer setupScriptProcessor start.`);

        // Clean up existing node
        if (audioWorkletNode) {
            audioWorkletNode.disconnect();
            audioWorkletNode = null;
            console.log('WebAudioWavePlayer existing ScriptProcessorNode disconnected');
        }

        // Create ScriptProcessorNode with smaller buffer for lower latency
        // Use 2048 samples (~46ms at 44100Hz) as a good balance between latency and stability
        // Smaller buffers = lower latency but higher CPU usage and risk of glitches
        const targetBufferSize = 2048;
        const validBufferSize = Math.pow(2, Math.ceil(Math.log2(Math.min(16384, Math.max(256, targetBufferSize)))));
        
        audioWorkletNode = audioContext.createScriptProcessor(
            validBufferSize,
            0, // No input channels
            channels // Output channels
        );
        console.log(`WebAudioWavePlayer ScriptProcessorNode created with buffer size: ${validBufferSize}`);

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
            }
        };

        // Connect to destination (speakers)
        audioWorkletNode.connect(audioContext.destination);
        console.log('WebAudioWavePlayer ScriptProcessorNode connected to destination');
        
        console.log(`WebAudioWavePlayer setupScriptProcessor exit.`);
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
        // Create a view over the byte array and interpret it as float32
        const floatArray = new Float32Array(audioDataBytes.buffer, audioDataBytes.byteOffset, sampleCount);
        
        // Check if we have room in the ring buffer
        const availableSpace = bufferCapacity - bufferedSamples;
        
        if (sampleCount > availableSpace) {
            // Buffer overflow - drop oldest samples to make room
            const samplesToDiscard = sampleCount - availableSpace;
            readPosition = (readPosition + samplesToDiscard) % bufferCapacity;
            bufferedSamples -= samplesToDiscard;
            console.warn(`Audio buffer overflow, discarded ${samplesToDiscard} samples`);
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
            console.log(`WebAudioWavePlayer starting playback with ${bufferedSamples} samples buffered`);
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
