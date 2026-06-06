const sockets = new Map();

function getState(id) {
    let state = sockets.get(id);
    if (!state) {
        state = {
            socket: null,
            queue: [],
            openPromise: null,
            closeCode: null,
            closeReason: "",
        };
        sockets.set(id, state);
    }

    return state;
}

export function open(id, url) {
    const state = getState(id);
    if (state.socket && (state.socket.readyState === WebSocket.OPEN || state.socket.readyState === WebSocket.CONNECTING)) {
        return state.openPromise ?? Promise.resolve();
    }

    state.queue = [];
    state.closeCode = null;
    state.closeReason = "";

    state.openPromise = new Promise((resolve, reject) => {
        const socket = new WebSocket(url);
        socket.binaryType = "arraybuffer";

        socket.onopen = () => resolve();
        socket.onmessage = (event) => {
            const bytes = new Uint8Array(event.data);
            for (const value of bytes) {
                state.queue.push(value);
            }
        };
        socket.onerror = () => {
            if (socket.readyState !== WebSocket.OPEN) {
                reject(new Error("WebSocket connection failed."));
            }
        };
        socket.onclose = (event) => {
            state.closeCode = event.code;
            state.closeReason = event.reason || "";
        };

        state.socket = socket;
    });

    return state.openPromise;
}

export function close(id, code, reason) {
    const state = getState(id);
    if (!state.socket) {
        return;
    }

    if (state.socket.readyState === WebSocket.OPEN || state.socket.readyState === WebSocket.CONNECTING) {
        state.socket.close(code, reason);
    }
}

export function sendByte(id, value) {
    const state = getState(id);
    if (!state.socket || state.socket.readyState !== WebSocket.OPEN) {
        throw new Error("WebSocket is not open.");
    }

    state.socket.send(Uint8Array.of(value & 0xff));
}

export function drainReceived(id) {
    const state = getState(id);
    if (state.queue.length === 0) {
        return "";
    }

    const drained = state.queue.join(",");
    state.queue = [];
    return drained;
}

export function getReadyState(id) {
    const state = getState(id);
    return state.socket ? state.socket.readyState : -1;
}

export function cleanup(id) {
    sockets.delete(id);
}
