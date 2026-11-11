(function () {
    var key = 'theme';
    // Only 'light' and 'dark' are valid explicit preferences.
    // 'system' (legacy) and any other value are ignored → follow OS.
    var preference = null; // null = follow OS
    try {
        var saved = window.localStorage.getItem(key);
        if (saved === 'light' || saved === 'dark') preference = saved;
    } catch (_) { /* Storage unavailable — keep null (follow OS). */ }

    var media = null;
    try { media = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null; } catch (_) { /* ignore */ }

    function isDark() {
        if (preference === 'dark') return true;
        if (preference === 'light') return false;
        // No explicit preference: follow OS, fallback light.
        return !!(media && media.matches);
    }

    function apply() {
        document.documentElement.classList.toggle('dark', isDark());
        document.querySelectorAll('[data-theme-toggle]').forEach(function (btn) {
            var checked = isDark().toString();
            btn.setAttribute('aria-checked', checked);
        });
    }

    apply(); // Run synchronously before paint.

    // React to OS changes only when there is no explicit saved preference.
    if (media) {
        var onMedia = function () {
            if (preference === null) apply();
        };
        if (media.addEventListener) media.addEventListener('change', onMedia);
        else if (media.addListener) media.addListener(onMedia);
    }

    function bind() {
        document.querySelectorAll('[data-theme-toggle]').forEach(function (btn) {
            btn.setAttribute('aria-checked', isDark().toString());
            btn.addEventListener('click', function () {
                // Toggle: flip current resolved state.
                preference = isDark() ? 'light' : 'dark';
                try { window.localStorage.setItem(key, preference); } catch (_) { /* Keep in-memory choice. */ }
                apply();
                // Sync all other toggles on the page.
                document.querySelectorAll('[data-theme-toggle]').forEach(function (item) {
                    item.setAttribute('aria-checked', isDark().toString());
                });
            });
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', bind);
    else bind();
}());
