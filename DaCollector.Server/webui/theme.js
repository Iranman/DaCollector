// Shared light/dark theme logic for DaCollector webui
(function () {
    const KEY = 'dc-theme';

    function apply(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem(KEY, theme);
        document.querySelectorAll('.theme-sun').forEach(el => {
            el.style.display = theme === 'dark' ? 'block' : 'none';
        });
        document.querySelectorAll('.theme-moon').forEach(el => {
            el.style.display = theme === 'light' ? 'block' : 'none';
        });
    }

    window.initTheme = function () {
        apply(localStorage.getItem(KEY) || 'dark');
    };

    window.toggleTheme = function () {
        const cur = document.documentElement.getAttribute('data-theme') || 'dark';
        apply(cur === 'dark' ? 'light' : 'dark');
    };

    // Apply immediately to avoid flash
    apply(localStorage.getItem(KEY) || 'dark');
})();
