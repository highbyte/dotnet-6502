// --------------------------------------------------------------------------------
// Synchronous Blazor DOM interop code based on KristofferStrube's Blazor.DOM library.
// https://github.com/KristofferStrube/Blazor.DOM
// --------------------------------------------------------------------------------

using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebIDLSync;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;

public class EventListener<TEvent> : BaseJSWrapperSync where TEvent : EventSync, IJSWrapperSync<TEvent>
{
    private Action<TEvent>? _callback;

    public static EventListener<TEvent> Create(IJSInProcessObjectReference helper, IJSRuntime jsRuntime, Action<TEvent> callback)
    {
        var iJSObjectReference = helper.Invoke<IJSInProcessObjectReference>("constructEventListener", Array.Empty<object>());
        var eventListener = new EventListener<TEvent>(helper, jsRuntime, iJSObjectReference)
        {
            _callback = callback
        };
        helper.InvokeVoid("registerEventHandlerAsync", DotNetObjectReference.Create(eventListener), iJSObjectReference);
        return eventListener;
    }

    protected EventListener(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference)
        : base(helper, jSRuntime, jSReference)
    {
    }

    [JSInvokable]
    public Task HandleEventAsync(IJSInProcessObjectReference jSObjectReference)
    {
        _callback?.Invoke(TEvent.Create(_helper, JSRuntime, jSObjectReference));
        return Task.CompletedTask;
    }
}
