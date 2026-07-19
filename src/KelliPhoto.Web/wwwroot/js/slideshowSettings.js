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
