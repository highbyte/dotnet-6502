
// ----------
// WebAudio
// ----------
//export function getAttribute(object, attribute) { return object[attribute]; }

export function constructAudioContext(contextOptions = null) {
    return new AudioContext(contextOptions)
}

export function constructOcillatorNode(context, options = null) {
    return new OscillatorNode(context, options);
}

export function constructGainNode(context, options = null) {
    return new GainNode(context, options);
}

export function constructPeriodicWave(context, options = null) {
    return new PeriodicWave(context, options);
}

export function constructWaveShaperNode(context, options = null) {
    return new WaveShaperNode(context, options);
}

export function constructAudioBuffer(options = null) {
    return new AudioBuffer(options);
}

export function constructAudioBufferSourceNode(context, options = null) {
    return new AudioBufferSourceNode(context, options);
}


// ----------
// DOM
// ----------
//export function getAttribute(object, attribute) { return object[attribute]; }

// export function setAttribute(object, attribute, value) { object[attribute] = value; }

export function getJSReference(element) { return element.valueOf(); }

export function addEventListener(target, type, eventListener = null, options = null) {
    target.addEventListener(type, eventListener, options)
}

export function removeEventListener(target, type, eventListener = null, options) {
    target.removeEventListener(type, eventListener, options)
}

export function constructEventListener() {
    return {};
}

export function registerEventHandlerAsync(objRef, jSInstance) {
    jSInstance.handleEvent = (e) => objRef.invokeMethodAsync("HandleEventAsync", DotNet.createJSObjectReference(e))
}

export function constructEvent(type, eventInitDict = null) {
    return new Event(type, eventInitDict);
}

export function constructCustomEvent(type, eventInitDict = null) {
    return new CustomEvent(type, eventInitDict);
}

export function constructEventTarget() { return new EventTarget(); }

export function constructAbortController() { return new AbortController(); }

// ----------
// IDL
// ----------
//export function getAttribute(object, attribute) { return object[attribute]; }

export function forEachWithNoArguments(jSReference, callbackObjRef) {
    jSReference.forEach(() => callbackObjRef.invokeMethodAsync('InvokeCallback'))
}

export function forEachWithOneArgument(jSReference, callbackObjRef) {
    jSReference.forEach((value) => callbackObjRef.invokeMethodAsync('InvokeCallback', DotNet.createJSObjectReference(value)))
}

export function forEachWithTwoArguments(jSReference, callbackObjRef) {
    jSReference.forEach((value, key) => callbackObjRef.invokeMethodAsync('InvokeCallback', DotNet.createJSObjectReference(value, key)))
}

// https://javascriptweblog.wordpress.com/2011/08/08/fixing-the-javascript-typeof-operator/
export function valuePropertiesType(obj) {
    return ({}).toString.call(obj.value).match(/\s([a-z|A-Z]+)/)[1].toLowerCase();
}

export function valuePropertiesValue(obj) {
    return obj.value;
}

// ----------
// Common helpers and built-in JavaScript types
// ----------
export function getAttribute(object, attribute) { return object[attribute]; }
export function setAttribute(object, attribute, value) { object[attribute] = value; }
export function callMethod(object, method, args) { return object[method](args); }

export function getArrayValue(object, index) { return object[index]; }
export function setArrayValue(object, index, value) { object[index] = value; }

export function constructFloat32Array() {
    return new Float32Array();
}

export function getAttributeFloat32Array(object, attribute) {
    var float32Array = object[attribute];
    return Array.from(float32Array);
}

export function setAttributeFloat32Array(object, attribute, value) {
    var float32array = new Float32Array(value);
    object[attribute] = float32array;
}

export function callMethodReturnFloat32Array(object, method, args) {
    var float32Array = object[method](args);
    return Array.from(float32Array);
}

export function callMethodVoidFloat32Array(object, method, floatArray, args) {
    var float32Array = new Float32Array(floatArray);
    object[method](float32Array, args);
}
