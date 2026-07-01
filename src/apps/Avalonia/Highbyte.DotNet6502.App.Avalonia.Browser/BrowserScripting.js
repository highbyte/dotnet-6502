export function getLocalStorageKeys(prefix) {
    const keys = [];
    for (let i = 0; i < localStorage.length; i++) {
        const key = localStorage.key(i);
        if (key?.startsWith(prefix))
            keys.push(key.substring(prefix.length));
    }
    return JSON.stringify(keys);
}

export function pickLocalFilesAsBase64(accept, allowMultiple) {
    return new Promise((resolve) => {
        const input = document.createElement("input");
        input.type = "file";
        input.multiple = !!allowMultiple;
        input.style.position = "fixed";
        input.style.left = "-10000px";
        input.style.top = "-10000px";

        if (accept)
            input.accept = accept;

        let settled = false;

        const finish = (value) => {
            if (settled)
                return;

            settled = true;
            window.removeEventListener("focus", onFocus, true);
            input.removeEventListener("change", onChange);
            input.removeEventListener("cancel", onCancel);
            input.remove();
            resolve(value ?? "");
        };

        const supportsCancelEvent = "oncancel" in input;

        const onCancel = () => finish("");

        const onFocus = () => {
            if (supportsCancelEvent)
                return;

            // Some browsers do not fire a cancel event for file inputs. When the OS file picker
            // closes without a selected file, focus returns to the window. Delay long enough for
            // browsers that restore focus before dispatching the "change" event on real selection;
            // otherwise a valid file pick can race with this fallback and be reported as cancel.
            setTimeout(() => {
                if (!settled && (!input.files || input.files.length === 0))
                    finish("");
            }, 1000);
        };

        const onChange = async () => {
            if (!input.files || input.files.length === 0) {
                finish("");
                return;
            }

            try {
                const files = await Promise.all(Array.from(input.files).map(async file => {
                    const bytes = new Uint8Array(await file.arrayBuffer());
                    return {
                        name: file.name,
                        base64: bytesToBase64(bytes)
                    };
                }));
                finish(JSON.stringify(files));
            } catch (error) {
                console.error("Failed to read local file", error);
                finish("");
            }
        };

        input.addEventListener("change", onChange);
        if (supportsCancelEvent)
            input.addEventListener("cancel", onCancel);
        else
            window.addEventListener("focus", onFocus, true);
        document.body.appendChild(input);
        input.click();
    });
}

// Triggers a browser download of binary data (e.g. an emulator snapshot). Avalonia's browser
// StorageProvider save uses the File System Access API (Chromium-only), so a Blob download is used
// instead for universal browser support.
export function downloadFileFromBase64(name, base64, mime) {
    const bytes = base64ToBytes(base64);
    const blob = new Blob([bytes], { type: mime || "application/octet-stream" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = name || "download";
    a.style.display = "none";
    document.body.appendChild(a);
    a.click();
    // Defer cleanup so the click is processed before the object URL is revoked.
    setTimeout(() => {
        a.remove();
        URL.revokeObjectURL(url);
    }, 0);
}

function base64ToBytes(base64) {
    const binary = atob(base64);
    const len = binary.length;
    const bytes = new Uint8Array(len);
    for (let i = 0; i < len; i++)
        bytes[i] = binary.charCodeAt(i);
    return bytes;
}

function bytesToBase64(bytes) {
    const chunkSize = 0x8000;
    let binary = "";

    for (let i = 0; i < bytes.length; i += chunkSize)
        binary += String.fromCharCode(...bytes.subarray(i, i + chunkSize));

    return btoa(binary);
}

export function getScriptsFromLocalStorage(prefix) {
    const results = [];
    for (let i = 0; i < localStorage.length; i++) {
        const key = localStorage.key(i);
        if (key?.startsWith(prefix)) {
            const content = localStorage.getItem(key);
            if (content !== null)
                results.push({ name: key.substring(prefix.length), content: content });
        }
    }
    return JSON.stringify(results);
}

// Detects the browser keyboard layout, for auto-selecting the emulated keyboard layout.
// Uses the Keyboard Map API (Chromium only; Safari/Firefox lack it, returning ""). getLayoutMap()
// is async, so this is an async function -> a Task<string> on the .NET side.
export async function getKeyboardLayoutId() {
    try {
        if (navigator.keyboard?.getLayoutMap) {
            const map = await navigator.keyboard.getLayoutMap();
            // Fingerprint keys whose character differs between supported layouts.
            const semicolon = map.get("Semicolon");
            const bracketLeft = map.get("BracketLeft");
            if (semicolon === "ö" || bracketLeft === "å")
                return "Swedish";
            if (semicolon === ";")
                return "US";
        }
    } catch {
        // Keyboard Map API unavailable or denied — fall through to "" (caller treats as auto-detect).
    }
    return "";
}

// Returns the OS platform string, used to detect macOS (for the ISO-keyboard key fix).
export function getNavigatorPlatform() {
    return navigator.userAgentData?.platform ?? navigator.platform ?? "";
}

// Unlocks audio autoplay. Browsers keep an AudioContext suspended until a real user gesture, so a
// program that plays audio on autostart stays silent until the user interacts. Called synchronously
// from the startup acknowledgement dialog's confirm click (a trusted gesture): creating and
// resuming an AudioContext here marks the page as activated, so the emulator's own (later) audio
// context is allowed to play. The scratch context is short-lived and then closed.
export function unlockAudio() {
    try {
        const Ctx = globalThis.AudioContext || globalThis.webkitAudioContext;
        if (!Ctx)
            return;
        const ctx = new Ctx();
        if (ctx.state === "suspended")
            ctx.resume()?.catch(() => { /* best-effort: rejected resume just leaves audio locked */ });
        setTimeout(() => { ctx.close()?.catch(() => { /* best-effort */ }); }, 1000);
    } catch {
        // Best-effort: if the Web Audio API is unavailable the emulator simply stays silent until
        // the user interacts, which is the pre-existing behaviour.
    }
}
