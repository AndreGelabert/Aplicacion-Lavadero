(function () {
    function createActionButton(label, handler) {
        const b = document.createElement('button');
        b.type = 'button';
        b.title = label;
        b.setAttribute('aria-label', label);
        const isEdit = label === 'Editar';
        const isActivate = label === 'Reactivar';
        b.className = `p-1.5 text-white ${isEdit ? 'bg-blue-600 hover:bg-blue-700' : isActivate ? 'bg-green-600 hover:bg-green-700' : 'bg-red-600 hover:bg-red-700'} rounded-lg transition-colors`;
        const path = isEdit
            ? 'm16.862 4.487 1.687-1.687a2.25 2.25 0 0 1 3.182 3.182L10.5 17.213l-4.5 1.5 1.5-4.5L16.862 4.487z'
            : isActivate
                ? 'M4.5 12.75l6 6 9-13.5'
                : 'M6 18L18 6M6 6l12 12';
        b.innerHTML = `<svg aria-hidden="true" class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="${path}" /></svg>`;
        b.addEventListener('click', handler);
        return b;
    }

    function updateSelects(selectSelectors, tipos, deletedName) {
        if (!Array.isArray(tipos)) return;
        const selectors = Array.isArray(selectSelectors) ? selectSelectors : [selectSelectors];
        selectors.forEach(sel => {
            document.querySelectorAll(sel).forEach(selectEl => {
                const current = selectEl.value;
                const firstOption = selectEl.querySelector('option[value=""]') || selectEl.options[0];
                const placeholder = firstOption && firstOption.value === '' ? firstOption.textContent : 'Seleccionar...';
                selectEl.replaceChildren(new Option(placeholder, ''));
                tipos.forEach(nombre => selectEl.add(new Option(nombre, nombre)));
                if (current && current !== deletedName && tipos.includes(current)) {
                    selectEl.value = current;
                }
            });
        });
    }

    function renderEmptyStateRow(colSpan, emptyTitle, emptyDetail) {
        const tr = document.createElement('tr');
        const td = document.createElement('td');
        td.colSpan = colSpan;
        td.className = 'p-0';
        const wrapper = document.createElement('div');
        wrapper.setAttribute('data-table-empty-state', '');
        wrapper.className = 'flex flex-col items-center bg-white dark:bg-gray-800 px-4 py-8 text-center text-gray-500 dark:text-gray-400';
        wrapper.innerHTML = `<svg class="w-16 h-16 mb-4 text-gray-300 dark:text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"></path></svg>`;
        const pTitle = document.createElement('p');
        pTitle.className = 'text-lg font-medium';
        pTitle.textContent = emptyTitle || 'No hay tipos registrados';
        wrapper.appendChild(pTitle);
        if (emptyDetail) {
            const pDetail = document.createElement('p');
            pDetail.className = 'text-sm';
            pDetail.textContent = emptyDetail;
            wrapper.appendChild(pDetail);
        }
        td.appendChild(wrapper);
        tr.appendChild(td);
        return tr;
    }

    function hideModalElement(modal) {
        if (!modal) return;
        const id = modal.id;
        try {
            const fbInstance = window.FlowbiteInstances?.getInstance?.('Modal', id)
                || (typeof window.Modal?.getInstance === 'function' ? window.Modal.getInstance(modal) : null);
            if (fbInstance && typeof fbInstance.hide === 'function') {
                fbInstance.hide();
            }
        } catch (_) {
            // Fallback manual hide below
        }
        modal.classList.add('hidden');
        modal.classList.remove('flex');
        modal.setAttribute('aria-hidden', 'true');
        const anyOpenModal = document.querySelector('[role="dialog"]:not(.hidden), .fixed[aria-modal="true"]:not(.hidden)');
        if (!anyOpenModal) {
            document.querySelectorAll('[modal-backdrop]').forEach(el => el.remove());
            document.body.classList.remove('overflow-hidden');
        }
    }

    window.initTipoCatalogoModal = function (config) {
        const modal = document.getElementById(config.modalId);
        if (!modal || modal.dataset.abmInitialized === 'true') return;
        modal.dataset.abmInitialized = 'true';

        const form = document.getElementById(config.formId);
        const message = document.getElementById(config.messageId);
        const table = document.getElementById(config.tableId);
        if (!form || !table) return;

        const canEdit = modal.dataset.canEdit === 'true';
        const canToggle = modal.dataset.canToggle !== 'false';
        const tokenInput = () => form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

        function setMessage(text, isError) {
            if (!message) return;
            message.textContent = text || '';
            message.className = text
                ? `mb-3 text-sm ${isError ? 'text-red-600 dark:text-red-400' : 'text-green-600 dark:text-green-400'}`
                : 'mb-3 text-sm dark:text-white';
        }

        function resetForm() {
            form.reset();
            if (form.elements.crear) form.elements.crear.value = 'true';
            if (form.elements.Id) form.elements.Id.value = '';
            if (typeof config.onReset === 'function') config.onReset(form);
        }

        function renderRows(items) {
            table.replaceChildren();
            if (!Array.isArray(items) || items.length === 0) {
                table.appendChild(renderEmptyStateRow(
                    config.columns.length + 2,
                    config.emptyTitle || 'No hay tipos registrados',
                    config.emptyDetail || 'Creá uno nuevo desde el formulario superior.'
                ));
                return;
            }

            items.forEach(item => {
                const tr = document.createElement('tr');
                tr.className = 'border-b dark:border-gray-700';

                config.columns.forEach((col, idx) => {
                    const td = document.createElement('td');
                    td.className = idx === 0 ? 'px-3 py-2 w-full' : 'px-3 py-2 whitespace-nowrap';
                    td.textContent = col.render ? col.render(item) : (item[col.field] ?? '');
                    tr.appendChild(td);
                });

                const estadoTd = document.createElement('td');
                estadoTd.className = 'px-3 py-2 whitespace-nowrap';
                const estado = item.estado || item.Estado || 'Activo';
                const badge = document.createElement('span');
                badge.className = estado === 'Activo'
                    ? 'px-2 py-0.5 text-xs font-medium rounded-full bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200'
                    : 'px-2 py-0.5 text-xs font-medium rounded-full bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-300';
                badge.textContent = estado;
                estadoTd.appendChild(badge);
                tr.appendChild(estadoTd);

                const actionsTd = document.createElement('td');
                actionsTd.className = 'px-3 py-2 flex gap-1.5 whitespace-nowrap';
                if (canEdit) {
                    actionsTd.appendChild(createActionButton('Editar', () => {
                        if (form.elements.crear) form.elements.crear.value = 'false';
                        if (form.elements.Id) form.elements.Id.value = item.id || item.Id || '';
                        config.fillForm(form, item);
                        setMessage('', false);
                        form.querySelector('input:not([type="hidden"])')?.focus();
                    }));
                }
                if (canToggle) {
                    const nextEstado = estado === 'Activo' ? 'Inactivo' : 'Activo';
                    const label = estado === 'Activo' ? 'Desactivar' : 'Reactivar';
                    actionsTd.appendChild(createActionButton(label, async () => {
                        const body = new URLSearchParams({
                            id: item.id || item.Id || '',
                            estado: nextEstado,
                            __RequestVerificationToken: tokenInput()
                        });
                        const response = await fetch(modal.dataset.stateUrl, {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'Accept': 'application/json' },
                            body
                        });
                        const data = await response.json().catch(() => ({}));
                        if (!response.ok) {
                            setMessage(data.mensaje || 'No se pudo cambiar el estado.', true);
                            return;
                        }
                        setMessage(data.mensaje || 'Estado actualizado.', false);
                        await refresh();
                        updateSelects(config.selectSelectors, data.tipos, nextEstado === 'Inactivo' ? (item.nombre || item.Nombre) : null);
                        if (typeof config.onUpdated === 'function') config.onUpdated(data);
                    }));
                }
                tr.appendChild(actionsTd);
                table.appendChild(tr);
            });
        }

        async function refresh() {
            try {
                const response = await fetch(modal.dataset.listUrl, { headers: { Accept: 'application/json' } });
                if (!response.ok) throw new Error('No se pudieron cargar los tipos.');
                const items = await response.json();
                renderRows(items);
            } catch (err) {
                setMessage(err.message || 'Error al cargar los tipos.', true);
            }
        }

        form.addEventListener('submit', async e => {
            e.preventDefault();
            setMessage('', false);
            const formData = new FormData(form);
            const response = await fetch(modal.dataset.saveUrl, {
                method: 'POST',
                headers: { Accept: 'application/json' },
                body: formData
            });
            const data = await response.json().catch(() => ({}));
            if (!response.ok) {
                setMessage(data.mensaje || 'No se pudo guardar el tipo.', true);
                return;
            }
            setMessage(data.mensaje || 'Tipo guardado correctamente.', false);
            resetForm();
            await refresh();
            updateSelects(config.selectSelectors, data.tipos);
            if (typeof config.onUpdated === 'function') config.onUpdated(data);
        });

        document.addEventListener('click', e => {
            const openTrigger = e.target.closest(`[data-modal-toggle="${config.modalId}"], [data-modal-target="${config.modalId}"]`);
            if (openTrigger && !openTrigger.closest(`#${config.modalId}`)) {
                resetForm();
                setMessage('', false);
                refresh();
                return;
            }

            const closeTrigger = e.target.closest(`[data-modal-hide="${config.modalId}"]`);
            if (closeTrigger) {
                e.preventDefault();
                resetForm();
                setMessage('', false);
                hideModalElement(modal);
            }
        });

        document.addEventListener('keydown', e => {
            if (e.key === 'Escape' && !modal.classList.contains('hidden')) {
                resetForm();
                setMessage('', false);
                hideModalElement(modal);
            }
        });
    };
})();
