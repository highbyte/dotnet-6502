// Gamepad interop module for .NET WASM
// Uses the browser Gamepad API: https://developer.mozilla.org/en-US/docs/Web/API/Gamepad_API

let logCallback = null;

function log(level, message) {
    if (logCallback) {
        logCallback(level, message);
    }
}

// Log levels: 0=Debug, 1=Info, 2=Warning, 3=Error

export function registerLogCallback(callback) {
    logCallback = callback;
}

/**
 * Gets the number of connected gamepads.
 * @returns {number} The number of connected gamepads.
 */
export function getGamepadCount() {
    const gamepads = navigator.getGamepads();
    let count = 0;
    for (let i = 0; i < gamepads.length; i++) {
        if (gamepads[i] !== null && gamepads[i].connected) {
            count++;
        }
    }
    return count;
}

/**
 * Gets the index of the first connected gamepad.
 * @returns {number} The index of the first connected gamepad, or -1 if none found.
 */
export function getFirstGamepadIndex() {
    const gamepads = navigator.getGamepads();
    for (let i = 0; i < gamepads.length; i++) {
        if (gamepads[i] !== null && gamepads[i].connected) {
            return i;
        }
    }
    return -1;
}

/**
 * Gets whether a gamepad at the specified index is connected.
 * @param {number} index - The gamepad index.
 * @returns {boolean} True if connected, false otherwise.
 */
export function isGamepadConnected(index) {
    const gamepads = navigator.getGamepads();
    if (index < 0 || index >= gamepads.length) {
        return false;
    }
    const gamepad = gamepads[index];
    return gamepad !== null && gamepad.connected;
}

/**
 * Gets the name/id of the gamepad at the specified index.
 * @param {number} index - The gamepad index.
 * @returns {string} The gamepad name, or empty string if not found.
 */
export function getGamepadName(index) {
    const gamepads = navigator.getGamepads();
    if (index < 0 || index >= gamepads.length) {
        return "";
    }
    const gamepad = gamepads[index];
    if (gamepad === null || !gamepad.connected) {
        return "";
    }
    return gamepad.id || "";
}

/**
 * Gets the pressed buttons as a comma-separated string of button indices.
 * @param {number} index - The gamepad index.
 * @returns {string} Comma-separated button indices that are pressed, or empty string.
 */
export function getPressedButtons(index) {
    const gamepads = navigator.getGamepads();
    if (index < 0 || index >= gamepads.length) {
        return "";
    }
    const gamepad = gamepads[index];
    if (gamepad === null || !gamepad.connected) {
        return "";
    }

    const pressedButtons = [];
    for (let i = 0; i < gamepad.buttons.length; i++) {
        if (gamepad.buttons[i].pressed) {
            pressedButtons.push(i);
        }
    }
    return pressedButtons.join(",");
}

/**
 * Gets the axis values as a comma-separated string.
 * @param {number} index - The gamepad index.
 * @returns {string} Comma-separated axis values, or empty string.
 */
export function getAxes(index) {
    const gamepads = navigator.getGamepads();
    if (index < 0 || index >= gamepads.length) {
        return "";
    }
    const gamepad = gamepads[index];
    if (gamepad === null || !gamepad.connected) {
        return "";
    }

    return gamepad.axes.map(a => a.toFixed(4)).join(",");
}

/**
 * Gets a specific axis value.
 * @param {number} gamepadIndex - The gamepad index.
 * @param {number} axisIndex - The axis index.
 * @returns {number} The axis value (-1 to 1), or 0 if not found.
 */
export function getAxisValue(gamepadIndex, axisIndex) {
    const gamepads = navigator.getGamepads();
    if (gamepadIndex < 0 || gamepadIndex >= gamepads.length) {
        return 0;
    }
    const gamepad = gamepads[gamepadIndex];
    if (gamepad === null || !gamepad.connected) {
        return 0;
    }
    if (axisIndex < 0 || axisIndex >= gamepad.axes.length) {
        return 0;
    }
    return gamepad.axes[axisIndex];
}

/**
 * Checks if a specific button is pressed.
 * @param {number} gamepadIndex - The gamepad index.
 * @param {number} buttonIndex - The button index.
 * @returns {boolean} True if pressed, false otherwise.
 */
export function isButtonPressed(gamepadIndex, buttonIndex) {
    const gamepads = navigator.getGamepads();
    if (gamepadIndex < 0 || gamepadIndex >= gamepads.length) {
        return false;
    }
    const gamepad = gamepads[gamepadIndex];
    if (gamepad === null || !gamepad.connected) {
        return false;
    }
    if (buttonIndex < 0 || buttonIndex >= gamepad.buttons.length) {
        return false;
    }
    return gamepad.buttons[buttonIndex].pressed;
}

/**
 * Gets the button count for a gamepad.
 * @param {number} index - The gamepad index.
 * @returns {number} The number of buttons, or 0 if not found.
 */
export function getButtonCount(index) {
    const gamepads = navigator.getGamepads();
    if (index < 0 || index >= gamepads.length) {
        return 0;
    }
    const gamepad = gamepads[index];
    if (gamepad === null || !gamepad.connected) {
        return 0;
    }
    return gamepad.buttons.length;
}

/**
 * Gets the axes count for a gamepad.
 * @param {number} index - The gamepad index.
 * @returns {number} The number of axes, or 0 if not found.
 */
export function getAxesCount(index) {
    const gamepads = navigator.getGamepads();
    if (index < 0 || index >= gamepads.length) {
        return 0;
    }
    const gamepad = gamepads[index];
    if (gamepad === null || !gamepad.connected) {
        return 0;
    }
    return gamepad.axes.length;
}

/**
 * Gets whether a gamepad uses the standard mapping.
 * Standard mapping means the browser has recognized it as a gamepad and mapped it
 * to the W3C standard layout. Non-standard mapping often indicates non-gamepad devices.
 * @param {number} index - The gamepad index.
 * @returns {boolean} True if using standard mapping, false otherwise.
 */
export function hasStandardMapping(index) {
    const gamepads = navigator.getGamepads();
    if (index < 0 || index >= gamepads.length) {
        return false;
    }
    const gamepad = gamepads[index];
    if (gamepad === null || !gamepad.connected) {
        return false;
    }
    // The mapping property is "standard" for recognized gamepads
    // Empty string or other values indicate non-standard/unknown devices
    return gamepad.mapping === "standard";
}
