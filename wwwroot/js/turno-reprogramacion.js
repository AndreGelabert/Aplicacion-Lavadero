(() => {
    const dialog = document.getElementById('reprogramacion-dialog');
    const form = document.getElementById('reprogramacion-form');
    if (!dialog || !form) return;
    const error = document.getElementById('reprogramacion-error');
    const submit = document.getElementById('reprogramacion-submit');
    const close = document.getElementById('reprogramacion-cerrar');
    let sending = false, frozen = null, uncertain = false, refreshRequired = false;
    let lockedControls = [];
    function lockControls() {
        if (lockedControls.length) return;
        lockedControls = [...form.querySelectorAll('input, select, textarea, button')]
            .filter(control => control !== submit && control !== close)
            .map(control => [control, control.disabled]);
        for (const [control] of lockedControls) control.disabled = true;
    }
    function unlockControls() {
        for (const [control, disabled] of lockedControls) control.disabled = disabled;
        lockedControls = [];
    }
    const plate = document.getElementById('reprogramacion-patente');
    const search = document.getElementById('reprogramacion-buscar');
    const results = document.getElementById('reprogramacion-resultados');
    const vehicleLabel = document.getElementById('reprogramacion-vehiculo');
    const servicesTarget = document.getElementById('reprogramacion-servicios');
    const discount = document.getElementById('reprogramacion-descuento');
    const originalDiscount = discount?.value;
    let vehicle = null, generation = 0, loading = false;
    const get = async (action, params) => {
        const response = await fetch(`/Turnos/${action}?${new URLSearchParams(params)}`);
        if (!response.ok) throw new Error();
        return response.json();
    };
    if (search && plate && results && vehicleLabel && servicesTarget) {
        plate.addEventListener('input', () => {
            if (frozen) return;
            generation++; vehicle = null; loading = false;
            results.replaceChildren(); servicesTarget.replaceChildren(); vehicleLabel.textContent = '';
        });
        search.addEventListener('click', async () => {
            if (sending || frozen) return;
            const current = ++generation; vehicle = null; loading = true;
            results.replaceChildren(); servicesTarget.replaceChildren(); vehicleLabel.textContent = '';
            try {
                const vehicles = await get('BuscarVehiculosPorPatente', { search: plate.value });
                if (current !== generation) return;
                if (!vehicles.length) error.textContent = 'No se encontró un vehículo activo.';
                for (const item of vehicles) {
                    const button = document.createElement('button'); button.type = 'button'; button.textContent = item.descripcion;
                    button.addEventListener('click', async () => {
                        if (sending || frozen) return;
                        const selected = ++generation; loading = true; vehicle = null; results.replaceChildren(); servicesTarget.replaceChildren();
                        vehicleLabel.textContent = item.descripcion; plate.value = item.patente;
                        try {
                            const [services, packages] = await Promise.all([
                                get('ObtenerServiciosPorTipoVehiculo', { tipoVehiculo: item.tipoVehiculo }),
                                get('ObtenerPaquetesPorTipoVehiculo', { tipoVehiculo: item.tipoVehiculo })
                            ]);
                            if (selected !== generation) return;
                            const add = (title, items, isPackage) => {
                                const field = document.createElement('fieldset'), legend = document.createElement('legend');
                                legend.textContent = title; field.append(legend);
                                for (const entry of items) {
                                    const label = document.createElement('label'), checkbox = document.createElement('input');
                                    checkbox.type = 'checkbox'; checkbox.dataset.selections = JSON.stringify(isPackage ? entry.servicios.map(s => ({ servicioId: s.id, paqueteId: entry.id })) : [{ servicioId: entry.id }]);
                                    label.append(checkbox, document.createTextNode(` ${entry.nombre} — $${entry.precio}`)); field.append(label);
                                }
                                servicesTarget.append(field);
                            };
                            add('Servicios', services, false); add('Paquetes', packages, true);
                            vehicle = item; error.textContent = 'Seleccione todos los servicios y paquetes deseados.';
                        } catch { if (selected === generation) error.textContent = 'No se pudieron cargar los servicios.'; }
                        finally { if (selected === generation) loading = false; }
                    });
                    results.append(button);
                }
            } catch { if (current === generation) error.textContent = 'No se pudieron buscar vehículos.'; }
            finally { if (current === generation) loading = false; }
        });
    }
    const ambiguous = 'No se pudo confirmar el resultado. Reintente la misma solicitud o actualice el detalle para verificar el turno.';
    document.querySelectorAll('[data-turno-reprogramacion]').forEach(button => button.addEventListener('click', () => {
        if (!sending && !dialog.open) dialog.showModal();
    }));
    close.addEventListener('click', () => { if (!sending && !uncertain) dialog.close(); });
    dialog.addEventListener('cancel', e => { if (sending || uncertain) e.preventDefault(); });
    form.addEventListener('submit', async e => {
        e.preventDefault();
        if (sending || refreshRequired || !dialog.open) return;
        let selections = [];
        if (!frozen && loading) { error.textContent = 'Espere a que termine la consulta.'; return; }
        if (!frozen && plate && plate.value.trim() && !vehicle) { error.textContent = 'Seleccione un vehículo o borre la patente.'; return; }
        if (!frozen && vehicle) {
            selections = [...servicesTarget.querySelectorAll('input:checked')].flatMap(c => JSON.parse(c.dataset.selections));
            if (!selections.length || new Set(selections.map(s => s.servicioId)).size !== selections.length) {
                error.textContent = 'Seleccione servicios sin repetir al menos un servicio.'; return;
            }
        }
        sending = true; submit.disabled = true; close.disabled = true; error.textContent = '';
        try {
            if (!frozen) {
                const data = new FormData(form);
                if (vehicle) {
                    data.set('VehiculoId', vehicle.id);
                    selections.forEach((s, i) => { data.append(`Servicios[${i}].ServicioId`, s.servicioId); if (s.paqueteId) data.append(`Servicios[${i}].PaqueteId`, s.paqueteId); });
                }
                if (discount && discount.value !== originalDiscount) data.set('Descuento', discount.value);
                lockControls();
                const tokenResponse = await fetch(form.dataset.tokenUrl, { cache: 'no-store' });
                const token = await tokenResponse.json();
                if (!tokenResponse.ok || !token || typeof token.identity !== 'string' || !token.identity) throw new Error('token');
                data.set('FormIdentity', token.identity);
                frozen = Array.from(data.entries());
            }
            const body = new FormData();
            for (const [key, value] of frozen) body.append(key, value);
            const wasUncertain = uncertain;
            uncertain = true;
            const response = await fetch(form.action, { method: 'POST', body });
            const data = await response.json();
            if (response.ok && data && data.success === true) { window.location.reload(); return; }
            if (!wasUncertain && response.status >= 400 && response.status < 500 && data && data.success === false) {
                uncertain = false; frozen = null; refreshRequired = true;
                error.textContent = (data.message || 'No se pudo modificar el turno.') + ' Actualice el detalle antes de volver a intentar.';
                submit.disabled = true;
                close.disabled = false;
                unlockControls();
                sending = false;
                return;
            }
            throw new Error('ambiguous');
        } catch { error.textContent = uncertain ? ambiguous : 'No se pudo preparar la solicitud. Intente nuevamente.'; }
        if (!uncertain) unlockControls();
        sending = false; submit.disabled = false; close.disabled = uncertain;
    });
})();
