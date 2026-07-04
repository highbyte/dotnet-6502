const databaseName = "dotnet6502-download-cache";
const databaseVersion = 1;
const storeName = "entries";

let openPromise = null;

export async function isDownloadCacheAvailable() {
    if (!globalThis.indexedDB)
        return false;

    try {
        const db = await openDatabase();
        db.close();
        openPromise = null;
        return true;
    } catch (error) {
        console.warn("IndexedDB download cache is unavailable.", error);
        openPromise = null;
        return false;
    }
}

export async function tryGetDownloadCacheEntry(url) {
    const db = await openDatabase();
    const record = await requestToPromise(
        db.transaction(storeName, "readwrite")
            .objectStore(storeName)
            .get(url));

    if (!record?.content || !record.entry)
        return "";

    record.entry.LastAccessUtc = new Date().toISOString();
    await requestToPromise(
        db.transaction(storeName, "readwrite")
            .objectStore(storeName)
            .put(record));

    return JSON.stringify({
        entry: record.entry,
        base64: bytesToBase64(new Uint8Array(record.content))
    });
}

export async function putDownloadCacheEntry(entryJson, base64) {
    const entry = JSON.parse(entryJson);
    const content = base64ToBytes(base64);
    const contentCopy = content.buffer.slice(content.byteOffset, content.byteOffset + content.byteLength);

    const record = {
        url: entry.Url,
        entry,
        content: contentCopy
    };

    const db = await openDatabase();
    await requestToPromise(
        db.transaction(storeName, "readwrite")
            .objectStore(storeName)
            .put(record));
}

export async function listDownloadCacheEntries() {
    const db = await openDatabase();
    const records = await requestToPromise(
        db.transaction(storeName, "readonly")
            .objectStore(storeName)
            .getAll());

    const entries = (records ?? [])
        .map(record => record.entry)
        .filter(entry => !!entry)
        .sort((a, b) => Date.parse(b.LastAccessUtc ?? "") - Date.parse(a.LastAccessUtc ?? ""));

    return JSON.stringify(entries);
}

export async function removeDownloadCacheEntry(url) {
    const db = await openDatabase();
    await requestToPromise(
        db.transaction(storeName, "readwrite")
            .objectStore(storeName)
            .delete(url));
}

export async function clearDownloadCache() {
    const db = await openDatabase();
    await requestToPromise(
        db.transaction(storeName, "readwrite")
            .objectStore(storeName)
            .clear());
}

function openDatabase() {
    if (openPromise)
        return openPromise;

    openPromise = new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, databaseVersion);

        request.onupgradeneeded = () => {
            const db = request.result;
            if (!db.objectStoreNames.contains(storeName))
                db.createObjectStore(storeName, { keyPath: "url" });
        };

        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error ?? new Error("Failed to open IndexedDB."));
        request.onblocked = () => reject(new Error("IndexedDB download cache upgrade was blocked."));
    });

    return openPromise;
}

function requestToPromise(request) {
    return new Promise((resolve, reject) => {
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error ?? new Error("IndexedDB request failed."));
    });
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
