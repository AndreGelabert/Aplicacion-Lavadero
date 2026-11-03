/** Shared presentation for operation results. Business logic stays in page modules. */
(function () {
    'use strict';

    const variants = {
        success: { title: 'Éxito', icon: '✓', duration: 5000 },
        error: { title: 'Error', icon: '×', duration: 0 },
        warning: { title: 'Aviso', icon: '!', duration: 0 },
        info: { title: 'Información', icon: 'i', duration: 5000 },
        loading: { title: 'En curso', icon: '…', duration: 0 }
    };

    function show(message, type = 'info') {
        if (typeof message !== 'string' || !message.trim()) return;
        if (!Object.hasOwn(variants, type)) type = 'info';
        const variant = variants[type];

        let container = document.getElementById('app-notifications');
        if (!container) {
            container = document.createElement('section');
            container.id = 'app-notifications';
            container.setAttribute('aria-label', 'Notificaciones');
            document.body.appendChild(container);
        }

        const card = document.createElement('div');
        card.className = 'app-notification';
        card.dataset.notificationType = type;
        card.setAttribute('role', type === 'error' ? 'alert' : 'status');
        card.setAttribute('aria-atomic', 'true');

        const icon = document.createElement('span');
        icon.className = 'app-notification-icon';
        icon.setAttribute('aria-hidden', 'true');
        icon.textContent = variant.icon;

        const content = document.createElement('div');
        content.className = 'app-notification-content';
        const title = document.createElement('strong');
        title.textContent = variant.title;
        const text = document.createElement('p');
        text.textContent = message;
        content.append(title, text);

        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'app-notification-close';
        close.setAttribute('aria-label', 'Cerrar notificación');
        close.textContent = '×';

        let timer;
        let remaining = variant.duration;
        let started;
        function dismiss() {
            clearTimeout(timer);
            card.remove();
        }
        function pause() {
            clearTimeout(timer);
            if (started !== undefined) remaining = Math.max(0, remaining - (Date.now() - started));
            started = undefined;
        }
        function resume() {
            if (!variant.duration || card.matches(':hover') || card.contains(document.activeElement)) return;
            started = Date.now();
            timer = setTimeout(dismiss, remaining);
        }
        close.addEventListener('click', dismiss);
        card.addEventListener('mouseenter', pause);
        card.addEventListener('mouseleave', resume);
        card.addEventListener('focusin', pause);
        card.addEventListener('focusout', () => queueMicrotask(resume));
        card.append(icon, content, close);
        container.appendChild(card);
        resume();
        return dismiss;
    }

    window.Notificaciones = Object.freeze({
        mostrar: show,
        exito: message => show(message, 'success'),
        error: message => show(message, 'error'),
        aviso: message => show(message, 'warning'),
        info: message => show(message, 'info'),
        cargando: message => show(message, 'loading')
    });
})();
