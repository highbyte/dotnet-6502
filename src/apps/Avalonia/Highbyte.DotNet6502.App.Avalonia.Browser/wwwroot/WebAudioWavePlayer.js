// WebAudioWavePlayer.js
// JavaScript module for WebAudio playback of audio from .NET NAudio

export const WebAudioWavePlayer = (() => {
    let audioContext = null;
    let audioWorkletNode = null;
    let sampleRate = 44100;
    let channels = 1;
    let bufferSize = 4096;
    let audioQueue = [];
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
        audioQueue = [];
        isPlaying = false;

        // Create AudioContext if it doesn't exist
        if (!audioContext) {
            audioContext = new (window.AudioContext || window.webkitAudioContext)({
                sampleRate: sampleRate,
                latencyHint: 'interactive' // Low latency for real-time audio
            });
        }

        // Resume context if it was suspended (browser autoplay policy)
        if (audioContext.state === 'suspended') {
            audioContext.resume();
        }

        console.log(`WebAudioWavePlayer initialized: ${sampleRate}Hz, ${channels} channel(s), buffer size: ${bufferSize}`);

        // Use ScriptProcessorNode as fallback (AudioWorklet requires separate file and HTTPS)
        // For production, consider implementing AudioWorklet for better performance
        setupScriptProcessor();
    }

    /**
     * Set up ScriptProcessorNode for audio processing
     * This is the fallback method that works without additional setup
     */
    function setupScriptProcessor() {
        // Clean up existing node
        if (audioWorkletNode) {
            audioWorkletNode.disconnect();
            audioWorkletNode = null;
        }

        // Create ScriptProcessorNode
        // Note: ScriptProcessorNode is deprecated but widely supported and simpler to set up
        // bufferSize must be power of 2 between 256 and 16384
        const validBufferSize = Math.pow(2, Math.ceil(Math.log2(Math.min(16384, Math.max(256, bufferSize)))));
        
        audioWorkletNode = audioContext.createScriptProcessor(
            validBufferSize,
            0, // No input channels
            channels // Output channels
        );

        // Process audio data
        audioWorkletNode.onaudioprocess = (audioProcessingEvent) => {
            const outputBuffer = audioProcessingEvent.outputBuffer;
            const bufferLength = outputBuffer.length;

            // Get audio data from queue
            if (audioQueue.length > 0 && isPlaying) {
                const audioData = audioQueue.shift();
                
                // Fill output buffer
                for (let channel = 0; channel < channels; channel++) {
                    const outputData = outputBuffer.getChannelData(channel);
                    
                    for (let i = 0; i < bufferLength; i++) {
                        const sampleIndex = channels === 1 ? i : (i * channels + channel);
                        outputData[i] = sampleIndex < audioData.length ? audioData[sampleIndex] : 0;
                    }
                }
            } else {
                // No data available or not playing - output silence
                for (let channel = 0; channel < channels; channel++) {
                    const outputData = outputBuffer.getChannelData(channel);
                    outputData.fill(0);
                }
            }
        };

        // Connect to destination (speakers)
        audioWorkletNode.connect(audioContext.destination);
        
        console.log(`ScriptProcessorNode created with buffer size: ${validBufferSize}`);
    }

    /**
     * Queue audio data for playback
     * @param {Uint8Array} audioDataBytes - Audio samples as byte array (little-endian float32)
     * @param {number} sampleCount - Number of float samples in the array
     */
    function queueAudioData(audioDataBytes, sampleCount) {
        if (!audioContext) {
            console.error('WebAudioWavePlayer not initialized');
            return;
        }

        // Convert byte array to Float32Array
        // Create a view over the byte array and interpret it as float32
        const floatArray = new Float32Array(audioDataBytes.buffer, audioDataBytes.byteOffset, sampleCount);
        
        // Convert to regular JavaScript array for easier manipulation
        const dataArray = Array.from(floatArray);
        audioQueue.push(dataArray);

        // Limit queue size to prevent memory issues
        const maxQueueSize = 10;
        if (audioQueue.length > maxQueueSize) {
            console.warn(`Audio queue overflow (${audioQueue.length} items), dropping oldest buffer`);
            audioQueue.shift();
        }

        // Resume context if it was suspended
        if (audioContext.state === 'suspended') {
            audioContext.resume();
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
     * Stop audio playback and clear queue
     */
    function stop() {
        isPlaying = false;
        audioQueue = [];
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
