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
