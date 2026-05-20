namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Marker interface for the target of an audio pipeline — a host audio backend that consumes the
/// audio a system produces.
///
/// Audio counterpart of <see cref="Rendering.IRenderTarget"/>. Each style of target
/// (<c>IAudioCommandTarget</c>, <c>IAudioSampleTarget</c>) implements this interface, with
/// different capabilities that must be matched to a compatible <see cref="IAudioSource"/> via
/// <see cref="AudioProviderTargetMap"/>.
/// </summary>
public interface IAudioTarget
{
    string Name { get; }
}
