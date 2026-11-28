(() => {
    'use strict';
    const fragment = location.hash.slice(1);
    history.replaceState(null, '', location.pathname);
    const params = new URLSearchParams(fragment);
    const keys = ['SolicitudId', 'Token'];
    const creds = {};
    const valid = keys.every(key => params.getAll(key).length === 1 && params.get(key).trim().length > 0);
    if (valid) keys.forEach(key => { creds[key] = params.get(key).trim(); });

    const titulo = document.getElementById('titulo');
    const subtitulo = document.getElementById('subtitulo');
    const meta = document.getElementById('meta');
    const formContainer = document.getElementById('formulario-contenedor');
    const itemsContainer = document.getElementById('items-contenedor');
    const totalEl = document.getElementById('total-estimado');
    const plazoInput = document.getElementById('plazo-entrega');
    const notasInput = document.getElementById('notas');
    const submitBtn = document.getElementById('enviar');
    const statusEl = document.getElementById('estado');

    const genericError = 'El enlace de cotización es inválido, expiró o ya fue utilizado. Consulte con el lavadero.';
    const nf2 = new Intl.NumberFormat('es-AR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const nfUnit = new Intl.NumberFormat('es-AR', { minimumFractionDigits: 2, maximumFractionDigits: 4 });

    function showStatus(text, isError) {
        statusEl.textContent = text;
        statusEl.className = isError ? 'status status-error' : 'status status-ok';
    }

    function parseDecimal(raw) {
        const text = String(raw ?? '').trim();
        if (!text) return NaN;
        const normalized = text.includes(',') && text.includes('.')
            ? text.replace(/\./g, '').replace(',', '.')
            : text.replace(',', '.');
        const value = Number(normalized);
        return Number.isFinite(value) && value >= 0 ? value : NaN;
    }

    const itemRows = [];
    let pending = false;
    let finished = false;

    function updatePreview() {
        let total = 0;
        let hasAny = false;
        itemRows.forEach(row => {
            const price = parseDecimal(row.priceInput.value);
            const mode = row.modeSelect.value;
            if (!Number.isFinite(price) || row.cantidad <= 0) {
                row.preview.textContent = '';
                return;
            }
            hasAny = true;
            if (mode === 'Final') {
                const unitPrice = price / row.cantidad;
                total += price;
                row.preview.textContent = `Precio unitario equivalente: $ ${nfUnit.format(unitPrice)} / ${row.unidad} — Subtotal: $ ${nf2.format(price)}`;
            } else {
                const subtotal = price * row.cantidad;
                total += subtotal;
                row.preview.textContent = `Subtotal estimado (${nf2.format(row.cantidad)} ${row.unidad}): $ ${nf2.format(subtotal)}`;
            }
        });
        totalEl.textContent = hasAny ? `Total estimado: $ ${nf2.format(total)}` : '';
    }

    function renderDetail(detail) {
        titulo.textContent = `Solicitud de cotización — ${detail.nombreLavadero || 'Lavadero'}`;
        subtitulo.textContent = 'Complete los precios, disponibilidad y plazo de entrega para los insumos solicitados.';

        meta.textContent = '';
        if (detail.fechaEntregaPreferida) {
            const pFecha = document.createElement('div');
            pFecha.textContent = `Fecha preferida de entrega: ${detail.fechaEntregaPreferida} (hora de Argentina)`;
            meta.appendChild(pFecha);
        }
        if (detail.notasSolicitud) {
            const pNotas = document.createElement('div');
            pNotas.textContent = `Observaciones: ${detail.notasSolicitud}`;
            meta.appendChild(pNotas);
        }
        if (meta.childNodes.length > 0) meta.classList.remove('hidden');

        itemsContainer.textContent = '';
        itemRows.length = 0;

        (detail.items || []).forEach(item => {
            const card = document.createElement('div');
            card.className = 'item';

            const title = document.createElement('div');
            title.className = 'item-title';
            title.textContent = `${item.nombreInsumo} — Cantidad solicitada: ${nf2.format(item.cantidad)} ${item.unidadMedida || 'unidad'}`;

            const grid = document.createElement('div');
            grid.className = 'grid';

            const priceLabel = document.createElement('label');
            priceLabel.textContent = 'Precio *';
            const priceInput = document.createElement('input');
            priceInput.type = 'number';
            priceInput.step = 'any';
            priceInput.min = '0';
            priceInput.placeholder = '0.00';
            priceInput.required = true;
            priceLabel.appendChild(priceInput);

            const modeLabel = document.createElement('label');
            modeLabel.textContent = 'Tipo de precio';
            const modeSelect = document.createElement('select');
            const optUnit = document.createElement('option');
            optUnit.value = 'Unitario';
            optUnit.textContent = 'Precio unitario';
            optUnit.selected = true;
            const optFinal = document.createElement('option');
            optFinal.value = 'Final';
            optFinal.textContent = 'Precio final (total del ítem)';
            modeSelect.append(optUnit, optFinal);
            modeLabel.appendChild(modeSelect);

            const availLabel = document.createElement('label');
            availLabel.textContent = 'Disponibilidad';
            const availSelect = document.createElement('select');
            const optYes = document.createElement('option');
            optYes.value = 'true';
            optYes.textContent = 'Disponible';
            const optNo = document.createElement('option');
            optNo.value = 'false';
            optNo.textContent = 'Sin disponibilidad';
            availSelect.append(optYes, optNo);
            availLabel.appendChild(availSelect);

            grid.append(priceLabel, modeLabel, availLabel);

            const preview = document.createElement('p');
            preview.className = 'preview';

            priceInput.addEventListener('input', updatePreview);
            modeSelect.addEventListener('change', updatePreview);

            card.append(title, grid, preview);
            itemsContainer.appendChild(card);

            itemRows.push({
                insumoId: item.insumoId,
                cantidad: Number(item.cantidad),
                unidad: item.unidadMedida || 'unidad',
                priceInput,
                modeSelect,
                availSelect,
                preview
            });
        });

        formContainer.classList.remove('hidden');
    }

    async function loadDetail() {
        if (!valid) {
            subtitulo.textContent = '';
            showStatus(genericError, true);
            return;
        }
        try {
            const response = await fetch('/api/cotizacion-proveedor/detalle', {
                method: 'POST',
                credentials: 'omit',
                redirect: 'error',
                referrerPolicy: 'no-referrer',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(creds)
            });
            if (!response.ok) {
                subtitulo.textContent = '';
                showStatus(response.status === 400 ? genericError : 'No pudimos cargar la solicitud en este momento. Reintente más tarde.', true);
                return;
            }
            const detail = await response.json();
            renderDetail(detail);
        } catch {
            subtitulo.textContent = '';
            showStatus('No pudimos cargar la solicitud en este momento. Reintente más tarde.', true);
        }
    }

    submitBtn.addEventListener('click', async () => {
        if (pending || finished) return;
        const plazoStr = String(plazoInput.value ?? '').trim();
        const plazoNum = Number(plazoStr);
        if (!plazoStr || !Number.isInteger(plazoNum) || plazoNum < 0) {
            showStatus('Ingrese un plazo de entrega válido (en días enteros mayores o iguales a 0).', true);
            return;
        }
        const items = [];
        for (const row of itemRows) {
            const price = parseDecimal(row.priceInput.value);
            if (!Number.isFinite(price)) {
                showStatus('Ingrese un precio válido mayor o igual a 0 para todos los ítems.', true);
                return;
            }
            items.push({
                InsumoId: row.insumoId,
                Precio: price,
                TipoPrecio: row.modeSelect.value,
                Disponible: row.availSelect.value === 'true'
            });
        }

        pending = true;
        submitBtn.disabled = true;
        statusEl.className = 'status hidden';

        try {
            const response = await fetch('/api/cotizacion-proveedor/responder', {
                method: 'POST',
                credentials: 'omit',
                redirect: 'error',
                referrerPolicy: 'no-referrer',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    SolicitudId: creds.SolicitudId,
                    Token: creds.Token,
                    PlazoEntrega: plazoStr,
                    Notas: notasInput.value.trim(),
                    Items: items
                })
            });
            if (response.ok) {
                finished = true;
                keys.forEach(key => { delete creds[key]; });
                formContainer.classList.add('hidden');
                subtitulo.textContent = '';
                showStatus('Su cotización fue enviada correctamente. Ya puede cerrar esta página.', false);
            } else if (response.status === 400) {
                let message = genericError;
                try {
                    const body = await response.json();
                    if (body && typeof body.error === 'string' && body.error.trim().length > 0) {
                        message = body.error;
                    } else {
                        finished = true;
                        formContainer.classList.add('hidden');
                    }
                } catch {
                    finished = true;
                    formContainer.classList.add('hidden');
                }
                showStatus(message, true);
            } else {
                showStatus('No pudimos registrar su respuesta en este momento. Puede reintentar.', true);
            }
        } catch {
            showStatus('Error de conexión al enviar la respuesta. Puede reintentar.', true);
        } finally {
            pending = false;
            if (!finished) submitBtn.disabled = false;
        }
    });

    loadDetail();
})();
