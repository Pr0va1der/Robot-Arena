mergeInto(LibraryManager.library, {
    RobotArenaInstallBrowserGuards: function () {
        if (typeof document === 'undefined') {
            return;
        }

        var canvas = document.getElementById('unity-canvas') || document.querySelector('canvas');
        if (!canvas || canvas.__robotArenaBrowserGuardsInstalled) {
            return;
        }

        var preventDefault = function (event) {
            event.preventDefault();
        };
        var preventCanvasScroll = function (event) {
            if (event.target === canvas || canvas.contains(event.target)) {
                event.preventDefault();
            }
        };
        var preventGameKeyScroll = function (event) {
            var scrollKeys = [' ', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'PageUp', 'PageDown', 'Home', 'End'];
            var activeElementIsCanvas = document.activeElement === canvas || canvas.contains(document.activeElement);
            if (activeElementIsCanvas && scrollKeys.indexOf(event.key) !== -1) {
                event.preventDefault();
            }
        };

        canvas.addEventListener('contextmenu', preventDefault, false);
        canvas.addEventListener('selectstart', preventDefault, false);
        canvas.addEventListener('wheel', preventCanvasScroll, { passive: false });
        canvas.addEventListener('mousewheel', preventCanvasScroll, { passive: false });
        document.addEventListener('keydown', preventGameKeyScroll, false);

        document.documentElement.style.overflow = 'hidden';
        document.body.style.overflow = 'hidden';
        canvas.style.userSelect = 'none';
        canvas.style.webkitUserSelect = 'none';
        canvas.__robotArenaBrowserGuardsInstalled = true;
    }
});
