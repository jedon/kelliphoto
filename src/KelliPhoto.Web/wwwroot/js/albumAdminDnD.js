/**
 * HTML5 drag-and-drop helper for album admin grid.
 * Reports the new order of folder IDs to Blazor via DotNetObjectReference.
 * Each initialize() call returns an independent controller; multiple grids can coexist.
 */

/**
 * @param {HTMLElement} container - Element whose direct children have data-album-id
 * @param {any} dotNetHelper - DotNetObjectReference with OnDragReorderAsync(int[])
 * @returns {{ dispose: () => void }}
 */
export function initialize(container, dotNetHelper) {
    if (!container || !dotNetHelper) {
        console.warn('albumAdminDnD: missing container or DotNet helper');
        return { dispose() {} };
    }

    let dragEl = null;
    let orderBefore = null;
    let disposed = false;

    const onDragStart = (e) => {
        if (container.getAttribute('data-busy') === 'true') {
            e.preventDefault();
            return;
        }

        const handle = e.target.closest('[data-album-dnd-handle]');
        const item = e.target.closest('[data-album-id]');
        if (!handle || !item || !container.contains(item)) {
            e.preventDefault();
            return;
        }

        if (handle.disabled || handle.getAttribute('aria-disabled') === 'true') {
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
        dragEl = item;
        orderBefore = readOrder(container);
    };

    const onDragOver = (e) => {
        e.preventDefault();
        e.dataTransfer.dropEffect = 'move';
        if (!dragEl) return;

        const over = e.target.closest('[data-album-id]');
        if (!over || over === dragEl || !container.contains(over)) return;

        const rect = over.getBoundingClientRect();
        const midX = rect.left + rect.width / 2;
        const midY = rect.top + rect.height / 2;
        // Prefer horizontal for grid layouts; fall back to vertical for narrow cards
        const insertBefore = Math.abs(e.clientX - midX) >= Math.abs(e.clientY - midY)
            ? e.clientX < midX
            : e.clientY < midY;

        if (insertBefore) {
            if (over.previousElementSibling !== dragEl) {
                container.insertBefore(dragEl, over);
            }
        } else if (over.nextElementSibling !== dragEl) {
            container.insertBefore(dragEl, over.nextElementSibling);
        }
    };

    const onDrop = (e) => {
        e.preventDefault();
    };

    const onDragEnd = async () => {
        if (dragEl) {
            dragEl.classList.remove('album-dnd-dragging');
        }

        try {
            await reportOrderIfChanged();
        } finally {
            dragEl = null;
            orderBefore = null;
        }
    };

    async function reportOrderIfChanged() {
        const ids = readOrder(container);
        const key = ids.join(',');
        const beforeKey = (orderBefore ?? []).join(',');
        if (key === beforeKey || ids.length === 0) return;

        try {
            await dotNetHelper.invokeMethodAsync('OnDragReorderAsync', ids);
        } catch (err) {
            console.error('albumAdminDnD: failed to report order', err);
        }
    }

    container.addEventListener('dragstart', onDragStart);
    container.addEventListener('dragover', onDragOver);
    container.addEventListener('drop', onDrop);
    container.addEventListener('dragend', onDragEnd);

    return {
        dispose() {
            if (disposed) return;
            disposed = true;

            try {
                container.removeEventListener('dragstart', onDragStart);
                container.removeEventListener('dragover', onDragOver);
                container.removeEventListener('drop', onDrop);
                container.removeEventListener('dragend', onDragEnd);
            } catch {
                // container may already be detached
            }

            if (dragEl) {
                dragEl.classList.remove('album-dnd-dragging');
            }
            dragEl = null;
            orderBefore = null;
        }
    };
}

function readOrder(container) {
    return Array.from(container.querySelectorAll(':scope > [data-album-id]'))
        .map((el) => parseInt(el.getAttribute('data-album-id') ?? '', 10))
        .filter((n) => !Number.isNaN(n));
}
