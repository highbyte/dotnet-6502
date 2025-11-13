RenderFrame (and related classes)
- Make single layer implementation also use uint array instead of byte array to make it consistent with multi layer implementation.

CommonFrameSource
- owners[i] = new NonOwningMemoryOwner<uint>(layerBuffers[i]);
- The comment claims "The rasterizer's ReaderWriterLockSlim ensures thread-safe access," but NonOwningMemoryOwner creates a mutable Memory<T> wrapper that persists beyond the lock's scope. The RenderFrame created here can be accessed later when the lock is no longer held, potentially creating a race condition with FlipBuffers().
- The zero-copy approach requires the consumer to respect the read-only nature of the buffers, but there's no enforcement mechanism once the Memory<T> escapes the lock scope.
- Suggestion below? May introduce an extra copy. Is this not necessary if ReadOnlyMemory all the way from RenderFrame?
                var buffer = _poolUint.Rent(layerBuffers[i].Length);
                layerBuffers[i].Span.CopyTo(buffer.Memory.Span);
                owners[i] = buffer;


NonOwningMemoryOwner<T>:
- The use of MemoryMarshal.AsMemory(_memory) to convert ReadOnlyMemory<T> to Memory<T> is unsafe and violates memory safety guarantees. This circumvents the read-only contract and could lead to unintended mutations of data that should be immutable.
- Consider using IMemoryOwner<T> with actual ownership semantics, or if zero-copy is required, pass ReadOnlyMemory<T> directly through the API rather than wrapping it in a mutable Memory<T> interface.


Control C64 joystick with Controller
- Is there built-in support in Avalonia for both desktop and browser?

Audio:
- Implement via existing NAudio code in AvaDesktop app
- Maybe for future in Avalonia Browser: Investigate audio/synth alternatives that is compatible with WebAssembly. Preferably without having to write browser WebAudio JS API interop code.


