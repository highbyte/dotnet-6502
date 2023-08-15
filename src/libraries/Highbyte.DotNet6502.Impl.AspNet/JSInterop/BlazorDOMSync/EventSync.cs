// --------------------------------------------------------------------------------
// Synchronous Blazor DOM interop code based on KristofferStrube's Blazor.DOM library.
// https://github.com/KristofferStrube/Blazor.DOM
// --------------------------------------------------------------------------------

using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebIDLSync;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;

public class EventSync : BaseJSWrapperSync, IJSWrapperSync<EventSync>, IJSWrapperSync
{
    public static EventSync Create(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference)
    {
        return new EventSync(helper, jSRuntime, jSReference);
    }

    public static EventSync Create(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, string type, EventInit? eventInitDict = null)
    {
        return new EventSync(
            helper,
            jSRuntime,
            helper.Invoke<IJSInProcessObjectReference>(
                "constructEvent",
                new object[2] { type, eventInitDict }));
    }

    protected EventSync(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference)
        : base(helper, jSRuntime, jSReference)
    {
    }

    public new string GetType()
    {
        return _helper.Invoke<string>("getAttribute", new object[2] { JSReference, "type" });
    }

    public EventTargetSync? GetTarget()
    {
        var iJSObjectReference = _helper.Invoke<IJSInProcessObjectReference>("getAttribute", new object[2] { JSReference, "target" });
        return iJSObjectReference == null ? null : EventTargetSync.Create(_helper, JSRuntime, iJSObjectReference);
    }

    public EventTargetSync? GetCurrentTarget()
    {
        var iJSObjectReference = _helper.Invoke<IJSInProcessObjectReference>("getAttribute", new object[2] { JSReference, "currentTarget" });
        return iJSObjectReference == null ? null : EventTargetSync.Create(_helper, JSRuntime, iJSObjectReference);
    }

    public EventTargetSync[] ComposedPath()
    {
        IJSObjectReference jSArray = JSReference.Invoke<IJSInProcessObjectReference>("composedPath", Array.Empty<object>());
        var length = _helper.Invoke<int>("getAttribute", new object[2] { jSArray, "length" });
        return Enumerable.Range(0, length).Select(delegate (int i)
        {
            var jSRuntime = JSRuntime;
            var helper = _helper;
            var jsReference = helper.Invoke<IJSInProcessObjectReference>("getAttribute", new object[2] { jSArray, i });
            return EventTargetSync.Create(helper, jSRuntime, jsReference);
        }
        ).ToArray();
    }

    public EventPhase GetEventPhase()
    {
        return _helper.Invoke<EventPhase>("getAttribute", new object[2] { JSReference, "eventPhase" });
    }

    public void StopPropagationAsync()
    {
        JSReference.InvokeVoid("stopPropagation");
    }

    public void StopImmediatePropagation()
    {
        JSReference.InvokeVoid("stopImmediatePropagation");
    }

    public bool GetBubbles()
    {
        return _helper.Invoke<bool>("getAttribute", new object[2] { JSReference, "bubbles" });
    }

    public bool GetCancelable()
    {
        return _helper.Invoke<bool>("getAttribute", new object[2] { JSReference, "cancelable" });
    }

    public void PreventDefault()
    {
        JSReference.InvokeVoid("preventDefault");
    }

    public bool GetDefaultPrevented()
    {
        return _helper.Invoke<bool>("getAttribute", new object[2] { JSReference, "defaultPrevented" });
    }

    public bool GetComposed()
    {
        return _helper.Invoke<bool>("getAttribute", new object[2] { JSReference, "composed" });
    }

    public bool GetIsTrusted()
    {
        return _helper.Invoke<bool>("getAttribute", new object[2] { JSReference, "isTrusted" });
    }

    public double GetTimeStamp()
    {
        return _helper.Invoke<double>("getAttribute", new object[2] { JSReference, "timeStamp" });
    }
}
