const STORAGE_KEY = 'kelli.photo.slideshowSettings';

export function getSettings() {
    try {
        const item = localStorage.getItem(STORAGE_KEY);
        return item ? JSON.parse(item) : null;
    } catch (e) {
        console.error('Failed to load slideshow settings from localStorage:', e);
        return null;
    }
}

export function saveSettings(settings) {
    try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(settings));
    } catch (e) {
        console.error('Failed to save slideshow settings to localStorage:', e);
    }
}

function isApiFullscreen() {
    return !!(document.fullscreenElement || document.webkitFullscreenElement);
}

/** Approximate F11 / chrome-hidden windowed fullscreen (Fullscreen API may be unset). */
function isBrowserChromeFullscreen() {
    const heightMatch = Math.abs(window.innerHeight - screen.height) <= 2
        || Math.abs(window.outerHeight - screen.height) <= 8;
    const widthMatch = Math.abs(window.innerWidth - screen.width) <= 2
        || Math.abs(window.outerWidth - screen.width) <= 8;
    return heightMatch && widthMatch;
}

export function isImmersiveDisplay() {
    return isApiFullscreen() || isBrowserChromeFullscreen();
}

export async function requestFullscreen(element) {
    if (!element) return false;
    const req = element.requestFullscreen || element.webkitRequestFullscreen;
    if (!req) return false;
    try {
        await req.call(element);
        return true;
    } catch (e) {
        console.warn('Fullscreen request failed:', e);
        return false;
    }
}

export async function exitFullscreen() {
    if (!isApiFullscreen()) return;
    const exit = document.exitFullscreen || document.webkitExitFullscreen;
    if (!exit) return;
    try {
        await exit.call(document);
    } catch (e) {
        console.warn('Exit fullscreen failed:', e);
    }
}

export async function toggleFullscreen(element) {
    if (isApiFullscreen()) {
        await exitFullscreen();
        return false;
    }
    return await requestFullscreen(element);
}

/**
 * Subscribe to fullscreen / resize and notify Blazor when immersive mode changes.
 * Returns a disposable handle.
 */
export function subscribeDisplayMode(dotNetHelper) {
    let last = null;

    const notify = () => {
        const immersive = isImmersiveDisplay();
        if (immersive === last) return;
        last = immersive;
        dotNetHelper.invokeMethodAsync('OnDisplayModeChanged', immersive);
    };

    document.addEventListener('fullscreenchange', notify);
    document.addEventListener('webkitfullscreenchange', notify);
    window.addEventListener('resize', notify);

    // Initial sync after Blazor attaches
    notify();

    return {
        dispose: () => {
            document.removeEventListener('fullscreenchange', notify);
            document.removeEventListener('webkitfullscreenchange', notify);
            window.removeEventListener('resize', notify);
        }
    };
}

/** Best-effort browser cache warm for the next/prev web image. */
export function prefetchImage(url) {
    if (!url) return;
    const img = new Image();
    img.decoding = 'async';
    img.src = url;
}
