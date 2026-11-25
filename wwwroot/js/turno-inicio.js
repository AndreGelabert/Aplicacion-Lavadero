(() => {
    const dialog = document.getElementById('turno-inicio-dialog');
    const form = document.getElementById('turno-inicio-form');
    if (!dialog || !form) return;
    const items = document.getElementById('turno-inicio-items');
    const error = document.getElementById('turno-inicio-error');
    const submit = document.getElementById('turno-inicio-submit');
    const cancel = document.getElementById('turno-inicio-cancel');
    let ready = false, sending = false, choices = [], generation = 0, uncertainBody = null;
    const uncertainMessage = 'No se pudo confirmar el resultado. Verifique el estado del turno antes de continuar; puede reintentar la misma solicitud.';
    async function responseJson(response) {
        let data;
        try { data = await response.json(); } catch { throw new Error('No se pudo completar la solicitud.'); }
        if (!response.ok || !data.success) throw new Error(data.message || 'No se pudo completar la solicitud.');
        return data;
    }
    document.querySelectorAll('[data-turno-inicio]').forEach(button => button.addEventListener('click', async () => {
        if (button.disabled || dialog.open || sending) return;
        const current = ++generation;
        if (uncertainBody !== null) {
            dialog.showModal();
            error.textContent = uncertainMessage;
            return;
        }
        ready = false; choices = []; items.replaceChildren(); error.textContent = '';
        dialog.showModal();
        try {
            const data = await responseJson(await fetch(`${form.dataset.recursosUrl}?id=${encodeURIComponent(form.dataset.turnoId)}`));
            if (current !== generation || !dialog.open) return;
            if (!Array.isArray(data.items)) throw new Error('No se pudieron consultar los recursos.');
            for (const item of data.items) {
                if (!Number.isInteger(item.indice) || item.indice < 0 || !Array.isArray(item.opciones)) throw new Error('No se pudieron consultar los recursos.');
                const group = document.createElement('div');
                const label = document.createElement('label');
                const select = document.createElement('select');
                const id = `turno-recurso-${item.indice}`;
                label.setAttribute('for', id); label.textContent = `${item.nombre} (${item.dosis} ${item.unidad}) *`;
                select.id = id; select.name = `seleccion-${item.indice}`; select.required = true;
                select.className = 'block w-full rounded-lg border border-gray-400 bg-gray-50 p-2.5 text-gray-900 dark:bg-gray-700 dark:text-white';
                const placeholder = document.createElement('option'); placeholder.value = ''; placeholder.textContent = item.opciones.length ? 'Seleccione un insumo' : 'No hay insumos disponibles'; select.append(placeholder);
                for (const option of item.opciones) {
                    const node = document.createElement('option'); node.value = option.id; node.textContent = `${option.nombre} (${option.unidad})`; select.append(node);
                }
                select.addEventListener('input', () => { select.removeAttribute('aria-invalid'); if (uncertainBody === null) error.textContent = ''; });
                group.append(label, select); items.append(group); choices.push({ indice: item.indice, select });
            }
            ready = true;
        } catch (e) {
            if (current === generation && dialog.open) error.textContent = e.message || 'No se pudieron consultar los recursos.';
        }
    }));
    cancel.addEventListener('click', () => { if (!sending) dialog.close(); });
    dialog.addEventListener('cancel', event => { if (sending) event.preventDefault(); });
    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (sending || !ready || !dialog.open) return;
        const missing = uncertainBody === null && choices.find(choice => !choice.select.value);
        if (missing) { error.textContent = 'Seleccione un insumo para cada recurso.'; missing.select.setAttribute('aria-invalid', 'true'); missing.select.focus(); return; }
        const seleccion = {};
        for (const choice of choices) seleccion[choice.indice] = choice.select.value;
        const body = uncertainBody ?? JSON.stringify({ turnoId: form.dataset.turnoId, seleccion });
        sending = true; submit.disabled = true; cancel.disabled = true; error.textContent = '';
        try {
            const response = await fetch(form.dataset.inicioUrl, {
                method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': form.querySelector('[name="__RequestVerificationToken"]').value }, body
            });
            let data;
            try { data = await response.json(); } catch { throw new Error(uncertainMessage); }
            if (response.status >= 500 || !data || typeof data.success !== 'boolean') throw new Error(uncertainMessage);
            if (!response.ok || !data.success) {
                if (uncertainBody !== null) throw new Error(uncertainMessage);
                error.textContent = data.message || 'No se pudo iniciar el turno.';
            } else {
                if (typeof data.lavadoId !== 'string' || !data.lavadoId) throw new Error(uncertainMessage);
                window.location.href = form.dataset.lavadoUrl + encodeURIComponent(data.lavadoId);
                return;
            }
        } catch {
            uncertainBody = body;
            for (const choice of choices) choice.select.disabled = true;
            error.textContent = uncertainMessage;
        }
        sending = false; submit.disabled = false; cancel.disabled = false;
    });
})();
