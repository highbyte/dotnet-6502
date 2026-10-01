(() => {
    let contentWidth = 0;
    let contentHeight = 0;
    const minimumZoom = 0.1;
    const maximumZoom = 2;
    const zoomStep = 0.25;

    const app = document.getElementById('out');
    const viewport = document.getElementById('browser-app-viewport');
    const scroller = document.getElementById('browser-app-scroll');
    const controls = document.getElementById('browser-zoom-controls');
    const zoomOutButton = document.getElementById('browser-zoom-out');
    const zoomInButton = document.getElementById('browser-zoom-in');
    const fitButton = document.getElementById('browser-zoom-fit');
    const resetButton = document.getElementById('browser-zoom-reset');
    const zoomLevel = document.getElementById('browser-zoom-level');
    const options = document.getElementById('browser-zoom-options');
    const toggleButton = document.getElementById('browser-zoom-toggle');
    const orientationButton = document.getElementById('browser-orientation-toggle');
    const orientationStatus = document.getElementById('browser-orientation-status');
    const touchPointer = window.matchMedia?.('(any-pointer: coarse)');

    let collapsed = false;
    let hasContentSize = false;
    let fitToWindow = true;
    let zoom = 1;
    let controlsClearance = 0;
    let viewportChanged = null;
    let lastViewport = null;
    let viewportNotificationPending = false;
    let modalOpen = false;
    let orientationLocked = false;
    let changingOrientation = false;
    let orientationUnavailable = false;
    let orientationSupported = false;
    const manualRotationHint = 'Screen rotation is unavailable in this browser. Turn your device to portrait or landscape. If the screen stays fixed, turn off the device rotation lock.';
    let enteredFullscreenForOrientation = false;
    let orientationMessageTimer;
    let orientationMessagePinned = false;

    function oppositeOrientation() {
        const type = window.screen?.orientation?.type;
        const portrait = type ? type.startsWith('portrait')
            : window.innerHeight >= document.documentElement.clientWidth;
        return portrait ? 'landscape' : 'portrait';
    }

    function updateOrientationButton() {
        orientationSupported = touchPointer?.matches
            && typeof window.screen?.orientation?.lock === 'function'
            && typeof window.screen?.orientation?.unlock === 'function'
            && typeof document.documentElement.requestFullscreen === 'function'
            && document.fullscreenEnabled;
        orientationButton.hidden = !touchPointer?.matches;
        // ARIA-disabled keeps unavailable rotation focusable/tappable for its help.
        // Native disabled would prevent touch users from opening the explanation.
        orientationButton.disabled = changingOrientation;
        orientationButton.setAttribute('aria-disabled',
            String(changingOrientation || orientationUnavailable || !orientationSupported));
        resetButton.disabled = changingOrientation;
        const fullscreenHint = document.fullscreenElement ? '' : ' (opens fullscreen)';
        const label = orientationUnavailable || !orientationSupported ? manualRotationHint
            : `Switch to ${oppositeOrientation()} orientation${fullscreenHint}`;
        orientationButton.setAttribute('aria-label', label);
        orientationButton.setAttribute('title', label);
    }

    function showManualRotationHelp(persistent = false) {
        if (!orientationButton.hidden && (orientationUnavailable || !orientationSupported)
            && !orientationMessagePinned) {
            const emulationHint = orientationUnavailable
                ? " In desktop device emulation, use the device toolbar's rotate icon." : '';
            showOrientationMessage(manualRotationHint + emulationHint, persistent);
        }
    }

    function dismissOrientationMessage() {
        window.clearTimeout(orientationMessageTimer);
        orientationMessagePinned = false;
        orientationStatus.hidden = true;
    }

    function showOrientationMessage(message, persistent = false) {
        window.clearTimeout(orientationMessageTimer);
        orientationMessagePinned = persistent;
        orientationStatus.textContent = message;
        orientationStatus.hidden = false;
        if (!persistent) {
            orientationMessageTimer = window.setTimeout(dismissOrientationMessage, 10000);
        }
    }

    function unlockOrientation() {
        if (orientationLocked) {
            try {
                window.screen?.orientation?.unlock();
            } catch {
                orientationUnavailable = true;
                showOrientationMessage('Use the browser controls to exit fullscreen and restore automatic rotation.');
            }
        }
        orientationLocked = false;
    }

    function toggleManualRotationHelp() {
        // Hover/focus may precede the first click. Only a second explicit
        // activation dismisses pinned help; the first keeps it open to read.
        if (orientationMessagePinned) {
            dismissOrientationMessage();
        } else {
            showManualRotationHelp(true);
        }
    }

    async function rotateOrientation() {
        if (changingOrientation || orientationButton.hidden) {
            return;
        }
        if (orientationUnavailable || !orientationSupported) {
            toggleManualRotationHelp();
            return;
        }
        const target = oppositeOrientation();
        let enteredFullscreen = false;
        let requestStage = 'fullscreen';
        changingOrientation = true;
        updateOrientationButton();
        try {
            // Fullscreen must start directly from the user's button click.
            if (!document.fullscreenElement) {
                await document.documentElement.requestFullscreen();
                enteredFullscreen = true;
            }
            requestStage = 'orientation-lock';
            await window.screen.orientation.lock(target);
            if (!document.fullscreenElement) {
                window.screen.orientation.unlock();
                return;
            }
            orientationLocked = true;
            enteredFullscreenForOrientation ||= enteredFullscreen;
            showOrientationMessage(`Switched to ${target}. Reset or exit fullscreen to restore automatic rotation.`);
        } catch (error) {
            // Log only platform diagnostics, without arbitrary exception text.
            console.warn('[DotNet6502] Orientation request failed.', {
                stage: requestStage,
                error: error?.name ?? 'UnknownError',
                target,
                orientation: window.screen?.orientation?.type,
                viewportWidth: document.documentElement.clientWidth,
                viewportHeight: document.documentElement.clientHeight || window.innerHeight,
                fullscreen: Boolean(document.fullscreenElement)
            });
            let exitHint = '';
            if (enteredFullscreen && document.fullscreenElement) {
                await document.exitFullscreen().catch(() => {
                    exitHint = ' Use the browser controls to exit fullscreen.';
                });
            }
            if (requestStage === 'orientation-lock' && error?.name === 'NotSupportedError') {
                orientationUnavailable = true;
                showOrientationMessage('Screen rotation is unavailable in this browser. Rotate your device; in desktop device emulation, use the device toolbar\'s rotate icon.' + exitHint, true);
            } else if (requestStage === 'fullscreen') {
                showOrientationMessage('Fullscreen was not allowed. Try Rotate again and allow fullscreen, or rotate your device.' + exitHint);
            } else {
                showOrientationMessage('Orientation change was not allowed. Rotate your device, or try Rotate again and allow fullscreen.' + exitHint);
            }
        } finally {
            changingOrientation = false;
            updateOrientationButton();
            applyZoom(zoom, fitToWindow);
        }
    }

    function reportViewport() {
        if (!viewportChanged || !hasContentSize) {
            return;
        }
        const bounds = app.getBoundingClientRect();
        const visible = window.visualViewport;
        const left = visible?.offsetLeft ?? 0;
        const top = visible?.offsetTop ?? 0;
        const width = Math.min(visible?.width ?? scroller.clientWidth, scroller.clientWidth);
        const height = Math.max(1, (visible?.height
            ?? scroller.clientHeight) - controlsClearance);
        const x = Math.max(left, bounds.left);
        const y = Math.max(top, bounds.top);
        const area = [(x - bounds.left) / zoom, (y - bounds.top) / zoom,
            Math.max(0, Math.min(left + width, bounds.right) - x) / zoom,
            Math.max(0, Math.min(top + height, bounds.bottom) - y) / zoom];
        if (!area.every(Number.isFinite) || area[2] <= 0 || area[3] <= 0) {
            return;
        }
        if (!lastViewport || area.some((value, index) => value !== lastViewport[index])) {
            lastViewport = area;
            viewportChanged(JSON.stringify(area));
        }
    }

    function queueViewportReport() {
        if (viewportNotificationPending) {
            return;
        }
        viewportNotificationPending = true;
        window.requestAnimationFrame(() => {
            viewportNotificationPending = false;
            reportViewport();
        });
    }

    function clampZoom(value) {
        return Math.max(minimumZoom, Math.min(maximumZoom, value));
    }

    function applyZoom(value, resetPosition = false) {
        // Preserve the top-left content position, so zooming from the page origin
        // never moves the top of the app above the visible area.
        const left = scroller.scrollLeft / zoom;
        const top = scroller.scrollTop / zoom;
        const windowWidth = scroller.clientWidth;
        const windowHeight = scroller.clientHeight;
        const needsZoom = hasContentSize
            && (contentWidth > windowWidth || contentHeight > windowHeight);
        // Keep Reset/Rotate reachable if the rotated app now fits without zoom.
        const showControls = needsZoom || orientationLocked;
        controls.hidden = !showControls;

        const clearance = showControls
            ? Math.ceil(controls.getBoundingClientRect().height
                + Number.parseFloat(window.getComputedStyle(controls).bottom) + 4)
            : 0;
        controlsClearance = clearance;

        // Fit uses natural app dimensions, independently of the previous zoom.
        const availableHeight = Math.max(1, windowHeight - clearance);
        const fittedZoom = Math.min(1, windowWidth / Math.max(contentWidth, windowWidth),
            availableHeight / Math.max(contentHeight, windowHeight));
        const requestedZoom = fitToWindow ? fittedZoom : value;
        zoom = needsZoom ? clampZoom(requestedZoom) : 1;
        // Keep the rendering surface stable across zoom changes. Center the
        // scaled canvas when it fits so its visible area shares the window center.
        const width = Math.max(contentWidth, windowWidth);
        const height = Math.max(contentHeight, windowHeight);
        app.style.width = `${width}px`;
        app.style.height = `${height}px`;
        // A transform scales rendered pixels without changing ResizeObserver's
        // device-pixel content box, which Avalonia uses to size its canvas.
        app.style.transform = `scale(${zoom})`;
        // Avoid subpixel rounding creating a scrollbar when Fit fills the viewport.
        const scaledWidth = Math.ceil(width * zoom - 0.000001);
        const scaledHeight = Math.ceil(height * zoom - 0.000001);
        viewport.style.width = `${scaledWidth}px`;
        viewport.style.height = `${scaledHeight}px`;
        const verticalOffset = Math.max(0, (availableHeight - scaledHeight) / 2);
        scroller.style.paddingTop = `${verticalOffset}px`;
        scroller.style.paddingBottom = `${clearance + verticalOffset}px`;
        const canPan = needsZoom && (scaledWidth > windowWidth
            || scaledHeight + clearance > windowHeight);
        app.classList.toggle('browser-can-pan', canPan);

        zoomLevel.value = `${Math.round(zoom * 100)}%`;
        zoomLevel.textContent = zoomLevel.value;
        zoomOutButton.disabled = zoom <= minimumZoom;
        zoomInButton.disabled = zoom >= maximumZoom;

        // Update scroll immediately after the scroll area changes. Delayed frame
        // callbacks can restore stale positions after a rapid Fit/zoom sequence.
        scroller.scrollTo(resetPosition || !needsZoom ? 0 : left * zoom,
            resetPosition || !needsZoom ? 0 : top * zoom);
        reportViewport();
    }

    // Avalonia reports the natural content size, including larger Scale settings.
    globalThis.dotnet6502BrowserZoom = {
        setModalOpen(value) {
            modalOpen = value;
            app.classList.toggle('browser-modal-open', modalOpen);
            document.body.classList.toggle('browser-modal-open', modalOpen);
        },
        setViewportChangedCallback(callback) {
            viewportChanged = callback;
            lastViewport = null;
            reportViewport();
        },
        clearViewportChangedCallback() {
            viewportChanged = null;
            lastViewport = null;
        },
        setContentSize(width, height) {
            if (!Number.isFinite(width) || !Number.isFinite(height) || width <= 0 || height <= 0) {
                return;
            }
            hasContentSize = true;
            contentWidth = Math.ceil(width);
            contentHeight = Math.ceil(height);
            applyZoom(zoom, fitToWindow);
        }
    };

    // A modal receives wheel input through Avalonia. Prevent native page scrolling
    // even over the backdrop or when its ScrollViewer reaches the end.
    window.addEventListener('wheel', event => {
        if (modalOpen && !event.ctrlKey) {
            event.preventDefault();
        }
    }, { capture: true, passive: false });

    // Outside a modal, let the browser pan the overflowing page. Ctrl+wheel is
    // browser zoom (including trackpad pinch) and stays available in both modes.
    viewport.addEventListener('wheel', event => {
        if (event.ctrlKey || (!modalOpen && app.classList.contains('browser-can-pan'))) {
            event.stopPropagation();
        }
    }, { capture: true, passive: true });

    toggleButton.addEventListener('click', () => {
        collapsed = !collapsed;
        controls.classList.toggle('is-collapsed', collapsed);
        options.hidden = collapsed;
        toggleButton.setAttribute('aria-expanded', String(!collapsed));
        toggleButton.setAttribute('aria-label', collapsed
            ? 'Show zoom controls' : 'Hide zoom controls');
        toggleButton.setAttribute('title', collapsed
            ? 'Show zoom controls' : 'Hide zoom controls');
        // Both states have the same height, keeping Fit and scroll position stable.
    });

    zoomOutButton.addEventListener('click', () => {
        fitToWindow = false;
        applyZoom(Math.floor((zoom - 0.001) / zoomStep) * zoomStep);
    });

    zoomInButton.addEventListener('click', () => {
        fitToWindow = false;
        applyZoom(Math.ceil((zoom + 0.001) / zoomStep) * zoomStep);
    });

    fitButton.addEventListener('click', () => {
        fitToWindow = true;
        applyZoom(zoom, true);
    });

    resetButton.addEventListener('click', () => {
        dismissOrientationMessage();
        if (orientationLocked) {
            showOrientationMessage('Automatic rotation restored.');
        }
        unlockOrientation();
        if (enteredFullscreenForOrientation && document.fullscreenElement) {
            document.exitFullscreen().catch(() => {
                showOrientationMessage('Automatic rotation restored. Use the browser controls to exit fullscreen.');
            });
        }
        enteredFullscreenForOrientation = false;
        updateOrientationButton();
        fitToWindow = false;
        applyZoom(1, true);
    });

    orientationButton.setAttribute('aria-describedby', 'browser-orientation-status');
    orientationButton.addEventListener('pointerenter', event => {
        // Touch emits pointerenter before click, but isn't a hover interaction.
        if (event.pointerType === 'mouse') {
            showManualRotationHelp();
        }
    });
    orientationButton.addEventListener('focus', () => showManualRotationHelp());
    orientationButton.addEventListener('click', rotateOrientation);
    document.addEventListener('pointerdown', event => {
        if (!orientationStatus.hidden && !orientationButton.contains(event.target)
            && !orientationStatus.contains(event.target)) {
            dismissOrientationMessage();
        }
    }, { capture: true, passive: true });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && !orientationStatus.hidden) {
            dismissOrientationMessage();
            // Dismiss this foreground message before Escape reaches an emulator dialog.
            event.preventDefault();
            event.stopPropagation();
        }
    }, { capture: true });
    touchPointer?.addEventListener('change', updateOrientationButton);
    window.screen?.orientation?.addEventListener('change', updateOrientationButton);
    document.addEventListener('fullscreenchange', () => {
        if (!document.fullscreenElement) {
            if (orientationLocked) {
                showOrientationMessage('Automatic rotation restored.');
            }
            unlockOrientation();
            enteredFullscreenForOrientation = false;
        }
        updateOrientationButton();
        applyZoom(zoom, fitToWindow);
    });

    window.addEventListener('resize', () => {
        applyZoom(zoom, fitToWindow);
    });

    window.addEventListener('scroll', queueViewportReport, { passive: true });
    scroller.addEventListener('scroll', queueViewportReport, { passive: true });
    window.visualViewport?.addEventListener('resize', queueViewportReport);
    window.visualViewport?.addEventListener('scroll', queueViewportReport);

    updateOrientationButton();
    applyZoom(1, true);
})();
