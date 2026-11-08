(function () {
    const key = 'theme';
    let preference = 'system';
    try {
        const saved = window.localStorage.getItem(key);
        if (['light', 'dark', 'system'].includes(saved)) preference = saved;
    } catch (_) { /* Storage can be unavailable. */ }
    const media = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;
    function apply() {
        document.documentElement.classList.toggle('dark', preference === 'dark' || (preference === 'system' && !!media?.matches));
    }
    apply();
    if (media?.addEventListener) media.addEventListener('change', apply);
    else if (media?.addListener) media.addListener(apply);
    function bind() {
        document.querySelectorAll('[data-theme-selector]').forEach(function (select) {
            select.value = preference;
            select.addEventListener('change', function () {
                if (!['light', 'dark', 'system'].includes(select.value)) return;
                preference = select.value;
                try { window.localStorage.setItem(key, preference); } catch (_) { /* Keep the in-memory choice. */ }
                apply();
                document.querySelectorAll('[data-theme-selector]').forEach(function (item) { item.value = preference; });
            });
        });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', bind);
    else bind();
}());
