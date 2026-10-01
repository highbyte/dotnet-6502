(() => {
    "use strict";
    let stopSession = null;
    let stopAvailabilityObservation = null;

    function observeAvailability(onChanged) {
        stopAvailabilityObservation?.();
        // Match the zoom toolbar: a touchscreen on a mouse-first desktop is
        // insufficient. Keep this independent of window size and user agent.
        const pointer = globalThis.matchMedia?.("(pointer: coarse)");
        const update = () => onChanged(pointer?.matches === true);
        pointer?.addEventListener("change", update);
        stopAvailabilityObservation = () => pointer?.removeEventListener("change", update);
        update();
    }

    function stop() {
        if (stopSession) {
            stopSession();
            stopSession = null;
        }
    }

    function start(onText, onFocusLost) {
        stop();
        // Avalonia owns one HTML input for its canvas text controls. Intercept it
        // only while the emulator's native-keyboard TextBox has Avalonia focus.
        const input = document.querySelector("#out input");
        if (!input) return false;
        let composing = false;
        let focused = true;
        const attributes = {
            autocapitalize: "none", autocorrect: "off", autocomplete: "off",
            spellcheck: "false", inputmode: "text", enterkeyhint: "enter"
        };
        const previous = new Map();
        for (const [name, value] of Object.entries(attributes)) {
            previous.set(name, input.getAttribute(name));
            input.setAttribute(name, value);
        }
        const clear = () => {
            input.value = "";
            input.setSelectionRange(0, 0);
        };
        const send = text => {
            if (focused && text) onText(text);
            clear();
        };
        const handlers = {
            focus() { focused = true; },
            blur() {
                focused = false;
                composing = false;
                clear();
                onFocusLost();
            },
            keydown(event) {
                event.stopImmediatePropagation();
                // Printable keys use native beforeinput/composition rather than
                // Avalonia's synthetic text from keydown (which would duplicate).
                if (composing || event.isComposing) return;
                const text = { Enter: "\r", Backspace: "\b", Escape: "\x1b" }[event.key];
                if (text) {
                    event.preventDefault();
                    send(text);
                }
            },
            keyup(event) { event.stopImmediatePropagation(); },
            beforeinput(event) {
                event.stopImmediatePropagation();
                if (composing || event.isComposing || event.inputType === "insertCompositionText"
                    || event.inputType === "insertFromComposition" || event.inputType === "deleteByComposition")
                    return;
                event.preventDefault();
                const edit = {
                    deleteContentBackward: "\b", insertParagraph: "\r", insertLineBreak: "\r"
                }[event.inputType];
                if (edit) send(edit);
                else if (event.inputType === "insertText" || event.inputType === "insertReplacementText")
                    send(event.data);
                // Paste is handled by its clipboard event. Prevent its following
                // beforeinput from sending the same chunk a second time.
            },
            compositionstart(event) {
                event.stopImmediatePropagation();
                composing = true;
            },
            compositionupdate(event) { event.stopImmediatePropagation(); },
            compositionend(event) {
                event.stopImmediatePropagation();
                composing = false;
                send(event.data);
            },
            input(event) {
                event.stopImmediatePropagation();
                if (!composing) clear();
            },
            paste(event) {
                event.stopImmediatePropagation();
                event.preventDefault();
                send(event.clipboardData?.getData("text/plain"));
            }
        };
        for (const [name, handler] of Object.entries(handlers))
            input.addEventListener(name, handler, true);
        stopSession = () => {
            for (const [name, handler] of Object.entries(handlers))
                input.removeEventListener(name, handler, true);
            for (const [name, value] of previous) {
                if (value === null) input.removeAttribute(name);
                else input.setAttribute(name, value);
            }
            clear();
        };
        return true;
    }

    globalThis.dotnet6502NativeKeyboard = Object.freeze({ start, stop, observeAvailability });
})();
