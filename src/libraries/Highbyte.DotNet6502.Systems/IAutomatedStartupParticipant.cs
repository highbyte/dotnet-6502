namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Optional per-system participant in the automated-startup lifecycle. Contributed by an
/// <c>App.&lt;Tech&gt;.Shell.&lt;System&gt;</c> shell plugin and resolved by <see cref="SystemName"/>.
/// When no participant is registered for the selected system, the system needs no special
/// automated-startup handling.
/// </summary>
/// <remarks>
/// See <c>docs/automated-startup-abstraction.md</c>. Methods are intentionally purpose-specific
/// and optional (default interface implementations) so further lifecycle hook points can be added
/// without breaking existing implementers.
/// </remarks>
public interface IAutomatedStartupParticipant
{
    /// <summary>The system this participant handles (matched against the selected system name).</summary>
    string SystemName { get; }

    /// <summary>
    /// Pre-start gate. Invoked by <see cref="AutomatedStartupHandler"/> after the system and
    /// variant have been selected, but before the system is started. May be interactive (show
    /// prompts / dialogs) and asynchronous.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the system is ready to start; <see langword="false"/> to abort
    /// automated startup (the host then falls back to its normal UI).
    /// </returns>
    /// <remarks>
    /// Contract: runs on the thread the host requires (the caller guarantees this); must not call
    /// <see cref="IHostApp.Start"/> itself; and must not terminate the process. The default
    /// implementation reports the system as ready.
    /// </remarks>
    Task<bool> EnsureReadyForStartAsync(IHostApp hostApp, AutomatedStartupRequest request)
        => Task.FromResult(true);

    /// <summary>
    /// Post-ready action. Invoked by <see cref="AutomatedStartupHandler"/> after the system has
    /// been started and (if requested) reported ready. May be asynchronous. Used for
    /// system-specific automation driven by <see cref="AutomatedStartupRequest.ExtraParameters"/>
    /// (e.g. the C64 participant pasting BASIC source).
    /// </summary>
    /// <remarks>
    /// The system is genuinely <em>ready</em> only when
    /// <see cref="AutomatedStartupRequest.WaitForSystemReady"/> was set; otherwise it has merely
    /// been started. A participant needing a true ready state should check that flag. The default
    /// implementation does nothing.
    /// </remarks>
    Task OnSystemReadyAsync(
        IHostApp hostApp, AutomatedStartupRequest request, AutomatedStartupContext context)
        => Task.CompletedTask;

    /// <summary>
    /// Gives a system-specific participant a chance to wait for additional post-ready settling
    /// before an automated PRG load mutates the running machine state.
    /// </summary>
    Task BeforePrgLoadAsync(
        IHostApp hostApp,
        AutomatedStartupRequest request,
        AutomatedStartupContext context)
        => Task.CompletedTask;

    /// <summary>
    /// Gives a system-specific participant a chance to finalize PRG loading after the generic
    /// loader has copied the program bytes into memory.
    /// </summary>
    /// <remarks>
    /// Used for systems that need additional state updates after a direct memory load, such as
    /// BASIC memory pointers that the normal machine LOAD command would have initialized.
    /// </remarks>
    Task OnPrgLoadedAsync(
        IHostApp hostApp,
        AutomatedStartupRequest request,
        AutomatedStartupContext context,
        ushort loadAddress,
        ushort fileLength)
        => Task.CompletedTask;

    /// <summary>
    /// Gives a system-specific participant a chance to start a freshly loaded PRG in a
    /// system-appropriate way after automated startup has completed.
    /// </summary>
    /// <remarks>
    /// Return <see langword="true"/> when the participant handled the launch (for example by
    /// queueing a BASIC <c>run</c> command). Return <see langword="false"/> to let the generic
    /// fallback decide how to start the program from <paramref name="loadAddress"/>.
    /// </remarks>
    Task<bool> TryRunLoadedProgramAsync(
        IHostApp hostApp,
        AutomatedStartupRequest request,
        AutomatedStartupContext context,
        ushort loadAddress)
        => Task.FromResult(false);
}
