// --------------------------------------------------------------------------------
// Synchronous Blazor DOM interop code based on KristofferStrube's Blazor.DOM library.
// https://github.com/KristofferStrube/Blazor.DOM
// --------------------------------------------------------------------------------

using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebIDLSync;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync
{
    public class EventTargetSync : BaseJSWrapperSync
    {
        public static EventTargetSync Create(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference)
        {
            return new EventTargetSync(helper, jSRuntime, jSReference);
        }
        protected EventTargetSync(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference)
            : base(helper, jSRuntime, jSReference)
        {
        }

        public void AddEventListener<TEvent>(string type, EventListener<TEvent>? callback, AddEventListenerOptions? options = null) where TEvent : EventSync, IJSWrapperSync<TEvent>
        {
            _helper.InvokeVoid("addEventListener", JSReference, type, callback?.JSReference, options);
        }

        public void RemoveEventListener<TEvent>(string type, EventListener<TEvent>? callback, EventListenerOptions? options = null) where TEvent : EventSync, IJSWrapperSync<TEvent>
        {
            _helper.InvokeVoid("removeEventListener", JSReference, type, callback?.JSReference, options);
        }

        public void RemoveEventListener<TEvent>(EventListener<TEvent>? callback, EventListenerOptions? options = null) where TEvent : EventSync, IJSWrapperSync<TEvent>
        {
            _helper.InvokeVoid("removeEventListener", JSReference, typeof(TEvent)!.Name, callback?.JSReference, options);
        }

        public bool DispatchEvent(EventSync eventInstance)
        {
            //return JSObjectReferenceExtensions.InvokeAsync<bool>(base.JSReference, "dispatchEvent", new object[1] { eventInstance.JSReference }).Result;
            return JSReference.Invoke<bool>("dispatchEvent", new object[1] { eventInstance.JSReference });
        }
    }
}
