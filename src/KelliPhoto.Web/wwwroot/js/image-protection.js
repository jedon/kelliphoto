// Image protection script to prevent right-click and save
(function() {
    'use strict';

    // Prevent right-click context menu on images
    document.addEventListener('contextmenu', function(e) {
        if (e.target.tagName === 'IMG' || e.target.closest('.photo-item') || e.target.closest('.folder-thumbnail')) {
            e.preventDefault();
            return false;
        }
    });

    // Prevent drag and drop of images
    document.addEventListener('dragstart', function(e) {
        if (e.target.tagName === 'IMG' || e.target.closest('.photo-item') || e.target.closest('.folder-thumbnail')) {
            e.preventDefault();
            return false;
        }
    });

    // Prevent image selection
    document.addEventListener('selectstart', function(e) {
        if (e.target.tagName === 'IMG' || e.target.closest('.photo-item') || e.target.closest('.folder-thumbnail')) {
            e.preventDefault();
            return false;
        }
    });

    // Disable common keyboard shortcuts for saving images
    document.addEventListener('keydown', function(e) {
        // Disable Ctrl+S, Ctrl+Shift+S, Ctrl+U (view source)
        if ((e.ctrlKey || e.metaKey) && (e.key === 's' || e.key === 'S' || e.key === 'u' || e.key === 'U')) {
            if (e.target.tagName === 'IMG' || document.activeElement?.closest('.photo-item') || document.activeElement?.closest('.folder-thumbnail')) {
                e.preventDefault();
                return false;
            }
        }
        // Disable F12 (developer tools) - optional, can be commented out if too restrictive
        // if (e.key === 'F12') {
        //     e.preventDefault();
        //     return false;
        // }
    });

    // Add draggable="false" to all images
    function disableImageDrag() {
        const images = document.querySelectorAll('img');
        images.forEach(function(img) {
            img.setAttribute('draggable', 'false');
            img.style.userSelect = 'none';
            img.style.webkitUserSelect = 'none';
            img.style.mozUserSelect = 'none';
            img.style.msUserSelect = 'none';
        });
    }

    // Run on page load
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', disableImageDrag);
    } else {
        disableImageDrag();
    }

    // Re-run when new images are added (for Blazor dynamic content)
    const observer = new MutationObserver(function(mutations) {
        mutations.forEach(function(mutation) {
            if (mutation.addedNodes.length) {
                disableImageDrag();
            }
        });
    });

    observer.observe(document.body, {
        childList: true,
        subtree: true
    });
})();
