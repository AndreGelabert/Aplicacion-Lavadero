(() => {
    'use strict';
    const fragment = location.hash.slice(1);
    history.replaceState(null, '', location.pathname);
    const params = new URLSearchParams(fragment);
    const keys = ['CascadeId', 'OfferId', 'ClientId', 'Token'];
    const values = {};
    const valid = keys.every(key => params.getAll(key).length === 1 && params.get(key).trim().length > 0);
    if (valid) keys.forEach(key => { values[key] = params.get(key); });
    const accept = document.getElementById('aceptar');
    const reject = document.getElementById('rechazar');
    const status = document.getElementById('estado');
    const error = 'No pudimos procesar su respuesta. Consulte con el lavadero.';
    if (!valid) { status.textContent = error; return; }
    accept.disabled = false;
    reject.disabled = false;
    let pending = false;
    let finished = false;
    let selected = null;
    let uncertain = false;
    const uncertainty = 'No sabemos si su respuesta fue registrada. Consulte con el lavadero.';
    async function respond(choice) {
        if (pending || finished || (selected !== null && selected !== choice)) return;
        selected = choice;
        pending = true;
        accept.disabled = true;
        reject.disabled = true;
        let retry = false;
        try {
            const response = await fetch('/api/turno-oferta-email/responder', {
                method: 'POST', credentials: 'omit', redirect: 'error', referrerPolicy: 'no-referrer',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ ...values, Accept: choice })
            });
            if (response.ok) {
                status.textContent = 'Su respuesta fue registrada.';
            } else if (response.status === 400) {
                status.textContent = uncertain ? uncertainty : error;
            } else {
                retry = true;
            }
        } catch {
            retry = true;
        }
        pending = false;
        if (retry) {
            uncertain = true;
            status.textContent = 'No sabemos si su respuesta fue registrada. Puede reintentar la misma respuesta o consultar con el lavadero.';
            const button = choice ? accept : reject;
            button.textContent = choice ? 'Reintentar aceptar oferta' : 'Reintentar rechazar oferta';
            button.disabled = false;
        } else {
            finished = true;
            keys.forEach(key => { delete values[key]; });
        }
    }
    accept.addEventListener('click', () => respond(true));
    reject.addEventListener('click', () => respond(false));
})();
