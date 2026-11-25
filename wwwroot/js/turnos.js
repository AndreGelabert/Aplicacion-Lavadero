(() => {
    const form = document.getElementById('turno-form');
    if (!form) return;
    const $ = id => document.getElementById(id);
    const message = (text, type) => {
        if ($('turnoMensaje')) $('turnoMensaje').textContent = text;
        if (text && typeof window !== 'undefined' && window.Notificaciones && typeof window.Notificaciones.mostrar === 'function') {
            window.Notificaciones.mostrar(text, type || (text.includes('correctamente') || text.includes('éxito') ? 'success' : 'info'));
        }
    };
    try {
        const fechaInput = $('Fecha');
        if (fechaInput) {
            const todayAr = new Date().toLocaleDateString('en-CA', { timeZone: 'America/Argentina/Buenos_Aires' });
            fechaInput.min = todayAr;
        }
    } catch { }
    const escapeHtml = str => {
        if (!str) return '';
        return String(str).replace(/[&<>"']/g, m => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[m]);
    };
    let vehicle = null, generation = 0, couponGeneration = 0, couponPercentages = new Map();
    let selectedOrder = [];
    let bookingPending = false, lockedPlate = '', capturedBooking = null, ambiguous = false, clientPending = false;
    let disabledControls = [];
    let editing = null, editLoading = false, editChanged = false;
    const container = $('turno-form-container');
    const canCreate = !container || container.dataset.puedeCrear === 'true';

    function lockControls() {
        disabledControls = [...form.querySelectorAll('input, select, textarea, button')].filter(control => control !== form.querySelector('[type=submit]')).map(control => [control, control.disabled]);
        for (const [control] of disabledControls) control.disabled = true;
        search.disabled = true;
    }
    function unlockControls() {
        for (const [control, disabled] of disabledControls) control.disabled = disabled;
        search.disabled = false;
    }
    const clearCoupons = () => {
        couponGeneration++;
        couponPercentages = new Map();
        $('CuponId').replaceChildren(new Option('Sin cupón', ''));
        $('turnoCuponMensaje').textContent = '';
        summary();
    };
    async function loadCoupons(clientId) {
        if (editing) return;
        clearCoupons();
        const current = couponGeneration;
        if (!clientId) return;
        try {
            const response = await fetch(`/TurnoCupones/Consultar?${new URLSearchParams({ clienteId: clientId })}`);
            if (!response.ok) throw new Error();
            const coupons = await response.json();
            if (current !== couponGeneration || $('ClienteId').value !== clientId) return;
            for (const coupon of coupons) {
                couponPercentages.set(coupon.id, coupon.percentage);
                const expiry = new Date(coupon.expiresUtc).toLocaleDateString('es-AR', { timeZone: 'America/Argentina/Buenos_Aires' });
                $('CuponId').add(new Option(`${coupon.percentage}% — vence ${expiry}`, coupon.id));
            }
            $('turnoCuponMensaje').textContent = coupons.length ? 'Seleccione un cupón si desea aplicarlo.' : 'No hay cupones disponibles.';
        } catch {
            if (current === couponGeneration && $('ClienteId').value === clientId) {
                $('turnoCuponMensaje').textContent = 'No se pudieron consultar los cupones.';
                message('No se pudieron consultar los cupones.', 'error');
            }
        }
    }
    const get = async (action, params) => {
        const response = await fetch(`/Turnos/${action}?${new URLSearchParams(params)}`);
        if (!response.ok) throw new Error();
        return response.json();
    };

    const resetVehicle = () => {
        vehicle = null; selectedOrder = []; $('VehiculoId').value = ''; $('ClienteId').value = ''; clearCoupons();
        if ($('abrirRegistroCliente')) $('abrirRegistroCliente').disabled = true;
        if ($('turnoVehiculoSeleccionado') && $('turnoVehiculoSeleccionado').classList) $('turnoVehiculoSeleccionado').classList.add('hidden');
        if ($('turnoVehiculoNombre')) $('turnoVehiculoNombre').textContent = '';
        if ($('turnoInfoVehiculoSection') && $('turnoInfoVehiculoSection').style) $('turnoInfoVehiculoSection').style.display = 'none';
        if ($('clientesAsociadosSection') && $('clientesAsociadosSection').style) $('clientesAsociadosSection').style.display = 'none';
        if ($('serviciosSection') && $('serviciosSection').style) $('serviciosSection').style.display = 'none';
        if ($('programacionSection') && $('programacionSection').style) $('programacionSection').style.display = 'none';
        if ($('resumenSection') && $('resumenSection').style) $('resumenSection').style.display = 'none';
        $('turnoInfoVehiculo').textContent = '';
        $('turnoServiciosPorVehiculo').replaceChildren();
        for (const id of ['ClienteTrajoId', 'ClienteRetiraId']) $(id).replaceChildren();
        $('turnoResumen').textContent = '';
        if ($('turnoPrecio')) $('turnoPrecio').textContent = '$0.00';
        if ($('turnoTiempo')) $('turnoTiempo').textContent = '0 min';
        if ($('turnoPrecioOriginal') && $('turnoPrecioOriginal').classList) $('turnoPrecioOriginal').classList.add('hidden');
        const pkgSelect = document.getElementById('paquete-select-turno');
        if (pkgSelect) { pkgSelect.disabled = false; pkgSelect.value = ''; }
        actualizarBotonSubmit();
    };

    let searchDebounceTimeout = null;
    $('turnoPatente').addEventListener('input', () => {
        if (bookingPending || capturedBooking) { $('turnoPatente').value = lockedPlate; return; }
        generation++; resetVehicle(); $('turnoPatenteResultados').replaceChildren();
        if ($('turnoPatenteResultados').classList) $('turnoPatenteResultados').classList.add('hidden');
        if (searchDebounceTimeout && typeof clearTimeout !== 'undefined') clearTimeout(searchDebounceTimeout);
        const val = $('turnoPatente').value ? $('turnoPatente').value.trim() : '';
        if (val.length >= 2 && typeof setTimeout !== 'undefined') {
            searchDebounceTimeout = setTimeout(() => {
                if (typeof search.click === 'function') search.click();
                else if (typeof search.fire === 'function') search.fire('click');
            }, 300);
        }
    });

    const search = document.createElement('button');
    search.type = 'button'; search.textContent = 'Buscar vehículo'; search.className = 'hidden';
    $('turnoPatente').after(search);
    search.addEventListener('click', async () => {
        if (bookingPending || capturedBooking) return;
        const current = ++generation; resetVehicle(); $('turnoPatenteResultados').replaceChildren();
        try {
            const vehicles = await get('BuscarVehiculosPorPatente', { search: $('turnoPatente').value });
            if (current !== generation) return;
            if (!vehicles.length) {
                $('turnoPatenteResultados').innerHTML = '<div class="p-3 text-gray-500 dark:text-gray-400">No se encontraron vehículos</div>';
                if ($('turnoPatenteResultados').classList) $('turnoPatenteResultados').classList.remove('hidden');
                return;
            }
            for (const v of vehicles) {
                const button = document.createElement('button'); button.type = 'button';
                button.textContent = v.descripcion;
                button.className = 'w-full text-left p-3 hover:bg-gray-100 dark:hover:bg-gray-600 cursor-pointer border-b dark:border-gray-600 last:border-b-0 block';
                if (typeof document.createElement === 'function' && button.innerHTML !== undefined) {
                    button.innerHTML = `
                        <div class="font-medium text-gray-900 dark:text-white">${escapeHtml(v.patente)}</div>
                        <div class="text-sm text-gray-500 dark:text-gray-400">${escapeHtml(v.descripcion)}</div>
                        <div class="text-xs text-gray-400 dark:text-gray-500">${escapeHtml(v.tipoVehiculo || '')}</div>
                    `;
                }
                button.addEventListener('click', () => selectVehicle(v));
                $('turnoPatenteResultados').append(button);
            }
            if ($('turnoPatenteResultados').classList) $('turnoPatenteResultados').classList.remove('hidden');
        } catch {
            if (current === generation) {
                message('No se pudieron buscar vehículos.');
                if ($('turnoPatenteResultados').classList) $('turnoPatenteResultados').classList.add('hidden');
            }
        }
    });

    if (typeof document.addEventListener === 'function') {
        document.addEventListener('click', event => {
            const results = $('turnoPatenteResultados');
            const patenteInput = $('turnoPatente');
            if (results && results.classList && patenteInput) {
                if (!patenteInput.contains?.(event.target) && !results.contains?.(event.target)) {
                    results.classList.add('hidden');
                }
            }
        });
    }

    async function selectVehicle(v, initial = null) {
        if (bookingPending || capturedBooking) return;
        const current = ++generation; resetVehicle(); vehicle = v;
        $('turnoPatente').value = v.patente; $('VehiculoId').value = v.id;
        $('turnoPatenteResultados').replaceChildren();
        if ($('turnoPatenteResultados').classList) $('turnoPatenteResultados').classList.add('hidden');
        if ($('turnoVehiculoSeleccionado') && $('turnoVehiculoSeleccionado').classList) $('turnoVehiculoSeleccionado').classList.remove('hidden');
        if ($('turnoVehiculoNombre')) $('turnoVehiculoNombre').textContent = `${v.patente} - ${v.descripcion}`;
        if ($('turnoInfoVehiculoSection') && $('turnoInfoVehiculoSection').style) $('turnoInfoVehiculoSection').style.display = 'block';

        if (typeof document.createElement === 'function' && $('turnoInfoVehiculo').innerHTML !== undefined) {
            $('turnoInfoVehiculo').innerHTML = `
                <p class="text-sm text-gray-600 dark:text-gray-400"><strong>Patente:</strong> ${escapeHtml(v.patente)}</p>
                <p class="text-sm text-gray-600 dark:text-gray-400"><strong>Vehículo:</strong> ${escapeHtml(v.descripcion)}</p>
                <p class="text-sm text-gray-600 dark:text-gray-400"><strong>Tipo:</strong> ${escapeHtml(v.tipoVehiculo || '')}</p>
            `;
        } else {
            $('turnoInfoVehiculo').textContent = v.descripcion;
        }

        try {
            const [clients, services, packages] = await Promise.all([
                get('ObtenerClientesVehiculo', { vehiculoId: v.id, patente: v.patente }),
                get('ObtenerServiciosPorTipoVehiculo', { tipoVehiculo: v.tipoVehiculo }),
                get('ObtenerPaquetesPorTipoVehiculo', { tipoVehiculo: v.tipoVehiculo })
            ]);
            if (current !== generation) return;
            for (const id of ['ClienteTrajoId', 'ClienteRetiraId'])
                for (const c of clients) $(id).add(new Option(`${c.nombre} (${c.documento})`, c.id));
            $('ClienteTrajoId').onchange = () => { $('ClienteId').value = $('ClienteTrajoId').value; loadCoupons($('ClienteId').value); };
            if (clients.length) {
                $('ClienteTrajoId').value = clients[0].id;
                $('ClienteRetiraId').value = clients[0].id;
            }
            $('ClienteTrajoId').onchange();
            if ($('abrirRegistroCliente')) $('abrirRegistroCliente').disabled = editing ? true : false;
            if ($('registroClientePatente')) $('registroClientePatente').textContent = v.patente;

            if ($('clientesAsociadosSection') && $('clientesAsociadosSection').style) $('clientesAsociadosSection').style.display = 'block';
            if ($('serviciosSection') && $('serviciosSection').style) $('serviciosSection').style.display = 'block';
            if ($('programacionSection') && $('programacionSection').style) $('programacionSection').style.display = 'block';
            if ($('resumenSection') && $('resumenSection').style) $('resumenSection').style.display = 'block';

            const target = $('turnoServiciosPorVehiculo');
            target.replaceChildren();

            // Fieldsets maintained for node unit tests and form values
            const checkboxesMap = new Map();
            function addFieldset(title, items, isPackage) {
                const field = document.createElement('fieldset'); const legend = document.createElement('legend');
                legend.textContent = title; field.append(legend);
                if (field.classList) field.classList.add('hidden');
                if (field.style) field.style.display = 'none';
                for (const item of items) {
                    const label = document.createElement('label'); label.className = 'block p-2 dark:text-white';
                    const checkbox = document.createElement('input'); checkbox.type = 'checkbox';
                    checkbox.dataset.selections = JSON.stringify(isPackage ? item.servicios.map(s => ({ servicioId: s.id, paqueteId: item.id })) : [{ servicioId: item.id }]);
                    checkbox.dataset.price = item.precio; checkbox.dataset.duration = item.tiempoEstimado;
                    checkbox.dataset.id = item.id;
                    checkbox.dataset.isPackage = isPackage ? 'true' : 'false';
                    checkbox.addEventListener('change', () => {
                        const key = (isPackage ? 'pkg_' : 'srv_') + item.id;
                        if (checkbox.checked) {
                            if (!selectedOrder.includes(key)) selectedOrder.push(key);
                        } else {
                            selectedOrder = selectedOrder.filter(k => k !== key);
                        }
                        renderCards();
                        summary();
                    });
                    checkboxesMap.set((isPackage ? 'pkg_' : 'srv_') + item.id, { checkbox, item, isPackage });
                    label.append(checkbox, document.createTextNode(` ${item.nombre} — $${item.precio} — ${item.tiempoEstimado} min`)); field.append(label);
                }
                target.append(field);
            }
            addFieldset('Servicios', services, false);
            addFieldset('Paquetes', packages, true);
            if (initial) {
                const saved = initial.vehiculos[0].servicios;
                for (const [key, entry] of checkboxesMap) {
                    const ids = entry.isPackage ? entry.item.servicios.map(s => s.id) : [entry.item.id];
                    const isChecked = ids.length > 0 && ids.every(id => saved.some(s => s.servicioId === id && (s.paqueteId || null) === (entry.isPackage ? entry.item.id : null)));
                    entry.checkbox.checked = isChecked;
                    if (isChecked && !selectedOrder.includes(key)) selectedOrder.push(key);
                }
            }

            // Rich visual container matching Lavados
            const visualDiv = document.createElement('div');
            visualDiv.className = 'mb-6 p-4 bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-600';
            const vehiculoNombre = `${v.patente} - ${v.descripcion}`;

            let visualHtml = `<h4 class="text-md font-medium text-gray-900 dark:text-white mb-4">${escapeHtml(vehiculoNombre)}</h4>`;

            if (packages && packages.length > 0) {
                visualHtml += `
                    <div class="mb-4">
                        <label class="block mb-2 text-sm font-medium text-gray-900 dark:text-white">Paquete de Servicios (solo uno)</label>
                        <select id="paquete-select-turno" class="bg-gray-50 border border-gray-300 text-gray-900 text-sm rounded-lg focus:ring-primary-500 focus:border-primary-500 block w-full p-2.5 dark:bg-gray-700 dark:border-gray-600 dark:text-white">
                            <option value="">Seleccionar paquete...</option>
                            ${packages.map(p => {
                                const precioOriginal = p.precioOriginal || 0;
                                const precioConDescuento = p.precio || (precioOriginal - (precioOriginal * (p.descuento || 0) / 100));
                                const desc = p.descuento ? ` (${p.descuento}% desc.)` : '';
                                const fmtOrig = (typeof window !== 'undefined' && window.SiteModule?.formatCurrency) ? window.SiteModule.formatCurrency(precioOriginal) : `$${precioOriginal}`;
                                const fmtDesc = (typeof window !== 'undefined' && window.SiteModule?.formatCurrency) ? window.SiteModule.formatCurrency(precioConDescuento) : `$${precioConDescuento}`;
                                return `<option value="${p.id}">${escapeHtml(p.nombre)} - ${fmtOrig}${desc} = ${fmtDesc}</option>`;
                            }).join('')}
                        </select>
                    </div>
                `;
            }

            visualHtml += `
                <div class="mb-4">
                    <label class="block mb-2 text-sm font-medium text-gray-900 dark:text-white">Servicios Individuales</label>
                    <select id="servicios-select-turno" class="bg-gray-50 border border-gray-300 text-gray-900 text-sm rounded-lg focus:ring-primary-500 focus:border-primary-500 block w-full p-2.5 dark:bg-gray-700 dark:border-gray-600 dark:text-white">
                        <option value="">Agregar servicio...</option>
                        ${services.map(s => `<option value="${s.id}">${escapeHtml(s.nombre)} - $${s.precio} (${s.tiempoEstimado} min)</option>`).join('')}
                    </select>
                </div>
                <div id="servicios-lista-turno" class="space-y-2">
                    <p class="text-gray-500 dark:text-gray-400 text-sm">No hay servicios seleccionados</p>
                </div>
            `;

            if (visualDiv.innerHTML !== undefined) {
                visualDiv.innerHTML = visualHtml;
                target.append(visualDiv);

                const pkgSelect = visualDiv.querySelector('#paquete-select-turno');
                if (pkgSelect) {
                    pkgSelect.addEventListener('change', () => {
                        const val = pkgSelect.value;
                        if (!val) return;
                        const entry = checkboxesMap.get('pkg_' + val);
                        const selected = [...checkboxesMap.values()].filter(e => e.checkbox.checked);
                        const types = selected.flatMap(e => e.isPackage ? e.item.servicios.map(s => s.tipo) : [e.item.tipo]);
                        const ids = new Set(selected.flatMap(e => e.isPackage ? e.item.servicios.map(s => s.id) : [e.item.id]));
                        let valid = !!entry && Array.isArray(entry.item.servicios) && entry.item.servicios.length > 0;
                        for (const service of entry?.item.servicios || []) {
                            const full = services.find(s => s.id === service.id);
                            if (!full || ids.has(service.id) || types.includes(full.tipo)) valid = false;
                            ids.add(service.id);
                            if (full) types.push(full.tipo);
                        }
                        if (!valid) {
                            pkgSelect.value = '';
                            message('No se puede agregar el paquete completo por servicios duplicados, incompatibles o ya seleccionados.', 'error');
                            return;
                        }
                        entry.checkbox.checked = true;
                        if (!selectedOrder.includes('pkg_' + val)) selectedOrder.push('pkg_' + val);
                        pkgSelect.value = '';
                        renderCards();
                        summary();
                    });
                }
                const srvSelect = visualDiv.querySelector('#servicios-select-turno');
                if (srvSelect) {
                    srvSelect.addEventListener('change', () => {
                        const val = srvSelect.value;
                        if (!val) return;
                        const entry = checkboxesMap.get('srv_' + val);
                        if (entry) {
                            entry.checkbox.checked = true;
                            if (!selectedOrder.includes('srv_' + val)) selectedOrder.push('srv_' + val);
                        }
                        srvSelect.value = '';
                        renderCards();
                        summary();
                    });
                }
            }

            function renderCards() {
                const listaDiv = target.querySelector ? target.querySelector('#servicios-lista-turno') : null;
                if (!listaDiv) return;

                for (const [key, entry] of checkboxesMap.entries()) {
                    if (entry.checkbox.checked && !selectedOrder.includes(key)) {
                        selectedOrder.push(key);
                    }
                }
                selectedOrder = selectedOrder.filter(key => {
                    const entry = checkboxesMap.get(key);
                    return entry && entry.checkbox.checked;
                });

                const selectedItems = selectedOrder.map(key => checkboxesMap.get(key)).filter(Boolean);

                const selectedServices = selectedItems.flatMap(({ item, isPackage, selectedServices }) => isPackage ? (selectedServices || item.servicios) : [item]);
                const selectedIds = selectedServices.map(s => s.id);
                const selectedTypes = selectedServices.map(s => s.tipo);
                const srvSelect = visualDiv?.querySelector ? visualDiv.querySelector('#servicios-select-turno') : null;
                if (srvSelect) {
                    Array.from(srvSelect.options).slice(1).forEach(option => {
                        const service = services.find(s => s.id === option.value);
                        const unavailable = selectedIds.includes(option.value) || (service && selectedTypes.includes(String(service.tipo)));
                        option.style.display = unavailable ? 'none' : '';
                        option.disabled = !!unavailable;
                    });
                }

                const pkgSelect = visualDiv?.querySelector ? visualDiv.querySelector('#paquete-select-turno') : $('paquete-select-turno');
                const hasSelectedPackage = selectedItems.some(x => x.isPackage);
                if (pkgSelect) {
                    pkgSelect.disabled = hasSelectedPackage || !!editing?.cuponId;
                    if (editing?.cuponId && pkgSelect.classList) pkgSelect.classList.add('bg-gray-200', 'dark:bg-gray-600', 'cursor-not-allowed');
                    if (!hasSelectedPackage && !editing?.cuponId) pkgSelect.value = '';
                }
                if (srvSelect && editing?.cuponId) {
                    srvSelect.disabled = true;
                    if (srvSelect.classList) srvSelect.classList.add('bg-gray-200', 'dark:bg-gray-600', 'cursor-not-allowed');
                }

                if (!selectedItems.length) {
                    listaDiv.innerHTML = '<p class="text-gray-500 dark:text-gray-400 text-sm">No hay servicios seleccionados</p>';
                    return;
                }

                const hideRemove = !!editing?.cuponId;
                listaDiv.innerHTML = selectedItems.map((sel, idx) => {
                    if (sel.isPackage) {
                        const p = sel.item;
                        return `
                            <div class="border-2 border-blue-200 dark:border-blue-700 rounded-lg p-2 bg-blue-50 dark:bg-blue-900/20">
                                <div class="flex items-center justify-between mb-2">
                                    <span class="text-xs font-semibold text-blue-600 dark:text-blue-400">📦 Paquete: ${escapeHtml(p.nombre)}</span>
                                    <div class="flex items-center gap-1 ${hideRemove ? 'hidden' : ''}">
                                        <button type="button" data-del-key="pkg_${p.id}" class="btn-remove-item p-1 text-red-400 hover:text-red-600" title="Eliminar">
                                            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path></svg>
                                        </button>
                                    </div>
                                </div>
                                ${(sel.selectedServices || p.servicios) ? (sel.selectedServices || p.servicios).map((s, sIdx) => {
                                    const sTipo = s.tipo ? `${escapeHtml(s.tipo)} • ` : '';
                                    const sDur = `${s.tiempoEstimado || 0} min`;
                                    const sPre = (typeof window !== 'undefined' && window.SiteModule?.formatCurrency) ? window.SiteModule.formatCurrency(s.precio || 0) : `$${s.precio || 0}`;
                                    return `
                                    <div class="flex items-center gap-3 p-2 bg-white dark:bg-gray-800 rounded ${sIdx > 0 ? 'mt-1' : ''}">
                                        <span class="text-gray-400 dark:text-gray-500 font-medium text-sm">${sIdx + 1}.</span>
                                        <div class="flex-1">
                                            <div class="font-medium text-gray-900 dark:text-white text-sm">${escapeHtml(s.nombre)}</div>
                                            <div class="text-xs text-gray-500 dark:text-gray-400">${sTipo}${sDur} • ${sPre}</div>
                                        </div>
                                    </div>
                                `; }).join('') : ''}
                            </div>
                        `;
                    } else {
                        const s = sel.item;
                        const sTipo = s.tipo ? `${escapeHtml(s.tipo)} • ` : '';
                        const sDur = `${s.tiempoEstimado || 0} min`;
                        const sPre = (typeof window !== 'undefined' && window.SiteModule?.formatCurrency) ? window.SiteModule.formatCurrency(s.precio || 0) : `$${s.precio || 0}`;
                        return `
                            <div class="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-700 rounded-lg">
                                <div class="flex items-center gap-3">
                                    <span class="text-gray-400 dark:text-gray-500 font-medium">${idx + 1}.</span>
                                    <div>
                                        <div class="font-medium text-gray-900 dark:text-white">${escapeHtml(s.nombre)}</div>
                                        <div class="text-sm text-gray-500 dark:text-gray-400">${sTipo}${sDur} • ${sPre}</div>
                                    </div>
                                </div>
                                <div class="flex items-center gap-1 ${hideRemove ? 'hidden' : ''}">
                                    <button type="button" data-del-key="srv_${s.id}" class="btn-remove-item p-1 text-red-400 hover:text-red-600" title="Eliminar">
                                        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path></svg>
                                    </button>
                                </div>
                            </div>
                        `;
                    }
                }).join('');

                listaDiv.querySelectorAll('.btn-remove-item').forEach(btn => {
                    btn.addEventListener('click', () => {
                        const key = btn.dataset.delKey;
                        const entry = checkboxesMap.get(key);
                        if (entry) {
                            entry.checkbox.checked = false;
                            selectedOrder = selectedOrder.filter(k => k !== key);
                            renderCards();
                            summary();
                        }
                    });
                });
            }

            if (initial) {
                for (const [id, value] of [['ClienteTrajoId', initial.clienteTrajoId], ['ClienteRetiraId', initial.clienteRetiraId]]) {
                    if (![...$(id).options].some(option => option.value === value)) $(id).add(new Option('Cliente no disponible', value));
                    $(id).value = value;
                }
                $('ClienteId').value = initial.clienteId;
                couponGeneration++;
                renderCards();
                $('turnoPrecio').textContent = `$${Number(initial.precioAcordado).toFixed(2)}`;
            }
            if (!clients.length) message('El vehículo no tiene clientes activos asociados.', 'warning');
        } catch {
            if (current === generation) { resetVehicle(); message('No se pudieron cargar los datos del vehículo.'); }
            if (initial) throw new Error('catalogue');
        }
    }

    const clientForm = $('registroClienteForm');
    if (clientForm) {
        const modal = $('registroClienteModal'), kind = $('registroClienteTipoDocumento'), number = $('registroClienteNumeroDocumento');
        const openModal = () => {
            if (!vehicle || bookingPending || capturedBooking) return;
            if (modal.showModal) modal.showModal();
            if (modal.classList) {
                modal.classList.remove('hidden');
                modal.classList.add('flex');
                modal.setAttribute('aria-hidden', 'false');
            }
            if (typeof document.body !== 'undefined' && document.body.classList) document.body.classList.add('overflow-hidden');
            if ($('registroClientePatente')) $('registroClientePatente').textContent = vehicle?.patente || '';
            if ($('registroClienteMensaje')?.classList) $('registroClienteMensaje').classList.add('hidden');
            actualizarFormatoDocumento();
        };
        const closeModal = () => {
            if (modal.close) modal.close();
            if (modal.classList) {
                modal.classList.add('hidden');
                modal.classList.remove('flex');
                modal.setAttribute('aria-hidden', 'true');
            }
            if (typeof document.body !== 'undefined' && document.body?.classList) document.body.classList.remove('overflow-hidden');
            clientForm.reset();
            if ($('registroClienteMensaje')?.classList) $('registroClienteMensaje').classList.add('hidden');
        };
        const actualizarFormatoDocumento = () => {
            if (!kind || !number) return;
            const option = kind.selectedOptions ? kind.selectedOptions[0] : null;
            const formato = option?.dataset?.formato || '';
            const regex = option?.dataset?.regex || '';
            const tipoHint = $('registroClienteTipoDocumentoHint');
            const docHint = $('registroClienteDocumentoHint');
            if (tipoHint) tipoHint.textContent = 'Seleccione un tipo de documento';
            if (docHint) docHint.textContent = kind.value ? (formato ? `Formato: ${formato}` : 'Ingrese el número de documento') : 'Selecciona el tipo de documento para ver el formato requerido';
            number.placeholder = formato || 'Ingrese número';
            if (regex && number.setAttribute) number.setAttribute('pattern', regex);
            else if (number.removeAttribute) number.removeAttribute('pattern');
            validateDocument();
        };
        const validateDocument = () => {
            if (!kind || !number) return;
            const option = kind.selectedOptions ? kind.selectedOptions[0] : null;
            const pattern = option?.dataset?.regex;
            if (number.setCustomValidity) {
                number.setCustomValidity(kind.value && number.value && (!pattern || !new RegExp(pattern).test(number.value.trim())) ? `Formato: ${option?.dataset?.formato || 'inválido'}` : '');
            }
        };
        if (kind) kind.addEventListener('change', actualizarFormatoDocumento);
        if (number) {
            number.addEventListener('input', validateDocument);
            number.addEventListener('blur', validateDocument);
        }
        if ($('abrirRegistroCliente')) $('abrirRegistroCliente').addEventListener('click', openModal);
        if ($('cerrarRegistroCliente')) $('cerrarRegistroCliente').addEventListener('click', closeModal);
        if ($('cancelarRegistroCliente')) $('cancelarRegistroCliente').addEventListener('click', closeModal);
        if (modal.addEventListener) {
            modal.addEventListener('click', event => { if (event.target === modal) closeModal(); });
        }
        if (typeof document.addEventListener === 'function') {
            document.addEventListener('keydown', event => { if (event.key === 'Escape' && modal.classList && !modal.classList.contains('hidden')) closeModal(); });
        }
        clientForm.addEventListener('submit', async event => {
            event.preventDefault();
            if (bookingPending || capturedBooking || clientPending) return;
            validateDocument();
            if (!vehicle || (clientForm.reportValidity && !clientForm.reportValidity())) return;
            clientPending = true;
            const selectedVehicle = vehicle.id, current = generation, button = clientForm.querySelector('[type=submit]');
            const data = {
                vehiculoId: selectedVehicle, tipoDocumento: kind.value, numeroDocumento: number.value.trim(),
                nombre: $('registroClienteNombre').value.trim(), apellido: $('registroClienteApellido').value.trim(),
                telefono: $('registroClienteTelefono').value.trim(), email: $('registroClienteEmail').value.trim(), claveAsociacion: $('registroClienteClave').value.trim()
            };
            if (button) button.disabled = true;
            try {
                const tokenInput = form.querySelector('[name="__RequestVerificationToken"]');
                const headers = { 'Content-Type': 'application/json' };
                if (tokenInput && tokenInput.value) headers['RequestVerificationToken'] = tokenInput.value;
                const response = await fetch('/Lavados/RegistrarClienteAsociado', { method: 'POST', headers, body: JSON.stringify(data) });
                const result = await response.json();
                if (!response.ok || !result.success) {
                    const msgEl = $('registroClienteMensaje');
                    if (msgEl) {
                        msgEl.textContent = result.message || 'No se pudo registrar el cliente.';
                        message(msgEl.textContent, 'error');
                        if (msgEl.classList) msgEl.classList.remove('hidden');
                    }
                    return;
                }
                if (current === generation && vehicle?.id === selectedVehicle) {
                    for (const id of ['ClienteTrajoId', 'ClienteRetiraId']) $(id).add(new Option(`${result.cliente.nombre} (${result.cliente.documento})`, result.cliente.id));
                    $('ClienteTrajoId').value = result.cliente.id;
                    $('ClienteRetiraId').value = result.cliente.id;
                    $('ClienteTrajoId').onchange();
                    message(result.message);
                }
                closeModal();
            } catch {
                const msgEl = $('registroClienteMensaje');
                if (msgEl) {
                    msgEl.textContent = 'No se pudo registrar el cliente.';
                    message(msgEl.textContent, 'error');
                    if (msgEl.classList) msgEl.classList.remove('hidden');
                }
            }
            finally { clientPending = false; if (button) button.disabled = false; }
        });
    }

    function summary() {
        const checked = [...$('turnoServiciosPorVehiculo').querySelectorAll('input:checked')];
        const discount = $('CuponId').value ? couponPercentages.get($('CuponId').value) || 0 : Number($('Descuento').value || 0);
        const subtotal = checked.reduce((n, c) => n + Number(c.dataset.price), 0);
        const price = subtotal * (1 - discount / 100);
        const time = checked.reduce((n, c) => n + Number(c.dataset.duration), 0);

        if ($('turnoPrecioOriginal') && $('turnoPrecioOriginal').classList) {
            if (discount > 0 && subtotal > 0) {
                $('turnoPrecioOriginal').textContent = `$${subtotal.toFixed(2)}`;
                $('turnoPrecioOriginal').classList.remove('hidden');
            } else {
                $('turnoPrecioOriginal').classList.add('hidden');
            }
        }
        $('turnoPrecio').textContent = `$${price.toFixed(2)}`;
        $('turnoTiempo').textContent = `${time} min`;
        $('turnoResumen').textContent = 'El servidor confirma el precio.';
        actualizarBotonSubmit();
    }

    function actualizarBotonSubmit() {
        if (editing) return;
        const submitBtn = document.getElementById('submit-button');
        if (!submitBtn) return;
        const hasVehicle = !!vehicle && !!$('VehiculoId')?.value;
        const hasCliente = !!$('ClienteId')?.value;
        const hasTrajo = !!$('ClienteTrajoId')?.value;
        const hasRetira = !!$('ClienteRetiraId')?.value;
        const hasFecha = !!$('Fecha')?.value;
        const hasHora = !!$('Hora')?.value;
        const hasServices = ($('turnoServiciosPorVehiculo')?.querySelectorAll ? $('turnoServiciosPorVehiculo').querySelectorAll('input:checked')?.length : 0) > 0;

        submitBtn.disabled = !(canCreate && hasVehicle && hasCliente && hasTrajo && hasRetira && hasFecha && hasHora && hasServices);
    }

    $('Fecha')?.addEventListener('input', actualizarBotonSubmit);
    $('Fecha')?.addEventListener('change', actualizarBotonSubmit);
    $('Hora')?.addEventListener('input', actualizarBotonSubmit);
    $('Hora')?.addEventListener('change', actualizarBotonSubmit);
    $('ClienteTrajoId')?.addEventListener('change', actualizarBotonSubmit);
    $('ClienteRetiraId')?.addEventListener('change', actualizarBotonSubmit);

    $('CuponId').addEventListener('change', () => {
        if ($('CuponId').value) $('Descuento').value = '0';
        summary();
    });
    $('Descuento').addEventListener('input', () => {
        if (Number($('Descuento').value) !== 0) $('CuponId').value = '';
        summary();
    });

    async function openEdit(id) {
        if (bookingPending || capturedBooking || editLoading) return;
        editLoading = true;
        const button = form.querySelector('[type=submit]');
        button.disabled = true;
        try {
            const response = await fetch(`/Turnos/DatosEdicion?${new URLSearchParams({ id })}`, { cache: 'no-store' });
            if (!response.ok) throw new Error();
            const data = await response.json();
            if (!data.versionEsperada || !data.vehiculos || data.vehiculos.length !== 1) throw new Error();
            editing = data; editChanged = false;
            const v = data.vehiculos[0];
            await selectVehicle({ id: v.vehiculoId, patente: v.patente, descripcion: v.patente, tipoVehiculo: v.tipoVehiculo }, data);
            const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Argentina/Buenos_Aires', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(new Date(data.inicioUtc));
            const part = type => parts.find(p => p.type === type).value;
            $('Fecha').value = `${part('year')}-${part('month')}-${part('day')}`;
            $('Hora').value = `${part('hour')}:${part('minute')}`;
            $('AceptaAdelanto').checked = data.aceptaAdelanto;
            $('Notas').value = data.notas || '';
            $('Descuento').value = data.descuento;
            $('CuponId').replaceChildren(new Option(data.cuponId ? 'Cupón aplicado' : 'Sin cupón', data.cuponId || ''));
            $('CuponId').disabled = true;
            if ($('CuponId')?.classList) $('CuponId').classList.add('bg-gray-200', 'dark:bg-gray-600', 'cursor-not-allowed');
            $('turnoPatente').disabled = true;
            if ($('turnoPatente')?.classList) $('turnoPatente').classList.add('bg-gray-200', 'dark:bg-gray-600', 'cursor-not-allowed');
            search.disabled = true;
            if ($('abrirRegistroCliente')) {
                $('abrirRegistroCliente').disabled = true;
                if ($('abrirRegistroCliente').classList) $('abrirRegistroCliente').classList.add('cursor-not-allowed', 'opacity-50');
            }
            if (data.cuponId) {
                for (const name of ['ClienteTrajoId', 'ClienteRetiraId', 'Notas', 'Descuento']) {
                    const el = $(name);
                    if (el) {
                        el.disabled = true;
                        if (el.classList) el.classList.add('bg-gray-200', 'dark:bg-gray-600', 'cursor-not-allowed');
                    }
                }
                const pkgSelect = document.getElementById('paquete-select-turno');
                if (pkgSelect) {
                    pkgSelect.disabled = true;
                    if (pkgSelect.classList) pkgSelect.classList.add('bg-gray-200', 'dark:bg-gray-600', 'cursor-not-allowed');
                }
                const srvSelect = document.getElementById('servicios-select-turno');
                if (srvSelect) {
                    srvSelect.disabled = true;
                    if (srvSelect.classList) srvSelect.classList.add('bg-gray-200', 'dark:bg-gray-600', 'cursor-not-allowed');
                }
                $('turnoServiciosPorVehiculo').querySelectorAll('input, select, button').forEach(control => {
                    control.disabled = true;
                    if (control.classList) control.classList.add('cursor-not-allowed');
                });
            }
            $('turnoEdicionAviso').hidden = false;
            $('turnoEdicionAviso').textContent = data.cuponId ? 'El cliente, cupón, vehículo, servicios, entrega, retiro, notas y descuento no pueden modificarse en turnos con cupón. Puede cambiar la fecha, hora y adelanto.' : 'El cliente del turno, el vehículo y el cupón no pueden cambiarse aquí. Cambiar servicios o descuento recalcula el precio; sin cambios se conserva el acuerdo.';
            $('form-title').textContent = 'Editando un Turno';
            button.innerHTML = `
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="w-5 h-5 mr-1.5 shrink-0" aria-hidden="true">
                    <path fill-rule="evenodd" d="M16.704 4.153a.75.75 0 01.143 1.052l-8 10.5a.75.75 0 01-1.127.075l-4.5-4.5a.75.75 0 011.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 011.05-.143z" clip-rule="evenodd" />
                </svg>
                Guardar Cambios
            `;
            if ($('clear-button')) {
                $('clear-button').innerHTML = `
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="w-5 h-5 mr-1.5 shrink-0" aria-hidden="true">
                        <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.28 7.22a.75.75 0 00-1.06 1.06L8.94 10l-1.72 1.72a.75.75 0 101.06 1.06L10 11.06l1.72 1.72a.75.75 0 101.06-1.06L11.06 10l1.72-1.72a.75.75 0 00-1.06-1.06L10 8.94 8.28 7.22z" clip-rule="evenodd" />
                    </svg>
                    Cancelar
                `;
            }
            $('accordion-flush-body-1').classList.remove('hidden');
            document.querySelector('[data-accordion-target="#accordion-flush-body-1"]')?.setAttribute('aria-expanded', 'true');
            $('Fecha').focus();
        } catch { editing = null; message('No se pudieron cargar los datos de edición.', 'error'); }
        finally { editLoading = false; button.disabled = !editing && !canCreate; }
    }
    if (!canCreate) form.querySelector('[type=submit]').disabled = true;
    if (container?.dataset.editar) openEdit(container.dataset.editar);
    $('turnoServiciosPorVehiculo').addEventListener('change', () => { if (editing) editChanged = true; });
    $('turnoServiciosPorVehiculo').addEventListener('click', event => { if (editing && event.target.closest?.('.btn-remove-item')) editChanged = true; });

    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (bookingPending || editLoading || (!editing && !canCreate)) return;
        if (clientPending) { message('Espere a que termine el registro del cliente.'); return; }
        if (!capturedBooking) {
            const selections = [...$('turnoServiciosPorVehiculo').querySelectorAll('input:checked')].flatMap(c => JSON.parse(c.dataset.selections));
            if (!vehicle || ((!editing || editChanged) && !selections.length)) { message('Seleccione un vehículo y al menos un servicio o paquete.'); return; }
            if (new Set(selections.map(s => s.servicioId)).size !== selections.length) { message('No seleccione el mismo servicio más de una vez.'); return; }
            capturedBooking = new FormData(form);
            if (!editing || editChanged) selections.forEach((s, i) => { capturedBooking.append(`Servicios[${i}].ServicioId`, s.servicioId); if (s.paqueteId) capturedBooking.append(`Servicios[${i}].PaqueteId`, s.paqueteId); });
            if (editing) {
                capturedBooking.delete('VehiculoId');
                capturedBooking.delete('ClienteId');
                capturedBooking.delete('CuponId');
                capturedBooking.delete('FormIdentity');
                if (!editChanged) capturedBooking.delete('Servicios');
                if (editing.cuponId || Number($('Descuento').value) === Number(editing.descuento)) capturedBooking.delete('Descuento');
                if (editing.cuponId) { capturedBooking.delete('ClienteTrajoId'); capturedBooking.delete('ClienteRetiraId'); capturedBooking.delete('Notas'); }
                capturedBooking.set('Id', editing.id);
                capturedBooking.set('VersionEsperada', editing.versionEsperada);
                capturedBooking.set('AceptaAdelanto', $('AceptaAdelanto').checked ? 'true' : 'false');
                if (!editing.cuponId) capturedBooking.set('Notas', $('Notas').value);
            }
            lockedPlate = $('turnoPatente').value; generation++; couponGeneration++;
            lockControls();
        }
        const button = form.querySelector('[type=submit]');
        bookingPending = true; button.disabled = true;
        try {
            if (editing && !capturedBooking.get('FormIdentity')) {
                try {
                    const tokenResponse = await fetch(`/Turnos/EditarToken?${new URLSearchParams({ id: editing.id })}`, { cache: 'no-store' });
                    const token = await tokenResponse.json();
                    if (!tokenResponse.ok || !token.identity) throw new Error('token');
                    capturedBooking.set('FormIdentity', token.identity);
                } catch {
                    capturedBooking = null; bookingPending = false; button.disabled = false;
                    unlockControls();
                    message('No se pudo preparar la solicitud. Intente nuevamente.', 'error');
                    return;
                }
            }
            const response = await fetch(editing ? '/Turnos/Editar' : form.action, { method: 'POST', body: capturedBooking });
            const result = await response.json();
            if (response.ok && result.success === true) {
                message(result.message || (editing ? 'Turno modificado correctamente.' : 'Turno registrado correctamente.'), 'success');
                if (typeof setTimeout !== 'undefined') {
                    setTimeout(() => {
                        window.location.href = '/Turnos';
                    }, 50);
                } else {
                    window.location.href = '/Turnos';
                }
                return;
            }
            if ((response.status === 400 || response.status === 409) && result.success === false && result.editable === true && !ambiguous) {
                capturedBooking = null; bookingPending = false; button.disabled = false;
                unlockControls();
                message(typeof result.message === 'string' && result.message ? result.message : 'Corrija los datos e intente nuevamente.', 'error');
                return;
            }
            const detail = typeof result.message === 'string' && result.message ? `${result.message} ` : '';
            ambiguous = true; bookingPending = false; button.disabled = false;
            message(`${detail}No se pudo confirmar el resultado. Reintente la misma solicitud sin modificarla.`, 'error');
            return;
        } catch { /* The server may have committed despite a missing response. */ }
        ambiguous = true; bookingPending = false; button.disabled = false;
        message('No se pudo confirmar el resultado. Reintente la misma solicitud sin modificarla.');
    });

    const clearBtn = $('clear-button');
    if (clearBtn) {
        clearBtn.addEventListener('click', () => {
            if (bookingPending || capturedBooking) return;
            if (editing) { window.location.href = '/Turnos'; return; }
            form.reset();
            resetVehicle();
            $('turnoPatente').value = '';
            actualizarBotonSubmit();
            button.innerHTML = `
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="w-5 h-5 mr-1.5 shrink-0" aria-hidden="true">
                    <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.857-9.809a.75.75 0 00-1.214-.882l-3.483 4.79-1.88-1.88a.75.75 0 10-1.06 1.061l2.5 2.5a.75.75 0 001.137-.089l4-5.5z" clip-rule="evenodd" />
                </svg>
                Registrar Turno
            `;
            clearBtn.innerHTML = `
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="w-5 h-5 mr-1.5 shrink-0" aria-hidden="true">
                    <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.28 7.22a.75.75 0 00-1.06 1.06L8.94 10l-1.72 1.72a.75.75 0 101.06 1.06L10 11.06l1.72 1.72a.75.75 0 101.06-1.06L11.06 10l1.72-1.72a.75.75 0 00-1.06-1.06L10 8.94 8.28 7.22z" clip-rule="evenodd" />
                </svg>
                Limpiar Campos
            `;
            message('');
        });
    }

    actualizarBotonSubmit();
})();