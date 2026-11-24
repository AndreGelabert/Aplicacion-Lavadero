(() => {
    const createModal = document.getElementById('insumo-form');
    const inventoryForm = createModal;
    const title = document.getElementById('form-title');
    const accordion = document.getElementById('accordion-flush-body-1');
    const accordionButton = document.querySelector('[data-accordion-target="#accordion-flush-body-1"]');
    const providerBox = inventoryForm?.querySelector('#insumo-proveedores');
    const providerSearch = inventoryForm?.querySelector('#insumo-proveedor-search');
    const providerDropdown = inventoryForm?.querySelector('#insumo-proveedor-dropdown');
    const providerList = inventoryForm?.querySelector('#insumo-proveedor-selected');
    const providerInputs = inventoryForm?.querySelector('#hiddenProveedoresIds');
    const providerOptions = providerDropdown ? [...providerDropdown.querySelectorAll('[data-provider-option]')] : [];
    let selectedProvider = null;
    function closeProviders() { if (!providerDropdown) return; providerDropdown.classList.add('hidden'); providerSearch.setAttribute('aria-expanded', 'false'); }
    function filterProviders() {
        if (!providerDropdown || providerSearch.disabled) return;
        selectedProvider = null;
        const term = providerSearch.value.trim().toLocaleLowerCase('es');
        providerOptions.forEach(option => { option.classList.toggle('hidden', !option.dataset.name.toLocaleLowerCase('es').includes(term) || [...providerInputs.children].some(input => input.value === option.dataset.id)); });
        providerDropdown.classList.remove('hidden'); providerSearch.setAttribute('aria-expanded', 'true');
    }
    function addProvider(id, name) {
        if (!providerInputs || [...providerInputs.children].some(input => input.value === id)) return;
        const input = document.createElement('input'); input.type = 'hidden'; input.name = 'ProveedoresIds'; input.value = id;
        const row = document.createElement('div'); row.className = 'flex items-center gap-3 p-2 bg-white dark:bg-gray-800 rounded border border-gray-200 dark:border-gray-600';
        const label = document.createElement('span'); label.className = 'flex-1 dark:text-white'; label.textContent = name;
        const remove = document.createElement('button'); remove.type = 'button'; remove.className = 'w-8 h-8 flex items-center justify-center text-red-600 hover:text-red-800'; remove.setAttribute('aria-label', 'Quitar proveedor ' + name);
        remove.innerHTML = '<svg aria-hidden="true" fill="currentColor" class="w-5 h-5" viewBox="0 0 24 24"><path fill-rule="evenodd" d="M12 2.25c-5.385 0-9.75 4.365-9.75 9.75s4.365 9.75 9.75 9.75 9.75-4.365 9.75-9.75S17.385 2.25 12 2.25Zm-1.72 6.97a.75.75 0 1 0-1.06 1.06L10.94 12l-1.72 1.72a.75.75 0 1 0 1.06 1.06L12 13.06l1.72 1.72a.75.75 0 1 0 1.06-1.06L13.06 12l1.72-1.72a.75.75 0 1 0-1.06-1.06L12 10.94l-1.72-1.72Z" clip-rule="evenodd"/></svg>';
        remove.addEventListener('click', () => { if (remove.disabled) return; row.remove(); input.remove(); providerSearch.focus(); });
        row.append(label, remove); providerList.append(row); providerInputs.append(input);
    }
    providerSearch?.addEventListener('focus', filterProviders);
    providerSearch?.addEventListener('input', filterProviders);
    providerOptions.forEach(option => option.addEventListener('click', () => { if (option.disabled) return; selectedProvider = option; providerSearch.value = option.dataset.name; closeProviders(); }));
    inventoryForm?.querySelector('[data-add-provider]')?.addEventListener('click', e => {
        if (e.currentTarget.disabled || !selectedProvider || providerSearch.disabled) return;
        addProvider(selectedProvider.dataset.id, selectedProvider.dataset.name); selectedProvider = null; providerSearch.value = ''; closeProviders(); providerSearch.focus();
    });
    if (providerBox) {
        document.addEventListener('click', e => { if (!providerBox.contains(e.target)) closeProviders(); });
        document.addEventListener('keydown', e => { if (e.key === 'Escape' && !providerDropdown.classList.contains('hidden')) { e.preventDefault(); closeProviders(); providerSearch.focus(); } });
    }
    document.querySelector('[data-clear-insumos-filters]')?.addEventListener('click', () => {
        document.querySelectorAll('#insumosFiltros input[type="checkbox"]').forEach(input => input.checked = input.name === 'estados' && input.value === 'Activo');
        const search = document.getElementById('insumos-search'); if (search) search.value = '';
    });
    function resetInventory() {
        if (!inventoryForm) return;
        inventoryForm.reset();
        inventoryForm.elements.crear.value = 'true';
        inventoryForm.elements.Id.value = '';
        const type = inventoryForm.querySelector('select[name="TipoId"]');
        const unit = inventoryForm.querySelector('select[name="UnidadMedida"]');
        const hiddenType = inventoryForm.querySelector('input[name="TipoId"]');
        const hiddenUnit = inventoryForm.querySelector('input[name="UnidadMedida"]');
        type.disabled = unit.disabled = false; hiddenType.disabled = hiddenUnit.disabled = true;
        inventoryForm.querySelectorAll('[data-open-tipos], [data-delete-tipo]').forEach(b => b.disabled = false);
        providerList?.replaceChildren(); providerInputs?.replaceChildren(); selectedProvider = null;
        if (providerSearch) { providerSearch.value = ''; closeProviders(); }
        const resetLabel = inventoryForm.querySelector('[data-reset-label]'); if (resetLabel) resetLabel.textContent = 'Limpiar campos';
        inventoryForm.querySelector('[type="submit"]').disabled = inventoryForm.dataset.canCreate !== 'true';
        title.textContent = 'Registrando un Insumo';
    }
    inventoryForm?.querySelector('[data-reset-insumo]')?.addEventListener('click', resetInventory);
    if (inventoryForm && inventoryForm.dataset.canCreate !== 'true') resetInventory();
    document.querySelectorAll('[data-edit-insumo]').forEach(button => button.addEventListener('click', () => {
        const record = JSON.parse(button.dataset.record);
        resetInventory();
        inventoryForm.elements.crear.value = 'false'; inventoryForm.elements.Id.value = record.Id;
        inventoryForm.elements.Nombre.value = record.Nombre;
        inventoryForm.elements.StockMinimo.value = record.StockMinimo;
        inventoryForm.elements.CantidadReabastecimiento.value = record.CantidadReabastecimiento;
        inventoryForm.elements.ReabastecimientoAutomatico.checked = record.ReabastecimientoAutomatico;
        inventoryForm.elements.Tipo.value = record.Tipo || '';
        const type = inventoryForm.querySelector('select[name="TipoId"]'), unit = inventoryForm.querySelector('select[name="UnidadMedida"]');
        const hiddenType = inventoryForm.querySelector('input[name="TipoId"]'), hiddenUnit = inventoryForm.querySelector('input[name="UnidadMedida"]');
        type.value = record.TipoId || ''; window.actualizarUnidadesInsumo(type); unit.value = record.UnidadMedida || '';
        hiddenType.value = record.TipoId || ''; hiddenUnit.value = record.UnidadMedida || '';
        type.disabled = unit.disabled = true; hiddenType.disabled = hiddenUnit.disabled = false;
        inventoryForm.querySelectorAll('[data-open-tipos], [data-delete-tipo]').forEach(b => b.disabled = true);
        for (const id of record.ProveedoresIds || []) {
            const option = providerOptions.find(item => item.dataset.id === id);
            addProvider(id, option?.dataset.name || 'Proveedor no disponible (' + id + ')');
        }
        const resetLabel = inventoryForm.querySelector('[data-reset-label]'); if (resetLabel) resetLabel.textContent = 'Cancelar';
        inventoryForm.querySelector('[type="submit"]').disabled = false;
        title.textContent = 'Editando un Insumo';
        if (accordion.classList.contains('hidden')) accordionButton.click();
        inventoryForm.scrollIntoView({ block: 'start' }); inventoryForm.elements.Nombre.focus();
    }));
    let dialogTrigger = null;
    function openDialog(id, trigger) { dialogTrigger = trigger; const modal = document.getElementById(id); modal.classList.remove('hidden'); modal.querySelector('input:not([type="hidden"]), button')?.focus(); }
    document.querySelectorAll('[data-ajustar-insumo]').forEach(button => button.addEventListener('click', () => { const modal = document.getElementById('insumoAjusteModal'); modal.querySelector('form').reset(); modal.querySelector('[name="insumoId"]').value = button.dataset.id; modal.querySelector('[data-ajuste-unidad]').textContent = `Ingrese una cantidad mayor que cero en ${window.etiquetaUnidadInsumo(button.dataset.unidadAjuste)}.`; openDialog('insumoAjusteModal', button); }));
    document.querySelectorAll('[data-estado-insumo]').forEach(button => button.addEventListener('click', () => {
        const modal = document.getElementById('insumoConfirmModal'), deactivate = button.dataset.estado === 'Inactivo';
        modal.querySelector('[name="id"]').value = button.dataset.id; modal.querySelector('[name="estado"]').value = button.dataset.estado;
        document.getElementById('insumoConfirmTitle').textContent = deactivate ? 'Desactivar Insumo' : 'Reactivar Insumo';
        const name = document.createElement('strong'); name.textContent = button.dataset.nombre;
        document.getElementById('insumoConfirmMessage').replaceChildren(`¿Confirma ${deactivate ? 'desactivar' : 'reactivar'} el insumo `, name, '?');
        const submit = document.getElementById('insumoConfirmSubmit'); submit.textContent = deactivate ? 'Desactivar' : 'Reactivar';
        submit.className = deactivate ? 'app-button app-button--danger' : 'app-button app-button--success';
        const wrapper = document.getElementById('insumoConfirmIconWrapper'), icon = document.getElementById('insumoConfirmIcon');
        wrapper.className = `w-12 h-12 rounded-full ${deactivate ? 'bg-red-100 dark:bg-red-900' : 'bg-green-100 dark:bg-green-900'} p-2 flex items-center justify-center mx-auto mb-3.5`;
        icon.className.baseVal = `w-8 h-8 ${deactivate ? 'text-red-600 dark:text-red-400' : 'text-green-500 dark:text-green-400'}`;
        icon.innerHTML = deactivate ? '<path fill-rule="evenodd" d="M12 2.25c-5.385 0-9.75 4.365-9.75 9.75s4.365 9.75 9.75 9.75 9.75-4.365 9.75-9.75S17.385 2.25 12 2.25Zm-1.72 6.97a.75.75 0 1 0-1.06 1.06L10.94 12l-1.72 1.72a.75.75 0 1 0 1.06 1.06L12 13.06l1.72 1.72a.75.75 0 1 0 1.06-1.06L13.06 12l1.72-1.72a.75.75 0 1 0-1.06-1.06L12 10.94l-1.72-1.72Z" clip-rule="evenodd"/>' : '<path fill-rule="evenodd" d="M2.25 12c0-5.385 4.365-9.75 9.75-9.75s9.75 4.365 9.75 9.75-4.365 9.75-9.75 9.75S2.25 17.385 2.25 12Zm13.36-1.814a.75.75 0 1 0-1.22-.872l-3.236 4.53L9.53 12.22a.75.75 0 0 0-1.06 1.06l2.25 2.25a.75.75 0 0 0 1.14-.094l3.75-5.25Z" clip-rule="evenodd"/>';
        openDialog('insumoConfirmModal', button);
    }));
    document.querySelectorAll('[data-close-dialog]').forEach(button => button.addEventListener('click', () => { button.closest('[role="dialog"]').classList.add('hidden'); dialogTrigger?.focus(); }));
    document.querySelectorAll('[data-sort-insumos]').forEach(button => button.addEventListener('click', () => {
        const table = button.closest('table'), body = table.tBodies[0];
        const ascending = button.dataset.order !== 'asc';
        const rows = [...body.rows];
        rows.sort((a, b) => (ascending ? 1 : -1) * a.cells[0].textContent.trim().localeCompare(b.cells[0].textContent.trim(), 'es', { sensitivity: 'base', numeric: true }));
        rows.forEach(row => body.appendChild(row));
        button.dataset.order = ascending ? 'asc' : 'desc';
        button.closest('th').setAttribute('aria-sort', ascending ? 'ascending' : 'descending');
    }));
    const typesModal = document.getElementById('tiposInsumoModal');
    document.querySelectorAll('[data-unidad]').forEach(el => { el.textContent = window.etiquetaUnidadInsumo(el.dataset.unidad); });
    document.querySelectorAll('[data-page]').forEach(link => {
        const url = new URL(window.location.href);
        url.searchParams.set('page', link.dataset.page);
        link.href = url.toString();
    });
    if (!createModal || !typesModal) return;

    const form = document.getElementById('tipoInsumoForm');
    const message = document.getElementById('tiposInsumoMensaje');
    const table = document.getElementById('tiposInsumoTabla');
    const createForm = createModal;
    const selector = createForm.querySelector('select[name="TipoId"]');
    const deleteDialog = document.getElementById('confirmarEliminarTipoInsumo');
    let deleteId = '', deleteTrigger = null;
    function closeDelete() { deleteDialog.classList.add('hidden'); deleteId = ''; deleteTrigger?.focus(); }
    deleteDialog.querySelector('[data-cancel-delete-tipo]').addEventListener('click', closeDelete);
    deleteDialog.querySelector('[data-confirm-delete-tipo]').addEventListener('click', async () => {
        const result = await mutate(typesModal.dataset.stateUrl, { id: deleteId, estado: 'Inactivo' });
        if (result) closeDelete();
        else document.getElementById('confirmarEliminarTipoError').textContent = message.textContent;
    });
    const canEdit = typesModal.dataset.canEdit === 'true';
    let activeModal = null;
    let returnFocus = null;
    let previousType = '';
    let instances = new Map();

    function instance(el) {
        if (!instances.has(el)) {
            const modal = typeof window.Modal === 'function' ?
                (typeof window.Modal.getInstance === 'function' && window.Modal.getInstance(el)) || new window.Modal(el, { backdrop: 'static', closable: false }) : null;
            instances.set(el, modal);
        }
        return instances.get(el);
    }
    function show(el, focus) {
        const modal = instance(el);
        if (modal) modal.show(); else { el.classList.remove('hidden'); el.setAttribute('aria-hidden', 'false'); el.setAttribute('role', 'dialog'); }
        activeModal = el;
        (focus || el.querySelector('input:not([type="hidden"]), button'))?.focus();
    }
    function hide(el) {
        const modal = instance(el);
        if (modal) modal.hide(); else { el.classList.add('hidden'); el.setAttribute('aria-hidden', 'true'); }
        activeModal = null;
    }
    function closeTypes() { hide(typesModal); returnFocus?.focus(); }
    createModal.querySelector('[data-open-tipos]').addEventListener('click', e => {
        returnFocus = e.currentTarget;
        show(typesModal);
        refresh();
    });
    typesModal.querySelector('[data-close-tipos]').addEventListener('click', closeTypes);
    document.addEventListener('keydown', e => {
        if (e.key === 'Escape' && activeModal) {
            e.preventDefault();
            if (activeModal === typesModal) closeTypes();
            else hide(activeModal);
        } else if (e.key === 'Tab' && activeModal) {
            const items = [...activeModal.querySelectorAll('button:not([disabled]), input:not([type="hidden"]):not([disabled]), select:not([disabled]), a[href]')].filter(el => el.offsetParent !== null);
            if (!items.length) return;
            const first = items[0], last = items[items.length - 1];
            if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
            else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
        }
    });
    function updateSummaryUnits(selected = form.elements.UnidadResumen.value) {
        const unit = form.elements.UnidadResumen;
        window.actualizarUnidadesInsumo({
            selectedOptions: [{ dataset: { categoria: form.elements.Categoria.value } }],
            form: { querySelector: () => unit }
        });
        unit.value = selected;
    }
    form.elements.Categoria.addEventListener('change', () => updateSummaryUnits());
    function resetForm() { form.reset(); form.elements.crear.value = 'true'; form.elements.Id.value = ''; updateSummaryUnits(); }
    updateSummaryUnits();
    typesModal.querySelector('[data-reset-tipo]').addEventListener('click', resetForm);
    function button(label, handler) {
        const b = document.createElement('button'); b.type = 'button'; b.title = label; b.setAttribute('aria-label', label);
        const edit = label === 'Editar', activate = label === 'Reactivar';
        b.className = `p-1.5 text-white ${edit ? 'bg-blue-600 hover:bg-blue-700' : activate ? 'bg-green-600 hover:bg-green-700' : 'bg-red-600 hover:bg-red-700'} rounded-lg transition-colors`;
        const path = edit ? 'm16.862 4.487 1.687-1.687a2.25 2.25 0 0 1 3.182 3.182L10.5 17.213l-4.5 1.5 1.5-4.5L16.862 4.487z' : activate ? 'M4.5 12.75l6 6 9-13.5' : label === 'Eliminar' ? 'M14.74 9l-.346 9m-4.788 0L9.26 9M4.772 5.79l1.388 13.883a2.25 2.25 0 002.244 2.077h7.192a2.25 2.25 0 002.244-2.077L19.228 5.79M3.75 5.955a48.11 48.11 0 0116.5 0M9 5.4V4.5a3 3 0 016 0v.9' : 'M6 18L18 6M6 6l12 12';
        b.innerHTML = `<svg aria-hidden="true" class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="${path}" /></svg>`;
        b.addEventListener('click', handler); return b;
    }
    async function refresh(selectedId) {
        try {
            const response = await fetch(typesModal.dataset.listUrl, { headers: { Accept: 'application/json' } });
            if (!response.ok) throw new Error('No se pudieron cargar los tipos.');
            const types = await response.json();
            const selected = selectedId || selector.value;
            selector.replaceChildren(new Option('Seleccionar tipo', ''));
            table.replaceChildren();
            let hasTool = false;
            for (const type of types) {
                const protectedType = type.esProtegido || type.nombre.toLowerCase() === 'herramienta';
                hasTool ||= type.nombre.toLowerCase() === 'herramienta';
                if (type.estado === 'Activo') {
                    const option = new Option(type.nombre, type.id); option.dataset.categoria = type.categoria; selector.add(option);
                }
                const row = document.createElement('tr'); row.className = 'border-b dark:border-gray-700';
                for (const value of [type.nombre, type.categoria, window.etiquetaUnidadInsumo(type.unidadResumen), type.estado]) {
                    const cell = document.createElement('td'); cell.className = 'p-2'; cell.textContent = value; row.append(cell);
                }
                const actions = document.createElement('td'); actions.className = 'p-2 flex flex-wrap gap-1';
                if (protectedType) actions.textContent = 'Protegido';
                else if (canEdit) {
                    actions.append(button('Editar', () => {
                        form.elements.crear.value = 'false'; form.elements.Id.value = type.id;
                        form.elements.Nombre.value = type.nombre; form.elements.Categoria.value = type.categoria;
                        updateSummaryUnits(type.unidadResumen); form.elements.Nombre.focus();
                    }));
                    actions.append(button(type.estado === 'Activo' ? 'Desactivar' : 'Reactivar', () => mutate(typesModal.dataset.stateUrl, { id: type.id, estado: type.estado === 'Activo' ? 'Inactivo' : 'Activo' })));
                }
                row.append(actions); table.append(row);
            }
            typesModal.querySelector('[data-init-tool]').hidden = hasTool;
            selector.value = selected;
            if (selector.value !== previousType) { window.actualizarUnidadesInsumo(selector); previousType = selector.value; }
        } catch (error) { message.textContent = error.message; }
    }
    selector.addEventListener('change', () => { previousType = selector.value; });
    createModal.querySelector('[data-delete-tipo]')?.addEventListener('click', e => {
        if (!selector.value) { window.alert('Seleccione un tipo de insumo.'); return; }
        deleteId = selector.value; deleteTrigger = e.currentTarget;
        document.getElementById('confirmarEliminarTipoMensaje').textContent = `¿Confirma desactivar el tipo de insumo ${selector.selectedOptions[0].textContent}?`;
        document.getElementById('confirmarEliminarTipoError').textContent = '';
        deleteDialog.classList.remove('hidden');
        deleteDialog.querySelector('[data-cancel-delete-tipo]').focus();
    });
    async function mutate(url, values) {
        const data = new FormData();
        data.set('__RequestVerificationToken', form.querySelector('[name="__RequestVerificationToken"]').value);
        for (const [key, value] of Object.entries(values)) data.set(key, value);
        try {
            const response = await fetch(url, { method: 'POST', body: data, headers: { Accept: 'application/json' } });
            const result = await response.json();
            if (!response.ok) throw new Error(result.mensaje || 'No se pudo completar la operación.');
            message.textContent = result.mensaje;
            resetForm();
            await refresh(result.id);
            return true;
        } catch (error) { message.textContent = error.message || 'No se pudo completar la operación.'; return false; }
    }
    form.addEventListener('submit', e => {
        e.preventDefault();
        if (!form.reportValidity()) return;
        mutate(typesModal.dataset.saveUrl, Object.fromEntries(new FormData(form)));
    });
    typesModal.querySelector('[data-init-tool]').addEventListener('click', () => mutate(typesModal.dataset.toolUrl, {}));
})();
