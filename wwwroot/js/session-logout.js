(function () {
    const form = document.getElementById('session-logout-form');
    if (!form) return;
    let pending = false;
    let channel;
    try {
        if (window.BroadcastChannel) channel = new window.BroadcastChannel('app-logout');
    } catch (error) {
        console.error('No se pudo abrir el canal de cierre de sesión.', error);
    }

    async function clearFirebase() {
        try {
            if (!window.firebaseSignOut) await import('./GoogleConnectionModule.js');
            await window.firebaseSignOut();
            return true;
        } catch (error) {
            console.error('No se pudo cerrar la sesión de Firebase.', error);
            return false;
        }
    }

    async function finishFirebase(firebase) {
        let timer;
        const success = await Promise.race([
            firebase,
            new Promise(resolve => { timer = setTimeout(() => resolve(false), 2000); })
        ]);
        clearTimeout(timer);
        if (!success) window.alert('No se pudo confirmar el cierre de sesión de Firebase. Compruebe su conexión e intente nuevamente.');
    }

    function notifyTabs() {
        try {
            window.localStorage.setItem('app-logout', String(Date.now()) + Math.random());
            return true;
        } catch (error) {
            try {
                if (channel) {
                    channel.postMessage('logout');
                    return true;
                }
            } catch (channelError) {
                console.error('No se pudo notificar a las pestañas.', channelError);
            }
            return false;
        }
    }

    async function receiveLogout() {
        if (pending) return;
        pending = true;
        await finishFirebase(clearFirebase());
        window.location.href = '/Login/Index';
    }

    if (channel) channel.onmessage = event => {
        if (event.data === 'logout') return receiveLogout();
    };

    async function logout(reason) {
        if (pending) return;
        pending = true;
        const firebase = clearFirebase();
        try {
            const body = new URLSearchParams();
            body.set('__RequestVerificationToken', form.querySelector('[name="__RequestVerificationToken"]').value);
            const response = await fetch(form.action, { method: 'POST', body, credentials: 'same-origin',
                redirect: 'manual', headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            const logoutCompleted = response.ok && !response.redirected && response.status === 204;
            const sessionAlreadyEnded = Boolean(reason) && response.type === 'opaqueredirect';
            if (!logoutCompleted && !sessionAlreadyEnded) throw new Error('Logout failed');
            if (!notifyTabs()) window.alert('Se cerró la sesión en esta pestaña, pero no se pudo notificar a las otras pestañas. Ciérrelas manualmente.');
            await finishFirebase(firebase);
            window.location.href = reason ? '/Login/Index?expired=' + reason : '/Login/Index';
        } catch (error) {
            console.error('No se pudo cerrar la sesión del servidor.', error);
            pending = false;
            window.alert('No se pudo cerrar la sesión. Por favor, intente de nuevo.');
        }
    }

    window.appLogout = logout;
    form.addEventListener('submit', event => { event.preventDefault(); return logout(); });
    document.querySelectorAll('[data-session-logout]').forEach(other => {
        other.addEventListener('submit', event => { event.preventDefault(); return logout(); });
    });
    window.addEventListener('storage', event => {
        if (event.key === 'app-logout' && event.newValue) {
            return receiveLogout();
        }
    });
})();
