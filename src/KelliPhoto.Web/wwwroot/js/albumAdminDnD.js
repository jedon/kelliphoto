/**
 * HTML5 drag-and-drop helper for album admin grid.
 * Reports the new order of folder IDs to Blazor via DotNetObjectReference.
 */

let active = null;

/**
 * @param {HTMLElement} container - Element whose direct children have data-album-id
 * @param {any} dotNetHelper - DotNetObjectReference with OnDragReorderAsync(int[])
 */
export function initialize(container, dotNetHelper) {
    dispose();

    if (!container || !dotNetHelper) {
        console.warn('albumAdminDnD: missing container or DotNet helper');
        return;
    }

    const onDragStart = (e) => {
        const handle = e.target.closest('[data-album-dnd-handle]');
        const item = e.target.closest('[data-album-id]');
        if (!handle || !item || !container.contains(item)) {
            e.preventDefault();
            return;
        }

        const id = item.getAttribute('data-album-id');
        e.dataTransfer.effectAllowed = 'move';
        try {
            e.dataTransfer.setData('text/plain', id ?? '');
        } catch {
            // some browsers are picky during dragstart
        }

        item.classList.add('album-dnd-dragging');
        active.dragEl = item;
        active.dragId = id;
        active.orderBefore = readOrder(container);
    };

    const onDragOver = (e) => {
        e.preventDefault();
        e.dataTransfer.dropEffect = 'move';
        if (!active?.dragEl) return;

        const over = e.target.closest('[data-album-id]');
        if (!over || over === active.dragEl || !container.contains(over)) return;

        const rect = over.getBoundingClientRect();
        const midX = rect.left + rect.width / 2;
        const midY = rect.top + rect.height / 2;
        // Prefer horizontal for grid layouts; fall back to vertical for narrow cards
        const insertBefore = Math.abs(e.clientX - midX) >= Math.abs(e.clientY - midY)
            ? e.clientX < midX
            : e.clientY < midY;

        if (insertBefore) {
            if (over.previousElementSibling !== active.dragEl) {
                container.insertBefore(active.dragEl, over);
            }
        } else if (over.nextElementSibling !== active.dragEl) {
            container.insertBefore(active.dragEl, over.nextElementSibling);
        }
    };

    const onDrop = (e) => {
        e.preventDefault();
    };

    const onDragEnd = async () => {
        if (active?.dragEl) {
            active.dragEl.classList.remove('album-dnd-dragging');
        }

        try {
            await reportOrderIfChanged();
        } finally {
            if (active) {
                active.dragEl = null;
                active.dragId = null;
                active.orderBefore = null;
            }
        }
    };

    async function reportOrderIfChanged() {
        if (!active?.dotNetHelper) return;

        const ids = readOrder(container);
        const key = ids.join(',');
        const beforeKey = (active.orderBefore ?? []).join(',');
        if (key === beforeKey || ids.length === 0) return;

        try {
            await active.dotNetHelper.invokeMethodAsync('OnDragReorderAsync', ids);
        } catch (err) {
            console.error('albumAdminDnD: failed to report order', err);
        }
    }

    container.addEventListener('dragstart', onDragStart);
    container.addEventListener('dragover', onDragOver);
    container.addEventListener('drop', onDrop);
    container.addEventListener('dragend', onDragEnd);

    active = {
        container,
        dotNetHelper,
        dragEl: null,
        dragId: null,
        orderBefore: null,
        onDragStart,
        onDragOver,
        onDrop,
        onDragEnd
    };
}

export function dispose() {
    if (!active) return;

    const { container, onDragStart, onDragOver, onDrop, onDragEnd } = active;
    try {
        container.removeEventListener('dragstart', onDragStart);
        container.removeEventListener('dragover', onDragOver);
        container.removeEventListener('drop', onDrop);
        container.removeEventListener('dragend', onDragEnd);
    } catch {
        // container may already be detached
    }

    active = null;
}

function readOrder(container) {
    return Array.from(container.querySelectorAll(':scope > [data-album-id]'))
        .map((el) => parseInt(el.getAttribute('data-album-id') ?? '', 10))
        .filter((n) => !Number.isNaN(n));
}
