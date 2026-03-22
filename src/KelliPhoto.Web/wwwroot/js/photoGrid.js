let observer = null;

export function initializeScrollObserver(element, dotNetHelper) {
    if (observer) {
        observer.disconnect();
    }

    // Check if element is valid before observing
    if (!element || typeof element !== 'object') {
        console.warn('Scroll observer: Invalid element reference');
        return;
    }

    const options = {
        root: null,
        rootMargin: '200px', // Trigger 200px before reaching the element
        threshold: 0.1
    };

    observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                dotNetHelper.invokeMethodAsync('LoadMoreOnScroll');
            }
        });
    }, options);

    try {
        observer.observe(element);
    } catch (error) {
        console.error('Scroll observer: Failed to observe element', error);
    }
}

export function disposeScrollObserver() {
    if (observer) {
        observer.disconnect();
        observer = null;
    }
}
