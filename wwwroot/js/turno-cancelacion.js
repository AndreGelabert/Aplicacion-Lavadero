(() => {
    const dialog = document.getElementById('turno-cancelacion-dialog');
    const form = document.getElementById('turno-cancelacion-form');
    if (!dialog || !form) return;
    const error = document.getElementById('turno-cancelacion-error');
    const submit = document.getElementById('turno-cancelacion-submit');
    const cancel = document.getElementById('turno-cancelacion-cancel');
    const close = document.getElementById('turno-cancelacion-close');
    const motivo = document.getElementById('turno-cancelacion-motivo');
    let sending = false;
    let opener = null;
    const ambiguous = 'No se pudo confirmar la cancelación. Actualice el detalle para verificar el estado antes de volver a intentar.';
    document.querySelectorAll('[data-turno-cancelacion]').forEach(button => button.addEventListener('click', () => {
        if (dialog.open || sending) return;
        error.textContent = '';
        if (motivo) motivo.value = '';
        if (button.dataset && button.dataset.turnoId) form.querySelector('[name="id"]').value = button.dataset.turnoId;
        opener = button;
        dialog.showModal();
    }));
    [cancel, close].filter(Boolean).forEach(button => button.addEventListener('click', () => { if (!sending) dialog.close(); }));
    dialog.addEventListener('cancel', event => { if (sending) event.preventDefault(); });
    dialog.addEventListener('close', () => {
        if (opener && opener.isConnected !== false && document.activeElement !== opener) opener.focus();
        opener = null;
    });
    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (sending || !dialog.open) return;
        if (!motivo.value.trim()) { error.textContent = 'Debe ingresar un motivo de cancelación.'; motivo.focus(); return; }
        const body = new FormData(form);
        sending = true; submit.disabled = true; if (cancel) cancel.disabled = true; if (close) close.disabled = true; error.textContent = '';
        try {
            const response = await fetch(form.action, { method: 'POST', body });
            let data;
            try { data = await response.json(); } catch { throw new Error(ambiguous); }
            if (response.ok && data && data.success === true) { window.location.reload(); return; }
            error.textContent = data && typeof data.message === 'string' && data.message ? data.message : ambiguous;
        } catch { error.textContent = ambiguous; }
        sending = false; submit.disabled = false; if (cancel) cancel.disabled = false; if (close) close.disabled = false;
    });
})();
