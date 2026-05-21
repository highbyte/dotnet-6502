export function getLocalStorageKeys(prefix) {
    const keys = [];
    for (let i = 0; i < localStorage.length; i++) {
        const key = localStorage.key(i);
        if (key && key.startsWith(prefix))
            keys.push(key.substring(prefix.length));
    }
    return JSON.stringify(keys);
}

export function getScriptsFromLocalStorage(prefix) {
    const results = [];
    for (let i = 0; i < localStorage.length; i++) {
        const key = localStorage.key(i);
        if (key && key.startsWith(prefix)) {
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
